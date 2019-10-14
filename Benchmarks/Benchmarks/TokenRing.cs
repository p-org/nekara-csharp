// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class TokenRing
    {
        [TestMethod]
        public static async Task RunTest(TestingServiceProxy ts)
        {
            int x1 = 1;
            int x2 = 2;
            int x3 = 1;

            bool flag1 = false;
            bool flag2 = false;
            bool flag3 = false;

            var l = ts.LockFactory.CreateLock(1);

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);
                ts.Api.ContextSwitch();
                using (l.Acquire())
                {
                    ts.Api.ContextSwitch();
                    x1 = (x3 + 1) % 4;
                    ts.Api.ContextSwitch();
                    flag1 = true;
                }
                ts.Api.EndTask(1);
            });

            ts.Api.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                ts.Api.StartTask(2);
                ts.Api.ContextSwitch();
                using (l.Acquire())
                {
                    ts.Api.ContextSwitch();
                    x2 = x1;
                    ts.Api.ContextSwitch();
                    flag2 = true;
                }
                ts.Api.EndTask(2);
            });

            ts.Api.CreateTask();
            Task t3 = Task.Run(async () =>
            {
                ts.Api.StartTask(3);
                ts.Api.ContextSwitch();
                using (l.Acquire())
                {
                    ts.Api.ContextSwitch();
                    x3 = x2;
                    ts.Api.ContextSwitch();
                    flag3 = true;
                }
                ts.Api.EndTask(3);
            });

            ts.Api.CreateTask();
            Task t4 = Task.Run(async () =>
            {
                ts.Api.StartTask(4);
                ts.Api.ContextSwitch();
                using (l.Acquire())
                {
                    ts.Api.ContextSwitch();
                    if (flag1 && flag2 && flag3)
                    {
                        ts.Api.ContextSwitch();
                        ts.Api.Assert(x1 == x2 && x2 == x3, "Bug found!");
                    }
                }
                ts.Api.EndTask(4);
            });

            await Task.WhenAll(t1, t2, t3, t4);
        }
    }
}
