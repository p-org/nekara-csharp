using System;
using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using Nekara.Models;
using Nekara.Orleans;
using Orleans;
using Orleans.Hosting;

namespace Nekara.Tests.Orleans
{
    class DeadlockGrain
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(Foo2Grain), typeof(Bar2Grain));
            Console.WriteLine("Setup");
        }

        [TestTeardownMethod]
        public static void Teardown()
        {
            silo.StopAsync().Wait();
            Console.WriteLine("Teardown");
        }

        public static int x;
        public static ILockGrain lck;

        [TestMethod]
        public async static NativeTasks.Task Run()
        {
            // Setup();
            x = 0;
            lck = client.GetGrain<ILockGrain>(3);

            var foo = client.GetGrain<IFoo2Grain>(0);
            var bar = client.GetGrain<IBar2Grain>(1);

            var t1 = Task.Run(() => foo.Foo().Wait());

            var t2 = Task.Run(() => bar.Bar().Wait());

            await Task.WhenAll(t1, t2);

            // Teardown();
        }

        public interface IFoo2Grain : IGrainWithIntegerKey
        {
            NativeTasks.Task Foo();
        }

        public interface IBar2Grain : IGrainWithIntegerKey
        {
            NativeTasks.Task Bar();
        }

        public class Foo2Grain : Grain, IFoo2Grain
        {
            public NativeTasks.Task Foo()
            {
                lck.Acquire();

                nekara.ContextSwitch();
                int lx1 = x;

                nekara.ContextSwitch();
                int lx2 = x;

                lck.Release();

                nekara.Assert(lx1 == lx2, "Race!");

                return NativeTasks.Task.CompletedTask;
            }
        }

        public class Bar2Grain : Grain, IBar2Grain
        {
            public NativeTasks.Task Bar()
            {
                lck = client.GetGrain<ILockGrain>(3);
                //lck.Acquire();

                nekara.ContextSwitch();
                x = 1;

                // lck.Release();

                return NativeTasks.Task.CompletedTask;
            }
        }
    }
}
