// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Models;

namespace Nekara.Tests.Benchmarks
{
    public class TokenRing
    {
        [TestMethod]
        public static void RunTest()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int x1 = 1;
            int x2 = 2;
            int x3 = 1;

            bool flag1 = false;
            bool flag2 = false;
            bool flag3 = false;

            var l = new Lock(1);

            Task t1 = Task.Run(() =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    x1 = (x3 + 1) % 4;
                    flag1 = true;
                }
            });

            Task t2 = Task.Run(() =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    x2 = x1;
                    flag2 = true;
                }
            });

            Task t3 = Task.Run(() =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    x3 = x2;
                    flag3 = true;
                }
            });

            Task t4 = Task.Run(() =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    if (flag1 && flag2 && flag3)
                    {
                        nekara.Assert(x1 == x2 && x2 == x3, "Bug found!");
                    }
                }
            });

            Task.WaitAll(t1, t2, t3, t4);
        }
    }
}
