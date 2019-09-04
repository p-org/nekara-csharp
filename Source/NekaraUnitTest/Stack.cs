using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class Stack
    {
        public static NekaraManagedClient nekara = RuntimeEnvironment.Client;
        public static bool bugFound = false;

        [Fact(Timeout = 5000)]
        public void RunStackTest()
        {
            while (!bugFound)
            {
                nekara.Api.CreateSession();

                // create an instance of stack
                var stack = new Stack();
                stack.Run().Wait();

                nekara.Api.WaitForMainTask();
            } 
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
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        this.Push(stack, i);
                        flag = true;
                    }
                    nekara.Api.ContextSwitch();
                }
            });

            Task t2 = Task.Run(() =>
            {
                for (int i = 0; i < size; i++)
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        nekara.Api.ContextSwitch();
                        if (flag)
                        {
                            // nekara.Assert(this.Pop(stack) != -2, "Bug found!");
                            if (!(this.Pop(stack) != -2))
                            {
                                bugFound = true;
                            }
                        }
                    }
                }
            });

            return Task.WhenAll(t1, t2);
        }
    }
}
