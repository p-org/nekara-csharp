// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Models;

namespace Benchmarks
{
    class Deadlock3
    {
        [TestMethod]
        public static async void Execute()
        {
            Console.WriteLine("  Starting Deadlock Benchmark ...");
            var nekara = RuntimeEnvironment.Client.Api;

            int counter = 1;

            var a = new Lock(1);
            var b = new Lock(2);

            Task t1 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                a.Acquire();

                nekara.ContextSwitch();
                b.Acquire(); // Deadlock

                nekara.ContextSwitch();
                counter++;

                nekara.ContextSwitch();
                b.Release();

                nekara.ContextSwitch();
                a.Release();
            });

            Task t2 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                b.Acquire();

                nekara.ContextSwitch();
                a.Acquire(); // Deadlock

                nekara.ContextSwitch();
                counter--;

                nekara.ContextSwitch();
                a.Release();

                nekara.ContextSwitch();
                b.Release();
            });

            await Task.WhenAll(t1, t2);

            Console.WriteLine("  ... Finished Deadlock Benchmark");
        }
    }
}