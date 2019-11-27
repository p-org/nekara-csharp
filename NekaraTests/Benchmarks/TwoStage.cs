// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Models;

namespace Nekara.Tests.Benchmarks
{
    public class TwoStage
    {
        [TestMethod]
        public static void Run()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int numTTasks = 1;
            int numRTasks = 1;
            int data1Value = 0;
            int data2Value = 0;

            var data1Lock = new Lock(1);
            var data2Lock = new Lock(2);

            Task[] tPool = new Task[numTTasks];
            Task[] rPool = new Task[numRTasks];

            for (int i = 0; i < numTTasks; i++)
            {
                tPool[i] = Task.Run(() =>
                {
                    nekara.ContextSwitch();
                    using (data1Lock.Acquire())
                    {
                        data1Value = 1;
                    }

                    nekara.ContextSwitch();
                    using (data2Lock.Acquire())
                    {
                        data2Value = data1Value + 1;
                    }

                    nekara.ContextSwitch();
                });
            }

            for (int i = 0; i < numRTasks; i++)
            {
                rPool[i] = Task.Run(() =>
                {
                    int t1 = -1;
                    int t2 = -1;

                    nekara.ContextSwitch();
                    using (data1Lock.Acquire())
                    {
                        if (data1Value == 0)
                        {
                            return;
                        }
                        t1 = data1Value;
                    }

                    nekara.ContextSwitch();
                    using (data2Lock.Acquire())
                    {
                        t2 = data2Value;
                    }

                    nekara.ContextSwitch();
                    int localT1 = t1;

                    nekara.ContextSwitch();
                    int localT2 = t2;

                    nekara.Assert(localT2 == localT1 + 1, "Bug found!");
                });
            }

            Task.WaitAll(tPool);
            Task.WaitAll(rPool);
        }
    }
}
