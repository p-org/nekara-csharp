// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class Reorder20
    {
        [TestMethod]
        public static async Task Run(ITestingService testingService)
        {
            int numSetTasks = 19;
            int numCheckTasks = 1;

            int a = 0;
            int b = 0;

            Task[] setPool = new Task[numSetTasks];
            Task[] checkPool = new Task[numCheckTasks];

            for (int i = 0; i < numSetTasks; i++)
            {
                int ti = 1 + i;
                testingService.CreateTask();
                setPool[i] = Task.Run(() =>
                {
                    testingService.StartTask(ti);
                    testingService.ContextSwitch();
                    a = 1;
                    testingService.ContextSwitch();
                    b = -1;
                    testingService.EndTask(ti);
                });
            }

            for (int i = 0; i < numCheckTasks; i++)
            {
                int ti = 1 + numSetTasks + i;
                testingService.CreateTask();
                checkPool[i] = Task.Run(() =>
                {
                    testingService.StartTask(ti);
                    testingService.ContextSwitch();
                    int localA = a;
                    testingService.ContextSwitch();
                    int localB = b;
                    testingService.Assert((localA == 0 && localB == 0) || (localA == 1 && localB == -1), "Bug found!");
                    testingService.EndTask(ti);
                });
            }

            await Task.WhenAll(setPool);
            await Task.WhenAll(checkPool);
        }
    }
}