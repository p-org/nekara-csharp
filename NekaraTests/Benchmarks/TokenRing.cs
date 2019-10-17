// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Models;

namespace Nekara.Tests.Benchmarks
{
    public class TokenRing
    {
        [TestMethod]
        public static async void RunTest()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int x1 = 1;
            int x2 = 2;
            int x3 = 1;

            bool flag1 = false;
            bool flag2 = false;
            bool flag3 = false;

            var l = new Lock(1);

            Task t1 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    nekara.ContextSwitch();
                    x1 = (x3 + 1) % 4;

                    nekara.ContextSwitch();
                    flag1 = true;
                }
            });

            Task t2 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    nekara.ContextSwitch();
                    x2 = x1;

                    nekara.ContextSwitch();
                    flag2 = true;
                }
            });

            Task t3 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    nekara.ContextSwitch();
                    x3 = x2;

                    nekara.ContextSwitch();
                    flag3 = true;
                }
            });

            Task t4 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    nekara.ContextSwitch();
                    if (flag1 && flag2 && flag3)
                    {
                        nekara.ContextSwitch();
                        nekara.Assert(x1 == x2 && x2 == x3, "Bug found!");
                    }
                }
            });

            await Task.WhenAll(t1, t2, t3, t4);
        }
    }
}
