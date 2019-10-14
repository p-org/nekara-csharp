// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class Lazy
    {
        [TestMethod]
        public static async Task RunTest(TestingServiceProxy ts)
        {
            int data = 0;

            var l = ts.LockFactory.CreateLock(1);

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);
                ts.Api.ContextSwitch();
                using (l.Acquire())
                {
                    data++;
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
                    data += 2;
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
                    ts.Api.Assert(data < 3, "Bug found!");
                }
                ts.Api.EndTask(3);
            });

            await Task.WhenAll(t1, t2, t3);
        }
    }
}
