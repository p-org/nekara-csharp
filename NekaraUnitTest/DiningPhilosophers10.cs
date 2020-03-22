using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class DiningPhilosophers10
    {
        public static int N = 10;
        public static int phil;

        [Fact(Timeout = 50000)]
        public void Run()
        {

            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            all.ContinueWith(prev => {
                nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            });

            all.Wait();
            nekara.Api.WaitForMainTask();
            return;
        }

        [Fact(Timeout = 50000)]
        public void RunBlocking()
        {

            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            Task.WaitAll(tasks);
            Console.WriteLine(phil);
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");

            nekara.Api.WaitForMainTask();
            return;
        }

        [Fact(Timeout = 5000000)]
        public System.Threading.Tasks.Task RunTask()
        {

            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            all.ContinueWith(prev => {
                nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            });

            all.Wait();
            nekara.Api.WaitForMainTask();
            return all.InnerTask;
        }

        [Fact(Timeout = 50000)]
        public System.Threading.Tasks.Task RunBlockingTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            Task.WaitAll(tasks);
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");

            nekara.Api.WaitForMainTask();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        [Fact(Timeout = 50000)]
        public Task RunNekaraTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            all.ContinueWith(prev => {
                nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            });

            all.Wait();
            nekara.Api.WaitForMainTask();
            return all;
        }

        [Fact(Timeout = 50000)]
        public Task RunBlockingNekaraTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            Task.WaitAll(tasks);
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");

            nekara.Api.WaitForMainTask();
            return Task.CompletedTask;
        }

        [Fact(Timeout = 50000)]
        public async void RunAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            await Task.WhenAll(tasks);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [Fact(Timeout = 50000)]
        public async void RunBlockingAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            Task.WaitAll(tasks);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [Fact(Timeout = 50000)]
        public async System.Threading.Tasks.Task RunTaskAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            await all;

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [Fact(Timeout = 50000)]
        public async System.Threading.Tasks.Task RunBlockingTaskAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            Task.WaitAll(tasks);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [Fact(Timeout = 50000)]
        public async Task RunNekaraTaskAsync()
        {

            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            await all;

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [Fact(Timeout = 50000)]
        public async Task RunBlockingNekaraTaskAsync()
        {

            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var tasks = Dine(N);
            Task.WaitAll(tasks);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }


        public Task[] Dine(int n)
        {

            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            phil = 0;

            var countLock = new Lock(0);

            Lock[] locks = new Lock[n];
            for (int i = 0; i < n; i++)
            {
                locks[i] = new Lock(1 + i);
            }

            Task[] tasks = new Task[n];
            for (int i = 0; i < n; i++)
            {
                int id = i;

                tasks[i] = Task.Run(() =>
                {
                    int left = id % n;
                    int right = (id + 1) % n;

                    nekara.Api.ContextSwitch();
                    var releaserR = locks[right].Acquire();

                    nekara.Api.ContextSwitch();
                    var releaserL = locks[left].Acquire();

                    using (countLock.Acquire())
                    {
                        phil++;
                        // Console.WriteLine("Philosopher {0} eats. Incrementing phil: {1}", id, phil);
                    }

                    nekara.Api.ContextSwitch();
                    releaserR.Dispose();

                    nekara.Api.ContextSwitch();
                    releaserL.Dispose();
                });
            }

            return tasks;
        }

    }
}
