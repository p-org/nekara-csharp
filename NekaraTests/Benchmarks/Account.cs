// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using Nekara.Client;
using Nekara.Models;

namespace Benchmarks
{
    public class Account
    {
        [TestMethod]
        public static async void RunTest()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int x = 1;
            int y = 2;
            int z = 4;
            int balance = x;

            bool depositDone = false;
            bool withdrawDone = false;

            var l = new Lock(1);

            Task t1 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    if (depositDone && withdrawDone)
                    {
                        nekara.Assert(balance == (x - y) - z, "Bug found!");
                    }
                }
            });

            Task t2 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    balance += y;
                    depositDone = true;
                }
            });

            Task t3 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    balance -= z;
                    withdrawDone = true;
                }
            });

            await Task.WhenAll(t1, t2, t3);

            return;
        }
    }
}
