// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using Nekara.Client;
using Nekara.Models;

namespace Benchmarks
{
    public class Lazy
    {
        [TestMethod]
        public static async void RunTest()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int data = 0;

            var l = new Lock(1);

            Task t1 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    data++;
                }
            });

            Task t2 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    data += 2;
                }
            });

            Task t3 = Task.Run(async () =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    nekara.Assert(data < 3, "Bug found!");
                }
            });

            await Task.WhenAll(t1, t2, t3);
        }
    }
}
