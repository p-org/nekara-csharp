// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class Carter
    {
        [TestMethod]
        public static async Task RunTest(ITestingService testingService)
        {
            int a = 0;
            int b = 0;

            var l1 = testingService.CreateLock(1);
            var l2 = testingService.CreateLock(2);

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);
                IDisposable releaser2 = null;

                testingService.ContextSwitch();
                using (l1.Acquire())
                {
                    a++;
                    if (a == 1)
                    {
                        releaser2 = l2.Acquire();
                    }
                }

                testingService.ContextSwitch();
                using (l1.Acquire())
                {
                    a--;
                    if (a == 0)
                    {
                        releaser2.Dispose();
                    }
                }
                testingService.EndTask(1);
            });

            testingService.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                testingService.StartTask(2);
                IDisposable releaser2 = null;

                testingService.ContextSwitch();
                using (l1.Acquire())
                {
                    b++;
                    if (b == 1)
                    {
                        releaser2 = l2.Acquire();
                    }
                }

                testingService.ContextSwitch();
                using (l1.Acquire())
                {
                    b--;
                    if (b == 0)
                    {
                        releaser2.Dispose();
                    }
                }
                testingService.EndTask(2);
            });

            testingService.CreateTask();
            Task t3 = Task.Run(() =>
            {
                testingService.StartTask(3);
                testingService.EndTask(3);
            });

            testingService.CreateTask();
            Task t4 = Task.Run(() =>
            {
                testingService.StartTask(4);
                testingService.EndTask(4);
            });

            await Task.WhenAll(t1, t2, t3, t4);
        }
    }
}