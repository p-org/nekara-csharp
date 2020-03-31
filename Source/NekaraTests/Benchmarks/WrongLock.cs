﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Models;

namespace Nekara.Tests.Benchmarks
{
    public class WrongLock
    {
        [TestMethod]
        public static void RunTest()
        {
            var nekara = RuntimeEnvironment.Client.Api;

            int iNum1 = 1;
            int iNum2 = 7;
            int dataValue = 0;

            var dataLock = new Lock(1);
            var thisLock = new Lock(2);

            Task[] num1Pool = new Task[iNum1];
            Task[] num2Pool = new Task[iNum2];

            for (int i = 0; i < iNum1; i++)
            {
                num1Pool[i] = Task.Run(() =>
                {
                    nekara.ContextSwitch();
                    using (dataLock.Acquire())
                    {
                        nekara.ContextSwitch();
                        int x = dataValue;

                        nekara.ContextSwitch();
                        dataValue++;

                        nekara.ContextSwitch();
                        nekara.Assert(dataValue == (x + 1), "Bug Found!");
                    }
                });
            }

            for (int i = 0; i < iNum2; i++)
            {
                num2Pool[i] = Task.Run(() =>
                {
                    nekara.ContextSwitch();
                    using (thisLock.Acquire())
                    {
                        nekara.ContextSwitch();
                        dataValue++;
                    }
                });
            }

            Task.WaitAll(num1Pool);
            Task.WaitAll(num2Pool);
        }
    }
}
