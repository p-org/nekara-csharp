// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using Nekara.Client;
using Nekara.Models;

namespace Benchmarks
{
    public class Reorder4
    {
        [TestMethod]
        public static async void Run()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int numSetTasks = 3;
            int numCheckTasks = 1;

            int a = 0;
            int b = 0;

            Task[] setPool = new Task[numSetTasks];
            Task[] checkPool = new Task[numCheckTasks];

            for (int i = 0; i < numSetTasks; i++)
            {
                setPool[i] = Task.Run(() =>
                {
                    nekara.ContextSwitch();
                    a = 1;

                    nekara.ContextSwitch();
                    b = -1;
                });
            }

            for (int i = 0; i < numCheckTasks; i++)
            {
                checkPool[i] = Task.Run(() =>
                {
                    nekara.ContextSwitch();
                    int localA = a;

                    nekara.ContextSwitch();
                    int localB = b;

                    nekara.Assert((localA == 0 && localB == 0) || (localA == 1 && localB == -1), "Bug found!");
                });
            }

            await Task.WhenAll(setPool);
            await Task.WhenAll(checkPool);
        }
    }
}