// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class Carter
    {
        [TestMethod]
        public static async Task RunTest(TestingServiceProxy ts)
        {
            int a = 0;
            int b = 0;

            var l1 = ts.LockFactory.CreateLock(1);
            var l2 = ts.LockFactory.CreateLock(2);

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);
                IDisposable releaser2 = null;

                ts.Api.ContextSwitch();
                using (l1.Acquire())
                {
                    a++;
                    if (a == 1)
                    {
                        releaser2 = l2.Acquire();
                    }
                }

                ts.Api.ContextSwitch();
                using (l1.Acquire())
                {
                    a--;
                    if (a == 0)
                    {
                        releaser2.Dispose();
                    }
                }
                ts.Api.EndTask(1);
            });

            ts.Api.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                ts.Api.StartTask(2);
                IDisposable releaser2 = null;

                ts.Api.ContextSwitch();
                using (l1.Acquire())
                {
                    b++;
                    if (b == 1)
                    {
                        releaser2 = l2.Acquire();
                    }
                }

                ts.Api.ContextSwitch();
                using (l1.Acquire())
                {
                    b--;
                    if (b == 0)
                    {
                        releaser2.Dispose();
                    }
                }
                ts.Api.EndTask(2);
            });

            ts.Api.CreateTask();
            Task t3 = Task.Run(() =>
            {
                ts.Api.StartTask(3);
                ts.Api.EndTask(3);
            });

            ts.Api.CreateTask();
            Task t4 = Task.Run(() =>
            {
                ts.Api.StartTask(4);
                ts.Api.EndTask(4);
            });

            await Task.WhenAll(t1, t2, t3, t4);
        }
    }
}