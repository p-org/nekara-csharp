// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using Nekara.Client;
using Nekara.Models;

namespace Benchmarks
{
    public class Carter
    {
        [TestMethod]
        public static async void RunTest()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int a = 0;
            int b = 0;

            var l1 = new Lock(1);
            var l2 = new Lock(2);

            Task t1 = Task.Run(async () =>
            {
                IDisposable releaser2 = null;

                nekara.ContextSwitch();
                using (l1.Acquire())
                {
                    a++;
                    if (a == 1)
                    {
                        releaser2 = l2.Acquire();
                    }
                }

                nekara.ContextSwitch();
                using (l1.Acquire())
                {
                    a--;
                    if (a == 0)
                    {
                        releaser2.Dispose();
                    }
                }
            });

            Task t2 = Task.Run(async () =>
            {
                IDisposable releaser2 = null;

                nekara.ContextSwitch();
                using (l1.Acquire())
                {
                    b++;
                    if (b == 1)
                    {
                        releaser2 = l2.Acquire();
                    }
                }

                nekara.ContextSwitch();
                using (l1.Acquire())
                {
                    b--;
                    if (b == 0)
                    {
                        releaser2.Dispose();
                    }
                }
            });

            Task t3 = Task.Run(() =>
            {
            });

            Task t4 = Task.Run(() =>
            {
            });

            await Task.WhenAll(t1, t2, t3, t4);
        }
    }
}