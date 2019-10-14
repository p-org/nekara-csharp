// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class Safestack
    {
        // private static IMachineRuntime Runtime;
        public static TestingServiceProxy ts;

        [TestMethod]
        public static async void RunTest(TestingServiceProxy ts)
        {
            Safestack.ts = ts;

            // create an instance of stack
            var stack = new Safestack();

            await stack.Run();
        }


        private struct SafeStackItem
        {
            public int Value;
            public volatile int Next;
        }

        private class SafeStack
        {
            internal readonly SafeStackItem[] Array;
            internal volatile int Head;
            internal volatile int Count;

            private readonly IAsyncLock ArrayLock;
            private readonly IAsyncLock HeadLock;
            private readonly IAsyncLock CountLock;

            public SafeStack(int pushCount)
            {
                this.Array = new SafeStackItem[pushCount];
                this.Head = 0;
                this.Count = pushCount;

                for (int i = 0; i < pushCount - 1; i++)
                {
                    this.Array[i].Next = i + 1;
                }

                this.Array[pushCount - 1].Next = -1;

                this.ArrayLock = ts.LockFactory.CreateLock(1);
                this.HeadLock = ts.LockFactory.CreateLock(2);
                this.CountLock = ts.LockFactory.CreateLock(3);

                //Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));
            }

            public async Task PushAsync(int id, int index)
            {
                //Runtime.Logger.WriteLine($"Task {id} starts push {index}.");
                ts.Api.ContextSwitch();
                int head = this.Head;
                //Runtime.Logger.WriteLine($"Task {id} reads head {head} in push {index}.");
                bool compareExchangeResult = false;

                do
                {
                    ts.Api.ContextSwitch();
                    this.Array[index].Next = head;
                    //Runtime.Logger.WriteLine($"Task {id} sets [{index}].next to {head} during push.");
                    //Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));

                    ts.Api.ContextSwitch();
                    using (this.HeadLock.Acquire())
                    {
                        if (this.Head == head)
                        {
                            this.Head = index;
                            compareExchangeResult = true;
                            // Runtime.Logger.WriteLine($"Task {id} compare-exchange in push {index} succeeded (head = {this.Head}, count = {this.Count}).");
                            // Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));
                        }
                        else
                        {
                            head = this.Head;
                            // Runtime.Logger.WriteLine($"Task {id} compare-exchange in push {index} failed and re-read head {head}.");
                        }
                    }
                }
                while (!compareExchangeResult);

                ts.Api.ContextSwitch();
                using (this.CountLock.Acquire())
                {
                    this.Count++;
                    //Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));
                }

                /*Runtime.Logger.WriteLine($"Task {id} pushed {index} (head = {this.Head}, count = {this.Count}).");
                Runtime.Logger.WriteLine($"   [0] = {this.Array[0]} | next = {this.Array[0].Next}");
                Runtime.Logger.WriteLine($"   [1] = {this.Array[1]} | next = {this.Array[1].Next}");
                Runtime.Logger.WriteLine($"   [2] = {this.Array[2]} | next = {this.Array[2].Next}");
                Runtime.Logger.WriteLine($"");*/
            }

            public async Task<int> PopAsync(int id)
            {
                //Runtime.Logger.WriteLine($"Task {id} starts pop.");
                while (this.Count > 1)
                {
                    ts.Api.ContextSwitch();
                    int head = this.Head;
                    // Runtime.Logger.WriteLine($"Task {id} reads head {head} in pop ([{head}].next is {this.Array[head].Next}).");

                    int next;
                    ts.Api.ContextSwitch();
                    using (this.ArrayLock.Acquire())
                    {
                        next = this.Array[head].Next;
                        this.Array[head].Next = -1;
                        // Runtime.Logger.WriteLine($"Task {id} exchanges {next} from [{head}].next with -1.");
                        // Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));
                    }

                    ts.Api.ContextSwitch();
                    int headTemp = head;
                    bool compareExchangeResult = false;
                    ts.Api.ContextSwitch();
                    using (this.HeadLock.Acquire())
                    {
                        if (this.Head == headTemp)
                        {
                            this.Head = next;
                            compareExchangeResult = true;
                            // Runtime.Logger.WriteLine($"Task {id} compare-exchange in pop succeeded (head = {this.Head}, count = {this.Count}).");
                            // Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));
                        }
                        else
                        {
                            headTemp = this.Head;
                            // Runtime.Logger.WriteLine($"Task {id} compare-exchange in pop failed and re-read head {headTemp}.");
                        }
                    }

                    if (compareExchangeResult)
                    {
                        ts.Api.ContextSwitch();
                        using (this.CountLock.Acquire())
                        {
                            this.Count--;
                            // Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));
                        }

                        /*Runtime.Logger.WriteLine($"Task {id} pops {head} (head = {this.Head}, count = {this.Count}).");
                        Runtime.Logger.WriteLine($"   [0] = {this.Array[0]} | next = {this.Array[0].Next}");
                        Runtime.Logger.WriteLine($"   [1] = {this.Array[1]} | next = {this.Array[1].Next}");
                        Runtime.Logger.WriteLine($"   [2] = {this.Array[2]} | next = {this.Array[2].Next}");
                        Runtime.Logger.WriteLine($"");*/
                        return head;
                    }
                    else
                    {
                        ts.Api.ContextSwitch();
                        using (this.ArrayLock.Acquire())
                        {
                            this.Array[head].Next = next;
                            // Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(this.Array));
                        }
                    }
                }

                return -1;
            }
        }

        public async Task Run()
        {
            // runtime.RegisterMonitor(typeof(StateMonitor));
            // Runtime = runtime;

            int numTasks = 5;
            var stack = new SafeStack(numTasks);

            Task[] tasks = new Task[numTasks];
            for (int i = 0; i < numTasks; i++)
            {
                int ti = 1 + i;
                ts.Api.CreateTask();
                tasks[i] = Task.Run(async () =>
                {
                    ts.Api.StartTask(ti);
                    int id = i;
                    //Runtime.Logger.WriteLine($"Starting task {id}.");
                    for (int j = 0; j != 2; j += 1)
                    {
                        int elem = await stack.PopAsync(id);
                        if (elem <= 0)
                        {
                            ts.Api.ContextSwitch();
                            continue;
                        }

                        stack.Array[elem].Value = id;
                        // Runtime.Logger.WriteLine($"Task {id} popped item '{elem}' and writes value '{id}'.");
                        // Runtime.InvokeMonitor<StateMonitor>(new StateMonitor.UpdateStateEvent(stack.Array));
                        ts.Api.ContextSwitch();
                        ts.Api.Assert(stack.Array[elem].Value == id,
                            $"Task {id} found bug: [{elem}].{stack.Array[elem].Value} is not '{id}'!");
                        
                        ts.Api.CreateTask();
                        await stack.PushAsync(id, elem);
                    }
                    ts.Api.EndTask(ti);
                });
            }

            await Task.WhenAll(tasks);
        }

        /*private class StateMonitor : Monitor
        {
            internal class UpdateStateEvent : Event
            {
                internal readonly SafeStackItem[] Array;

                internal UpdateStateEvent(SafeStackItem[] array)
                {
                    this.Array = array;
                }
            }

            public SafeStackItem[] Array;

            protected override int GetHashedState()
            {
                unchecked
                {
                    int hash = 37;
                    foreach (var item in this.Array)
                    {
                        int arrayHash = 37;
                        arrayHash = (arrayHash * 397) + item.Value.GetHashCode();
                        arrayHash = (arrayHash * 397) + item.Next.GetHashCode();
                        hash *= arrayHash;
                    }

                    return hash;
                }
            }

            [Start]
            [OnEventDoAction(typeof(UpdateStateEvent), nameof(UpdateState))]
            class Init : MonitorState { }

            void UpdateState()
            {
                var array = (this.ReceivedEvent as UpdateStateEvent).Array;
                this.Array = array;
            }
        }*/
    }
}
