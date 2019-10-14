// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class DiningPhilosophers3
    {
        [TestMethod]
        public static async Task Run(TestingServiceProxy ts)
        {
            int n = 3;
            int phil = 0;

            var countLock = ts.LockFactory.CreateLock(0);

            IAsyncLock[] locks = new TestRuntimeLock[n];
            for (int i = 0; i < n; i++)
            {
                locks[i] = ts.LockFactory.CreateLock(1 + i);
            }

            Task[] tasks = new Task[n];
            for (int i = 0; i < n; i++)
            {
                int ti = 1 + i;
                ts.Api.CreateTask();
                tasks[i] = Task.Run(() =>
                {
                    ts.Api.StartTask(ti);

                    int id = i;
                    int left = id % n;
                    int right = (id + 1) % n;

                    ts.Api.ContextSwitch();
                    var releaserR = locks[right].Acquire();
                    ts.Api.ContextSwitch();
                    var releaserL = locks[left].Acquire();
                    ts.Api.ContextSwitch();
                    releaserL.Dispose();
                    ts.Api.ContextSwitch();
                    releaserR.Dispose();

                    using (countLock.Acquire())
                    {
                        ++phil;
                        ts.Api.Assert(phil != n, "Bug found!");
                    }

                    ts.Api.EndTask(ti);
                });
            }

            await Task.WhenAll(tasks);
        }
    }
}