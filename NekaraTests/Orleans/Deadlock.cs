using System;
using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using Nekara.Models;
using Orleans;
using Orleans.Hosting;

namespace Nekara.Tests.Orleans
{
    public static class Globals
    {
        public static int x = 0;
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static Lock lck;
    }

    class Deadlock
    {
        static ISiloHost silo;
        static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(FooGrain), typeof(BarGrain));
            Console.WriteLine("Setup");
        }

        [TestTeardownMethod]
        public static void Teardown()
        {
            silo.StopAsync().Wait();
            Console.WriteLine("Teardown");
        }

        [TestMethod]
        public async static NativeTasks.Task Execute()
        {
            // Setup();

            var foo = client.GetGrain<IFooGrain>(0);
            var bar = client.GetGrain<IBarGrain>(1);

            Globals.lck = new Lock(3);
            Globals.x = 0;

            var t1 = Task.Run(() => foo.Foo().Wait());

            var t2 = Task.Run(() => bar.Bar().Wait());

            await Task.WhenAll(t1, t2);

            // Teardown();
        }
    }

    public interface IFooGrain : IGrainWithIntegerKey
    {
        NativeTasks.Task Foo();
    }

    public interface IBarGrain : IGrainWithIntegerKey
    {
        NativeTasks.Task Bar();
    }

    public class FooGrain : Grain, IFooGrain
    {
        public NativeTasks.Task Foo()
        {
            Globals.lck.Acquire();

            Globals.nekara.ContextSwitch();
            int lx1 = Globals.x;

            Globals.nekara.ContextSwitch();
            int lx2 = Globals.x;

            Globals.lck.Release();

            Globals.nekara.Assert(lx1 == lx2, "Race!");
            
            return NativeTasks.Task.CompletedTask;
        }
    }

    public class BarGrain : Grain, IBarGrain
    {
        public NativeTasks.Task Bar()
        {
            //Globals.lck.Acquire();

            Globals.nekara.ContextSwitch();
            Globals.x = 1;

            // Globals.lck.Release();

            return NativeTasks.Task.CompletedTask;
        }
    }
}
