// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class Account
    {
        [TestMethod]
        public static async Task RunTest(ITestingService testingService)
        {
            int x = 1;
            int y = 2;
            int z = 4;
            int balance = x;

            bool depositDone = false;
            bool withdrawDone = false;

            var l = testingService.CreateLock(1);

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);
                testingService.ContextSwitch();
                using (l.Acquire())
                {
                    if (depositDone && withdrawDone)
                    {
                        testingService.Assert(balance == (x - y) - z, "Bug found!");
                    }
                }
                testingService.EndTask(1);
            });

            testingService.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                testingService.StartTask(2);
                testingService.ContextSwitch();
                using (l.Acquire())
                {
                    balance += y;
                    depositDone = true;
                }
                testingService.EndTask(2);
            });

            testingService.CreateTask();
            Task t3 = Task.Run(async () =>
            {
                testingService.StartTask(3);
                testingService.ContextSwitch();
                using (l.Acquire())
                {
                    balance -= z;
                    withdrawDone = true;
                }
                testingService.EndTask(3);
            });

            await Task.WhenAll(t1, t2, t3);

            return;
        }
    }
}
