// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    class Deadlock3
    {
        [TestMethod]
        public static async Task Execute(ITestingService testingService)
        {
            Console.WriteLine("  Starting Deadlock Benchmark ...");

            int counter = 1;

            var a = testingService.CreateLock(1);
            var b = testingService.CreateLock(2);

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);

                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Acquire();
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Acquire(); // Deadlock
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter++;
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Release();
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Release();

                testingService.EndTask(1);
            });

            testingService.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                testingService.StartTask(2);

                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Acquire();
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Acquire(); // Deadlock
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter--;
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                a.Release();
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                b.Release();

                testingService.EndTask(2);
            });

            await Task.WhenAll(t1, t2);

            Console.WriteLine("  ... Finished Deadlock Benchmark");
        }
    }
}