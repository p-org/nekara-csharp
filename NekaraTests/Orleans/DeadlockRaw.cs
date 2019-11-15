using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using NekaraModels = Nekara.Models;
using Orleans;
using Orleans.Hosting;

namespace Nekara.Tests.Orleans
{
    class DeadlockRaw
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(RawFooGrain), typeof(RawBarGrain));
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
        public async static Task Run()
        {
            // Setup();

            var foo = client.GetGrain<IRawFooGrain>(0);
            var bar = client.GetGrain<IRawBarGrain>(1);

            lck = new NekaraModels.Lock(3);
            x = 0;

            var t1 = NekaraModels.Task.Run(() => foo.Foo().Wait());

            var t2 = NekaraModels.Task.Run(() => bar.Bar().Wait());

            await NekaraModels.Task.WhenAll(t1, t2);

            // Teardown();
        }
    }

    public interface IRawFooGrain : IGrainWithIntegerKey
    {
        Task Foo();
    }

    public interface IRawBarGrain : IGrainWithIntegerKey
    {
        Task Bar();
    }

    public class RawFooGrain : Grain, IRawFooGrain
    {
        public Task Foo()
        {
            Console.WriteLine("Foo()\tstarted");
            DeadlockRaw.lck.Acquire();

            Console.WriteLine("Foo()\tacquired lock");

            DeadlockRaw.nekara.ContextSwitch();
            Console.WriteLine("Foo()\tgot control");
            int lx1 = DeadlockRaw.x;

            Console.WriteLine("Foo()\tcopied x");

            DeadlockRaw.nekara.ContextSwitch();
            Console.WriteLine("Foo()\tgot control");
            int lx2 = DeadlockRaw.x;

            Console.WriteLine("Foo()\tcopied x");

            DeadlockRaw.lck.Release();
            Console.WriteLine("Foo()\treleased lock");

            DeadlockRaw.nekara.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo()\tending");
            return Task.CompletedTask;
        }
    }

    public class RawBarGrain : Grain, IRawBarGrain
    {
        public Task Bar()
        {
            //DeadlockRaw.lck.Acquire();
            Console.WriteLine("Bar()\tstarted");

            DeadlockRaw.nekara.ContextSwitch();
            Console.WriteLine("Bar()\tgot control");
            DeadlockRaw.x = 1;

            // DeadlockRaw.lck.Release();
            Console.WriteLine("Bar()\tending");
            return Task.CompletedTask;
        }
    }
}