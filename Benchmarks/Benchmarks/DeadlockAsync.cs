// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    class DeadlockAsync
    {
        [TestMethod]
        public static async Task Execute(TestingServiceProxy ts)
        {
            Console.WriteLine("  Starting Deadlock Benchmark ...");

            int counter = 1;

            AsyncLock a = AsyncLock.Create("A");
            AsyncLock b = AsyncLock.Create("B");

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);

                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserA = await a.AcquireAsync("X");
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserB = await b.AcquireAsync("X"); // Deadlock
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter++;
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserB.Dispose("X");
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserA.Dispose("X");

                ts.Api.EndTask(1);
            });

            ts.Api.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                ts.Api.StartTask(2);

                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserB = await b.AcquireAsync("Y");
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                var releaserA = await a.AcquireAsync("Y"); // Deadlock
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                counter--;
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserA.Dispose("Y");
                ts.Api.ContextSwitch();
                // Specification.InjectContextSwitch();
                releaserB.Dispose("Y");

                ts.Api.EndTask(2);
            });

            await Task.WhenAll(t1, t2);

            Console.WriteLine("  ... Finished Deadlock Benchmark");
        }
    }
}
