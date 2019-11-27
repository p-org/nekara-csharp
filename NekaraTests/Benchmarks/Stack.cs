// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    public class Stack
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;

        [TestMethod]
        public static void RunTest()
        {
            // create an instance of stack
            var stack = new Stack();

            stack.Run().Wait();
        }

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

        public Task Run()
        {
            int size = 10;
            int[] stack = new int[size];
            bool flag = false;

            var l = new Lock(0);

            Task t1 = Task.Run(() =>
            {
                for (int i = 0; i < size; i++)
                {
                    nekara.ContextSwitch();
                    using (l.Acquire())
                    {
                        this.Push(stack, i);
                        flag = true;
                    }
                    nekara.ContextSwitch();
                }
            });

            Task t2 = Task.Run(() =>
            {
                for (int i = 0; i < size; i++)
                {
                    nekara.ContextSwitch();
                    using (l.Acquire())
                    {
                        nekara.ContextSwitch();
                        if (flag)
                        {
                            nekara.Assert(this.Pop(stack) != -2, "Bug found!");
                        }
                    }
                }
            });

            return Task.WhenAll(t1, t2);
        }
    }
}
