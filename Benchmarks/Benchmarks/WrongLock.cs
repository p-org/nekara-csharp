// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using Benchmarks;

namespace Benchmarks
{
    public class WrongLock
    {
        public async Task Run()
        {
            int iNum1 = 1;
            int iNum2 = 7;
            int dataValue = 0;

            AsyncLock dataLock = AsyncLock.Create();
            AsyncLock thisLock = AsyncLock.Create();

            Task[] num1Pool = new Task[iNum1];
            Task[] num2Pool = new Task[iNum2];

            for (int i = 0; i < iNum1; i++)
            {
                num1Pool[i] = Task.Run(async () =>
                {
                    Specification.InjectContextSwitch();
                    using (await dataLock.AcquireAsync())
                    {
                        Specification.InjectContextSwitch();
                        int x = dataValue;
                        Specification.InjectContextSwitch();
                        dataValue++;
                        Specification.InjectContextSwitch();
                        Specification.Assert(dataValue == (x + 1), "Bug Found!");
                    }
                });
            }

            for (int i = 0; i < iNum2; i++)
            {
                num2Pool[i] = Task.Run(async () =>
                {
                    Specification.InjectContextSwitch();
                    using (await thisLock.AcquireAsync())
                    {
                        Specification.InjectContextSwitch();
                        dataValue++;
                    }
                });
            }

            await Task.WhenAll(num1Pool);
            await Task.WhenAll(num2Pool);
        }
    }
}
