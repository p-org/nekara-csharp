// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    class Deadlock3
    {
        [TestMethod]
        public static async Task Execute(TestingServiceProxy ts)
        {
            Console.WriteLine("  Starting Deadlock Benchmark ...");

            int counter = 1;

            var a = ts.LockFactory.CreateLock(1);
            var b = ts.LockFactory.CreateLock(2);

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);

                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Acquire();
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Acquire(); // Deadlock
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter++;
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Release();
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Release();

                ts.Api.EndTask(1);
            });

            ts.Api.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                ts.Api.StartTask(2);

                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Acquire();
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Acquire(); // Deadlock
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter--;
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Release();
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Release();

                ts.Api.EndTask(2);
            });

            await Task.WhenAll(t1, t2);

            Console.WriteLine("  ... Finished Deadlock Benchmark");
        }
    }
}