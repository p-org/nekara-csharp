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
    class DiningPhilosophers2
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(PhilosopherGrain));
            Console.WriteLine("Setup");
        }

        [TestTeardownMethod]
        public static void Teardown()
        {
            silo.StopAsync().Wait();
            Console.WriteLine("Teardown");
        }

        public static int n;
        public static int phil;

        public static Lock countLock;
        public static Lock[] locks;

        [TestMethod]
        public static async void Run()
        {
            n = 2;
            phil = 0;

            countLock = new Lock(0);
            locks = new Lock[n];

            for (int i = 0; i < n; i++)
            {
                locks[i] = new Lock(1 + i);
            }

            // IPhilosopherGrain[] philosophers = new PhilosopherGrain[n];
            Task[] tasks = new Task[n];

            for (int i = 0; i < n; i++)
            {
                var philosopher = client.GetGrain<IPhilosopherGrain>(i);
                int ci = i;
                tasks[i] = Task.Run(() => philosopher.Eat(ci).Wait());
            }

            await Task.WhenAll(tasks);
        }
    }

    public interface IPhilosopherGrain : IGrainWithIntegerKey
    {
        NativeTasks.Task Eat(int id);
    }

    public class PhilosopherGrain : Grain, IPhilosopherGrain
    {
        public NativeTasks.Task Eat(int id)
        {
            int left = id % DiningPhilosophers2.n;
            int right = (id + 1) % DiningPhilosophers2.n;

            DiningPhilosophers2.nekara.ContextSwitch();
            var releaserR = DiningPhilosophers2.locks[right].Acquire();

            DiningPhilosophers2.nekara.ContextSwitch();
            var releaserL = DiningPhilosophers2.locks[left].Acquire();

            DiningPhilosophers2.nekara.ContextSwitch();
            releaserL.Dispose();

            DiningPhilosophers2.nekara.ContextSwitch();
            releaserR.Dispose();

            using (DiningPhilosophers2.countLock.Acquire())
            {
                ++DiningPhilosophers2.phil;
                DiningPhilosophers2.nekara.Assert(DiningPhilosophers2.phil != DiningPhilosophers2.n, "Bug found!");
            }

            return NativeTasks.Task.CompletedTask;
        }
    }
}
