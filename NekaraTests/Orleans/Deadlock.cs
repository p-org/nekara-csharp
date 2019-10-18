using System;
using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using Nekara.Models;
using Orleans;
using Orleans.Hosting;

namespace Nekara.Tests.Orleans
{
    class Deadlock
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

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

        public static int x = 0;
        public static Lock lck;

        [TestMethod]
        public async static NativeTasks.Task Run()
        {
            // Setup();

            var foo = client.GetGrain<IFooGrain>(0);
            var bar = client.GetGrain<IBarGrain>(1);

            lck = new Lock(3);
            x = 0;

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
            Deadlock.lck.Acquire();

            Deadlock.nekara.ContextSwitch();
            int lx1 = Deadlock.x;

            Deadlock.nekara.ContextSwitch();
            int lx2 = Deadlock.x;

            Deadlock.lck.Release();

            Deadlock.nekara.Assert(lx1 == lx2, "Race!");
            
            return NativeTasks.Task.CompletedTask;
        }
    }

    public class BarGrain : Grain, IBarGrain
    {
        public NativeTasks.Task Bar()
        {
            //Deadlock.lck.Acquire();

            Deadlock.nekara.ContextSwitch();
            Deadlock.x = 1;

            // Deadlock.lck.Release();

            return NativeTasks.Task.CompletedTask;
        }
    }
}
