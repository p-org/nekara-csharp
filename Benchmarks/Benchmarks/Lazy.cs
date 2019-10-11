// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class Lazy
    {
        [TestMethod]
        public static async Task RunTest(ITestingService testingService)
        {
            int data = 0;

            var l = testingService.CreateLock(1);

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);
                testingService.ContextSwitch();
                using (l.Acquire())
                {
                    data++;
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
                    data += 2;
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
                    testingService.Assert(data < 3, "Bug found!");
                }
                testingService.EndTask(3);
            });

            await Task.WhenAll(t1, t2, t3);
        }
    }
}
