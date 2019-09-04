using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using NekaraModels = Nekara.Models;
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
        public static NekaraModels.Lock lck;

        [TestMethod(30000, 1000)]
        public static Task Run()
        {
            var foo = client.GetGrain<IFooGrain>(0);
            var bar = client.GetGrain<IBarGrain>(1);

            lck = new NekaraModels.Lock(3);
            x = 0;

            nekara.CreateTask();
            var t1 = foo.Foo();

            nekara.CreateTask();
            var t2 = bar.Bar();

            //NekaraModels.Task.Run(() => Task.Delay(30000).Wait());

            return Task.WhenAll(t1, t2);
        }
    }

    public interface IFooGrain : IGrainWithIntegerKey
    {
        Task Foo();
    }

    public interface IBarGrain : IGrainWithIntegerKey
    {
        Task Bar();
    }

    public class FooGrain : Grain, IFooGrain
    {
        public Task Foo()
        {
            Console.WriteLine("Foo()\tstarted");
            Deadlock.lck.Acquire();

            Console.WriteLine("Foo()\tacquired lock");

            Deadlock.nekara.ContextSwitch();
            Console.WriteLine("Foo()\tgot control");
            int lx1 = Deadlock.x;

            Console.WriteLine("Foo()\tcopied x");

            Deadlock.nekara.ContextSwitch();
            Console.WriteLine("Foo()\tgot control");
            int lx2 = Deadlock.x;

            Console.WriteLine("Foo()\tcopied x");

            Deadlock.lck.Release();
            Console.WriteLine("Foo()\treleased lock");

            Deadlock.nekara.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo()\tending");
            return Task.CompletedTask;
        }
    }

    public class BarGrain : Grain, IBarGrain
    {
        public Task Bar()
        {
            //Deadlock.lck.Acquire();
            Console.WriteLine("Bar()\tstarted");

            Deadlock.nekara.ContextSwitch();
            Console.WriteLine("Bar()\tgot control");
            Deadlock.x = 1;

            // Deadlock.lck.Release();
            Console.WriteLine("Bar()\tending");
            return Task.CompletedTask;
        }
    }
}