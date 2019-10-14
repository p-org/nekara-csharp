// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class Account
    {
        [TestMethod]
        public static async Task RunTest(TestingServiceProxy ts)
        {
            int x = 1;
            int y = 2;
            int z = 4;
            int balance = x;

            bool depositDone = false;
            bool withdrawDone = false;

            var l = ts.LockFactory.CreateLock(1);

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);
                ts.Api.ContextSwitch();
                using (l.Acquire())
                {
                    if (depositDone && withdrawDone)
                    {
                        ts.Api.Assert(balance == (x - y) - z, "Bug found!");
                    }
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
                    balance += y;
                    depositDone = true;
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
                    balance -= z;
                    withdrawDone = true;
                }
                ts.Api.EndTask(3);
            });

            await Task.WhenAll(t1, t2, t3);

            return;
        }
    }
}
