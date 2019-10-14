// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class Reorder10
    {
        [TestMethod]
        public static async Task Run(TestingServiceProxy ts)
        {
            int numSetTasks = 9;
            int numCheckTasks = 1;

            int a = 0;
            int b = 0;

            Task[] setPool = new Task[numSetTasks];
            Task[] checkPool = new Task[numCheckTasks];

            for (int i = 0; i < numSetTasks; i++)
            {
                int ti = 1 + i;
                ts.Api.CreateTask();
                setPool[i] = Task.Run(() =>
                {
                    ts.Api.StartTask(ti);
                    ts.Api.ContextSwitch();
                    a = 1;
                    ts.Api.ContextSwitch();
                    b = -1;
                    ts.Api.EndTask(ti);
                });
            }

            for (int i = 0; i < numCheckTasks; i++)
            {
                int ti = 1 + numSetTasks + i;
                ts.Api.CreateTask();
                checkPool[i] = Task.Run(() =>
                {
                    ts.Api.StartTask(ti);
                    ts.Api.ContextSwitch();
                    int localA = a;
                    ts.Api.ContextSwitch();
                    int localB = b;
                    ts.Api.Assert((localA == 0 && localB == 0) || (localA == 1 && localB == -1), "Bug found!");
                    ts.Api.EndTask(ti);
                });
            }

            await Task.WhenAll(setPool);
            await Task.WhenAll(checkPool);
        }
    }
}