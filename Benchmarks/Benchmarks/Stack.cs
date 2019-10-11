// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class Stack
    {
        public static ITestingService testingService;

        [TestMethod]
        public static async void RunTest(ITestingService testingService)
        {
            Stack.testingService = testingService;

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

            var l = testingService.CreateLock(0);

            testingService.CreateTask();
            Task t1 = Task.Run(() =>
            {
                testingService.StartTask(2);
                for (int i = 0; i < size; i++)
                {
                    testingService.ContextSwitch();
                    using (l.Acquire())
                    {
                        testingService.ContextSwitch();
                        this.Push(stack, i);
                        testingService.ContextSwitch();
                        flag = true;
                    }
                    testingService.ContextSwitch();
                }
                testingService.EndTask(2);
            });

            testingService.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                testingService.StartTask(3);
                for (int i = 0; i < size; i++)
                {
                    testingService.ContextSwitch();
                    using (l.Acquire())
                    {
                        testingService.ContextSwitch();
                        if (flag)
                        {
                            testingService.ContextSwitch();
                            testingService.Assert(this.Pop(stack) != -2, "Bug found!");
                        }
                    }
                    testingService.ContextSwitch();
                }
                testingService.EndTask(3);
            });

            await Task.WhenAll(t1, t2);
        }
    }
}
