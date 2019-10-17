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

    class Hello
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
        public async static void Teardown()
        {
            await silo.StopAsync();
        }

        [TestMethod]
        public async static NativeTasks.Task Execute()
        {
            Setup();

            var foo = client.GetGrain<IFooGrain>(0);
            var bar = client.GetGrain<IBarGrain>(1);

            Globals.lck = new Lock(3);
            Globals.x = 0;

            var t1 = Task.Run(() => {
                Console.WriteLine("Calling foo.Foo() ...");
                foo.Foo().Wait();
                Console.WriteLine("foo.Foo() returned");
            });

            var t2 = Task.Run(() => {
                Console.WriteLine("Calling bar.Bar() ...");
                bar.Bar().Wait();
                Console.WriteLine("bar.Bar() returned");
            });

            await Task.WhenAll(t1, t2);

            Teardown();
            Console.WriteLine("Exiting");
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
            Console.WriteLine("[Foo] trying to acquire lock");
            Globals.lck.Acquire();

            Console.WriteLine("[Foo] context switch");
            Globals.nekara.ContextSwitch();
            int lx1 = Globals.x;

            Console.WriteLine("[Foo] context switch");
            Globals.nekara.ContextSwitch();
            int lx2 = Globals.x;

            Console.WriteLine("[Foo] releasing lock");
            Globals.lck.Release();

            Globals.nekara.Assert(lx1 == lx2, "Race!");
            
            Console.WriteLine("[Foo] returning");
            return NativeTasks.Task.CompletedTask;
        }
    }

    public class BarGrain : Grain, IBarGrain
    {
        public NativeTasks.Task Bar()
        {
            //lck.Acquire();

            Console.WriteLine("[Bar] context switch");
            Globals.nekara.ContextSwitch();
            Globals.x = 1;

            // Release();

            Console.WriteLine("Bar EndTask");
            return NativeTasks.Task.CompletedTask;
        }
    }
}
