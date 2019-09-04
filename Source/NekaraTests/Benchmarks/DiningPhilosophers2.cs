// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Core;
using Nekara.Models;
using System;

namespace Nekara.Tests.Benchmarks
{
    public class DiningPhilosophers2
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;

        public static int N = 2;
        public static int phil;

        [TestMethod]
        public static void Run()
        {
            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            all.ContinueWith(prev => {
                nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            });
            return;
        }

        [TestMethod]
        public static void RunBlocking()
        {
            var tasks = Dine(N);
            Task.WaitAll(tasks);
            Console.WriteLine(phil);
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [TestMethod]
        public static System.Threading.Tasks.Task RunTask()
        {
            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            all.ContinueWith(prev => {
                nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            });
            return all.InnerTask;
        }

        [TestMethod]
        public static System.Threading.Tasks.Task RunBlockingTask()
        {
            var tasks = Dine(N);
            Task.WaitAll(tasks);
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        [TestMethod]
        public static Task RunNekaraTask()
        {
            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            all.ContinueWith(prev => {
                nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            });
            return all;
        }

        [TestMethod]
        public static Task RunBlockingNekaraTask()
        {
            var tasks = Dine(N);
            Task.WaitAll(tasks);
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return Task.CompletedTask;
        }

        [TestMethod]
        public async static void RunAsync()
        {
            var tasks = Dine(N);
            await Task.WhenAll(tasks);
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [TestMethod]
        public async static void RunBlockingAsync()
        {
            var tasks = Dine(N);
            Task.WaitAll(tasks);
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [TestMethod]
        public async static System.Threading.Tasks.Task RunTaskAsync()
        {
            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            await all;
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [TestMethod]
        public async static System.Threading.Tasks.Task RunBlockingTaskAsync()
        {
            var tasks = Dine(N);
            Task.WaitAll(tasks);
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [TestMethod]
        public async static Task RunNekaraTaskAsync()
        {
            var tasks = Dine(N);
            var all = Task.WhenAll(tasks);
            await all;
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }

        [TestMethod]
        public async static Task RunBlockingNekaraTaskAsync()
        {
            var tasks = Dine(N);
            Task.WaitAll(tasks);
            nekara.Assert(phil == N, $"Bug found! Only {phil} philosophers ate");
            return;
        }


        public static Task[] Dine(int n)
        {
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

                    nekara.ContextSwitch();
                    var releaserR = locks[right].Acquire();

                    nekara.ContextSwitch();
                    var releaserL = locks[left].Acquire();

                    using (countLock.Acquire())
                    {
                        phil++;
                        // Console.WriteLine("Philosopher {0} eats. Incrementing phil: {1}", id, phil);
                    }

                    nekara.ContextSwitch();
                    releaserR.Dispose();

                    nekara.ContextSwitch();
                    releaserL.Dispose();
                });
            }

            return tasks;
        }
    }
}