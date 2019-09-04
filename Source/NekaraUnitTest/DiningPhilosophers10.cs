using System;
using Xunit;
using NekaraManaged.Client;
// using System.Threading.Tasks;
using System.Threading;
using Nekara.Models;
using System.Collections.Generic;

namespace NekaraUnitTest
{
    public class DiningPhilosophers
    {
        public static int N = 2;
        public static int phil;
        public static NekaraManagedClient nekara = RuntimeEnvironment.Client;
        public static bool bugfound = false;
        public static int[] temp1 = new int[N];
        public static HashSet<int> blkresourceID = new HashSet<int>();

        [Fact(Timeout = 500000)]
        public void Run10()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                N = 10;

                nekara.Api.CreateSession();
                temp1 = new int[N];
                blkresourceID = new HashSet<int>();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
            }
            return;
        }

        [Fact(Timeout = 500000)]
        public void Run2()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                N = 2;

                nekara.Api.CreateSession();
                temp1 = new int[N];
                blkresourceID = new HashSet<int>();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
            }
            return;
        }

        [Fact(Timeout = 500000)]
        public void Run3()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                N = 3;

                nekara.Api.CreateSession();
                temp1 = new int[N];
                blkresourceID = new HashSet<int>();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
            }
            return;
        }

        [Fact(Timeout = 500000)]
        public void Run4()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                N = 4;

                nekara.Api.CreateSession();
                temp1 = new int[N];
                blkresourceID = new HashSet<int>();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
            }
            return;
        }

        [Fact(Timeout = 500000)]
        public void Run5()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                N = 5;

                nekara.Api.CreateSession();
                temp1 = new int[N];
                blkresourceID = new HashSet<int>();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
            }
            return;
        }

        [Fact(Timeout = 500000)]
        public void Run6()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                N = 6;

                nekara.Api.CreateSession();
                temp1 = new int[N];
                blkresourceID = new HashSet<int>();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
            }
            return;
        }

        [Fact(Timeout = 500000)]
        public void Run7()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                N = 7;

                nekara.Api.CreateSession();
                temp1 = new int[N];
                blkresourceID = new HashSet<int>();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
            }
            return;
        }

        [Fact(Timeout = 50000)]
        public void RunBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                nekara.Api.CreateSession();

                var tasks = Dine(N);
                Task.WaitAll(tasks);
                Console.WriteLine(phil);
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                Assert.True(phil == N);
            }
            return;
        }

        [Fact(Timeout = 5000000)]
        public System.Threading.Tasks.Task RunTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                nekara.Api.CreateSession();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                    Assert.True(phil == N);
                });

                all.Wait();

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                if (bugfound)
                {
                    return all.InnerTask;
                }
            }

            return new Task().InnerTask;
        }

        [Fact(Timeout = 50000)]
        public System.Threading.Tasks.Task RunBlockingTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (bugfound)
            {
                nekara.Api.CreateSession();

                var tasks = Dine(N);
                Task.WaitAll(tasks);
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                Assert.True(phil == N);
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }

        [Fact(Timeout = 50000)]
        public Task RunNekaraTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                nekara.Api.CreateSession();

                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                all.ContinueWith(prev => {
                    nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                });

                all.Wait();
                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();

                if (bugfound)
                {
                    return all;
                }
            }
            return new Task();
        }

        [Fact(Timeout = 50000)]
        public Task RunBlockingNekaraTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                nekara.Api.CreateSession();

                var tasks = Dine(N);
                Task.WaitAll(tasks);
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                Assert.True(phil == N);
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        [Fact(Timeout = 50000)]
        public async void RunAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                var tasks = Dine(N);
                await Task.WhenAll(tasks);

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                Assert.True(phil == N);
            }
            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 50000)]
        public async void RunBlockingAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {

                var tasks = Dine(N);
                Task.WaitAll(tasks);

                await Task.Delay(1);

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                Assert.True(phil == N);
            }

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 50000)]
        public async System.Threading.Tasks.Task RunTaskAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                await all;

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();

                await Task.Delay(1);

                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                Assert.True(phil == N);
            }

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 50000)]
        public async System.Threading.Tasks.Task RunBlockingTaskAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                var tasks = Dine(N);
                Task.WaitAll(tasks);

                await Task.Delay(1);

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                Assert.True(phil == N);
            }

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 50000)]
        public async Task RunNekaraTaskAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                var tasks = Dine(N);
                var all = Task.WhenAll(tasks);
                await all;

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                Assert.True(phil == N);
            }

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 50000)]
        public async Task RunBlockingNekaraTaskAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            bugfound = false;

            while (!bugfound)
            {
                var tasks = Dine(N);
                Task.WaitAll(tasks);

                await Task.Delay(1);

                System.Threading.Tasks.Task.Run(() => nekara.Api.WaitForMainTask()).Wait();
                // nekara.Api.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
                Assert.True(phil == N);
            }

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }


        public Task[] Dine(int n)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            phil = 0;

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
                    var releaserR = locks[right].Acquire(id);

                    temp1[id] = 0;

                    nekara.Api.ContextSwitch();
                    var releaserL = locks[left].Acquire(id);

                    temp1[id] = 0;

                    phil++;

                    nekara.Api.ContextSwitch();
                    releaserR.Dispose();

                    nekara.Api.ContextSwitch();
                    releaserL.Dispose();

                    temp1[id] = 2;

                });
            }

            return tasks;
        }


        internal class Lock // : ILock
        {

            internal class Releaser // : IDisposable
            {
                private Lock lck;

                public Releaser(Lock lck)
                {
                    this.lck = lck;
                }

                public void Dispose()
                {
                    this.lck.Release();
                }
            }

            private int id;
            private bool locked;

            internal Lock(int resourceId, string label = "")
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;

                this.id = resourceId;
                this.locked = false;

                nekara.Api.CreateResource(resourceId);
            }

            public Releaser Acquire(int id)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;

                nekara.Api.ContextSwitch();
                while (true)
                {
                    if (this.locked == false)
                    {
                        this.locked = true;
                        break;
                    }
                    else
                    {
                        int blocked_tasks = 0;
                        int live_tasks = 0;
                        for (int i = 0; i < N; i++)
                        {
                            if (temp1[i] == 0)
                            {
                                live_tasks++;
                            }
                            else if (temp1[i] == 1)
                            {
                                blocked_tasks++;
                            }
                        }

                        if (blocked_tasks > 0 && live_tasks == 1)
                        {
                            bugfound = true;
                            foreach (int j in blkresourceID)
                            {
                                nekara.Api.SignalUpdatedResource(j);
                            }
                            break;
                        }
                        else
                        {
                            temp1[id] = 1;
                        }

                        if (bugfound)
                        {
                            break;
                        }

                        blkresourceID.Add(this.id);
                        nekara.Api.BlockedOnResource(this.id);
                        continue;
                    }
                }
                return new Releaser(this);
            }

            public void Release()
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;

                if (!bugfound)
                {
                    nekara.Api.Assert(this.locked == true, "Release called on non-acquired lock");

                    this.locked = false;

                    blkresourceID.Remove(this.id);

                    nekara.Api.SignalUpdatedResource(this.id);
                }
            }
        }
    }
}
