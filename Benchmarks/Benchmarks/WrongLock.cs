// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class WrongLock
    {
        [TestMethod]
        public static async Task Run(TestingServiceProxy ts)
        {
            int iNum1 = 1;
            int iNum2 = 7;
            int dataValue = 0;

            var dataLock = ts.LockFactory.CreateLock(1);
            var thisLock = ts.LockFactory.CreateLock(2);

            Task[] num1Pool = new Task[iNum1];
            Task[] num2Pool = new Task[iNum2];

            for (int i = 0; i < iNum1; i++)
            {
                ts.Api.CreateTask();
                num1Pool[i] = Task.Run(async () =>
                {
                    int ti = i; // need to capture the loop variable to assign the correct task id
                    Console.WriteLine("Starting Task {0}", 1 + ti);
                    ts.Api.StartTask(1 + ti);
                    ts.Api.ContextSwitch();
                    using (dataLock.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        int x = dataValue;
                        ts.Api.ContextSwitch();
                        dataValue++;
                        ts.Api.ContextSwitch();
                        ts.Api.Assert(dataValue == (x + 1), "Bug Found!");
                    }
                    Console.WriteLine("Ending Task {0}", 1 + ti);
                    ts.Api.EndTask(1 + ti);
                });
            }

            for (int i = 0; i < iNum2; i++)
            {
                ts.Api.CreateTask();
                num2Pool[i] = Task.Run(async () =>
                {
                    int ti = i; // need to capture the loop variable to assign the correct task id
                    Console.WriteLine("Starting Task {0}", 1 + iNum1 + ti);
                    ts.Api.StartTask(1 + iNum1 + ti);
                    ts.Api.ContextSwitch();
                    using (thisLock.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        dataValue++;
                    }
                    Console.WriteLine("Ending Task {0}", 1 + iNum1 + ti);
                    ts.Api.EndTask(1 + iNum1 + ti);
                });
            }

            await Task.WhenAll(num1Pool);
            await Task.WhenAll(num2Pool);
        }
    }
}
