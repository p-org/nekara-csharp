// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class Stack
    {
        public static TestingServiceProxy ts;

        [TestMethod]
        public static async void RunTest(TestingServiceProxy ts)
        {
            Stack.ts = ts;

            // create an instance of stack
            var stack = new Stack();

            await stack.Run();
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

        public async Task Run()
        {
            int size = 10;
            int[] stack = new int[size];
            bool flag = false;

            var l = ts.LockFactory.CreateLock(0);

            ts.Api.CreateTask();
            Task t1 = Task.Run(() =>
            {
                ts.Api.StartTask(2);
                for (int i = 0; i < size; i++)
                {
                    ts.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        this.Push(stack, i);
                        ts.Api.ContextSwitch();
                        flag = true;
                    }
                    ts.Api.ContextSwitch();
                }
                ts.Api.EndTask(2);
            });

            ts.Api.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                ts.Api.StartTask(3);
                for (int i = 0; i < size; i++)
                {
                    ts.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        if (flag)
                        {
                            ts.Api.ContextSwitch();
                            ts.Api.Assert(this.Pop(stack) != -2, "Bug found!");
                        }
                    }
                    ts.Api.ContextSwitch();
                }
                ts.Api.EndTask(3);
            });

            await Task.WhenAll(t1, t2);
        }
    }
}
