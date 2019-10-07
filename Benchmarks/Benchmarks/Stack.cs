// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using Benchmarks;

namespace Benchmarks
{
    public class Stack
    {
        int Top = 0;

        int Push(int[] stack, int x)
        {
            if (this.Top == stack.Length)
            {
                return -1;
            }
            else
            {
                stack[this.Top] = x;
                this.Top++;
            }
            return 0;
        }

        int Pop(int[] stack)
        {
            if (this.Top == 0)
            {
                return -2;
            }
            else
            {
                this.Top--;
                return stack[this.Top];
            }
        }

        public async Task Run()
        {
            int size = 10;
            int[] stack = new int[size];
            bool flag = false;

            AsyncLock l = AsyncLock.Create();

            Task t1 = Task.Run(async () =>
            {
                for (int i = 0; i < size; i++)
                {
                    Specification.InjectContextSwitch();
                    using (await l.AcquireAsync())
                    {
                        this.Push(stack, i);
                        flag = true;
                    }
                }
            });

            Task t2 = Task.Run(async () =>
            {
                for (int i = 0; i < size; i++)
                {
                    Specification.InjectContextSwitch();
                    using (await l.AcquireAsync())
                    {
                        if (flag)
                        {
                            Specification.Assert(this.Pop(stack) != -2, "Bug found!");
                        }
                    }
                }
            });

            await Task.WhenAll(t1, t2);
        }
    }
}
