// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    class DeadlockAsync
    {
        [TestMethod]
        public static async Task Execute(ITestingService testingService)
        {
            Console.WriteLine("  Starting Deadlock Benchmark ...");

            int counter = 1;

            AsyncLock a = AsyncLock.Create("A");
            AsyncLock b = AsyncLock.Create("B");

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);

                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserA = await a.AcquireAsync("X");
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserB = await b.AcquireAsync("X"); // Deadlock
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter++;
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserB.Dispose("X");
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserA.Dispose("X");

                testingService.EndTask(1);
            });

            testingService.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                testingService.StartTask(2);

                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserB = await b.AcquireAsync("Y");
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserA = await a.AcquireAsync("Y"); // Deadlock
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter--;
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserA.Dispose("Y");
                testingService.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserB.Dispose("Y");

                testingService.EndTask(2);
            });

            await Task.WhenAll(t1, t2);

            Console.WriteLine("  ... Finished Deadlock Benchmark");
        }
    }
}
