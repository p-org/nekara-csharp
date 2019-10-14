// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class Queue
    {
        public static TestingServiceProxy ts;

        [TestMethod]
        public static async void RunTest(TestingServiceProxy ts)
        {
            Queue.ts = ts;

            // create an instance of stack
            var queue = new Queue();

            await queue.Run();
        }

        class QType
        {
            public int[] Element;
            public int Head;
            public int Tail;
            public int Amount;
        }

        int Size;

        int Enqueue(QType q, int x)
        {
            ts.Api.ContextSwitch();
            q.Element[q.Tail] = x;
            ts.Api.ContextSwitch();
            q.Amount++;
            ts.Api.ContextSwitch();
            if (q.Tail == this.Size)
            {
                ts.Api.ContextSwitch();
                q.Tail = 1;
            }
            else
            {
                ts.Api.ContextSwitch();
                q.Tail++;
            }

            return 0;
        }

        int Dequeue(QType q)
        {
            ts.Api.ContextSwitch();
            int x = q.Element[q.Head];
            ts.Api.ContextSwitch();
            q.Amount--;
            ts.Api.ContextSwitch();
            if (q.Head == this.Size)
            {
                ts.Api.ContextSwitch();
                q.Head = 1;
            }
            else
            {
                ts.Api.ContextSwitch();
                q.Head++;
            }

            return x;
        }

        public async Task Run()
        {
            this.Size = 20;
            int[] storedElements = new int[this.Size];

            QType queue = new QType
            {
                Element = new int[this.Size],
                Head = 0,
                Tail = 0,
                Amount = 0
            };

            bool enqueue = true;
            bool dequeue = false;

            var l = ts.LockFactory.CreateLock(1);

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);
                int value;

                ts.Api.ContextSwitch();
                using (l.Acquire())
                {
                    ts.Api.ContextSwitch();
                    value = 0;
                    ts.Api.ContextSwitch();
                    storedElements[0] = value;
                }

                for (int i = 0; i < (this.Size - 1); i++)
                {
                    ts.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        if (enqueue)
                        {
                            ts.Api.ContextSwitch();
                            value++;
                            ts.Api.ContextSwitch();
                            this.Enqueue(queue, value);
                            ts.Api.ContextSwitch();
                            storedElements[i + 1] = value;
                            ts.Api.ContextSwitch();
                            enqueue = false;
                            ts.Api.ContextSwitch();
                            dequeue = true;
                        }
                    }
                }
                ts.Api.EndTask(1);
            });

            ts.Api.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                ts.Api.StartTask(2);
                for (int i = 0; i < this.Size; i++)
                {
                    ts.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        if (dequeue)
                        {
                            ts.Api.Assert(this.Dequeue(queue) == storedElements[i], "<Queue> Bug found!");
                            ts.Api.ContextSwitch();
                            dequeue = false;
                            ts.Api.ContextSwitch();
                            enqueue = true;
                        }
                    }
                }
                ts.Api.EndTask(2);
            });

            await Task.WhenAll(t1, t2);
        }
    }
}
