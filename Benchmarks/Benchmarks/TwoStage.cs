// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class TwoStage
    {
        [TestMethod]
        public static async Task Run(TestingServiceProxy ts)
        {
            int numTTasks = 3;
            int numRTasks = 3;
            int data1Value = 0;
            int data2Value = 0;

            var data1Lock = ts.LockFactory.CreateLock(1);
            var data2Lock = ts.LockFactory.CreateLock(2);

            Task[] tPool = new Task[numTTasks];
            Task[] rPool = new Task[numRTasks];

            for (int i = 0; i < numTTasks; i++)
            {
                ts.Api.CreateTask();
                int ti = 1 + i;
                tPool[i] = Task.Run(() =>
                {
                    ts.Api.StartTask(ti);
                    ts.Api.ContextSwitch();
                    using (data1Lock.Acquire())
                    {
                        data1Value = 1;
                    }

                    ts.Api.ContextSwitch();
                    using (data2Lock.Acquire())
                    {
                        data2Value = data1Value + 1;
                    }
                    ts.Api.ContextSwitch();

                    ts.Api.EndTask(ti);
                });
            }

            for (int i = 0; i < numRTasks; i++)
            {
                int ti = 1 + numTTasks + i;
                ts.Api.CreateTask();
                rPool[i] = Task.Run(() =>
                {
                    ts.Api.StartTask(ti);

                    int t1 = -1;
                    int t2 = -1;

                    ts.Api.ContextSwitch();
                    using (data1Lock.Acquire())
                    {
                        if (data1Value == 0)
                        {
                            return;
                        }
                        t1 = data1Value;
                    }

                    ts.Api.ContextSwitch();
                    using (data2Lock.Acquire())
                    {
                        t2 = data2Value;
                    }

                    ts.Api.ContextSwitch();
                    int localT1 = t1;
                    ts.Api.ContextSwitch();
                    int localT2 = t2;
                    ts.Api.Assert(localT2 == localT1 + 1, "Bug found!");

                    ts.Api.EndTask(ti);
                });
            }

            await Task.WhenAll(tPool);
            await Task.WhenAll(rPool);
        }
    }
}
