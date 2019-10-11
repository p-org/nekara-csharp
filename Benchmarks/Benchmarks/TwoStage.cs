// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class TwoStage
    {
        [TestMethod]
        public static async Task Run(ITestingService testingService)
        {
            int numTTasks = 3;
            int numRTasks = 3;
            int data1Value = 0;
            int data2Value = 0;

            var data1Lock = testingService.CreateLock(1);
            var data2Lock = testingService.CreateLock(2);

            Task[] tPool = new Task[numTTasks];
            Task[] rPool = new Task[numRTasks];

            for (int i = 0; i < numTTasks; i++)
            {
                testingService.CreateTask();
                int ti = 1 + i;
                tPool[i] = Task.Run(() =>
                {
                    testingService.StartTask(ti);
                    testingService.ContextSwitch();
                    using (data1Lock.Acquire())
                    {
                        data1Value = 1;
                    }

                    testingService.ContextSwitch();
                    using (data2Lock.Acquire())
                    {
                        data2Value = data1Value + 1;
                    }
                    testingService.ContextSwitch();

                    testingService.EndTask(ti);
                });
            }

            for (int i = 0; i < numRTasks; i++)
            {
                int ti = 1 + numTTasks + i;
                testingService.CreateTask();
                rPool[i] = Task.Run(() =>
                {
                    testingService.StartTask(ti);

                    int t1 = -1;
                    int t2 = -1;

                    testingService.ContextSwitch();
                    using (data1Lock.Acquire())
                    {
                        if (data1Value == 0)
                        {
                            return;
                        }
                        t1 = data1Value;
                    }

                    testingService.ContextSwitch();
                    using (data2Lock.Acquire())
                    {
                        t2 = data2Value;
                    }

                    testingService.ContextSwitch();
                    int localT1 = t1;
                    testingService.ContextSwitch();
                    int localT2 = t2;
                    testingService.Assert(localT2 == localT1 + 1, "Bug found!");

                    testingService.EndTask(ti);
                });
            }

            await Task.WhenAll(tPool);
            await Task.WhenAll(rPool);
        }
    }
}
