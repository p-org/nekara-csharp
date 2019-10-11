// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class TokenRing
    {
        [TestMethod]
        public static async Task RunTest(ITestingService testingService)
        {
            int x1 = 1;
            int x2 = 2;
            int x3 = 1;

            bool flag1 = false;
            bool flag2 = false;
            bool flag3 = false;

            var l = testingService.CreateLock(1);

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);
                testingService.ContextSwitch();
                using (l.Acquire())
                {
                    testingService.ContextSwitch();
                    x1 = (x3 + 1) % 4;
                    testingService.ContextSwitch();
                    flag1 = true;
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
                    testingService.ContextSwitch();
                    x2 = x1;
                    testingService.ContextSwitch();
                    flag2 = true;
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
                    testingService.ContextSwitch();
                    x3 = x2;
                    testingService.ContextSwitch();
                    flag3 = true;
                }
                testingService.EndTask(3);
            });

            testingService.CreateTask();
            Task t4 = Task.Run(async () =>
            {
                testingService.StartTask(4);
                testingService.ContextSwitch();
                using (l.Acquire())
                {
                    testingService.ContextSwitch();
                    if (flag1 && flag2 && flag3)
                    {
                        testingService.ContextSwitch();
                        testingService.Assert(x1 == x2 && x2 == x3, "Bug found!");
                    }
                }
                testingService.EndTask(4);
            });

            await Task.WhenAll(t1, t2, t3, t4);
        }
    }
}
