// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class DiningPhilosophers7
    {
        [TestMethod]
        public static async Task Run(ITestingService testingService)
        {
            int n = 7;
            int phil = 0;

            var countLock = testingService.CreateLock(0);

            IAsyncLock[] locks = new TestRuntimeLock[n];
            for (int i = 0; i < n; i++)
            {
                locks[i] = testingService.CreateLock(1 + i);
            }

            Task[] tasks = new Task[n];
            for (int i = 0; i < n; i++)
            {
                int ti = 1 + i;
                testingService.CreateTask();
                tasks[i] = Task.Run(() =>
                {
                    testingService.StartTask(ti);

                    int id = i;
                    int left = id % n;
                    int right = (id + 1) % n;

                    testingService.ContextSwitch();
                    var releaserR = locks[right].Acquire();
                    testingService.ContextSwitch();
                    var releaserL = locks[left].Acquire();
                    testingService.ContextSwitch();
                    releaserL.Dispose();
                    testingService.ContextSwitch();
                    releaserR.Dispose();

                    using (countLock.Acquire())
                    {
                        ++phil;
                        testingService.Assert(phil != n, "Bug found!");
                    }

                    testingService.EndTask(ti);
                });
            }

            await Task.WhenAll(tasks);
        }
    }
}