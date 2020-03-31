﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Models;

namespace Nekara.Tests.Benchmarks
{
    public class DiningPhilosophers4
    {
        [TestMethod]
        public static async void Run()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int n = 4;
            int phil = 0;

            var countLock = new Lock(0);

            Lock[] locks = new Lock[n];
            for (int i = 0; i < n; i++)
            {
                locks[i] = new Lock(1 + i);
            }

            Task[] tasks = new Task[n];
            for (int i = 0; i < n; i++)
            {
                int id = i;

                tasks[i] = Task.Run(() =>
                {
                    int left = id % n;
                    int right = (id + 1) % n;

                    nekara.ContextSwitch();
                    var releaserR = locks[right].Acquire();

                    nekara.ContextSwitch();
                    var releaserL = locks[left].Acquire();

                    nekara.ContextSwitch();
                    releaserL.Dispose();

                    nekara.ContextSwitch();
                    releaserR.Dispose();

                    using (countLock.Acquire())
                    {
                        ++phil;
                        nekara.Assert(phil != n, "Bug found!");
                    }
                });
            }

            await Task.WhenAll(tasks);
        }
    }
}