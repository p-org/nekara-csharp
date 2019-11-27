// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Core;
using Nekara.Models;

namespace Nekara.Tests.Benchmarks
{
    public class Queue
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;

        [TestMethod]
        public static void RunTest()
        {
            // create an instance of stack
            var queue = new Queue();

            queue.Run().Wait();
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
            nekara.ContextSwitch();
            q.Element[q.Tail] = x;

            nekara.ContextSwitch();
            q.Amount++;

            nekara.ContextSwitch();
            if (q.Tail == this.Size)
            {
                nekara.ContextSwitch();
                q.Tail = 1;
            }
            else
            {
                nekara.ContextSwitch();
                q.Tail++;
            }

            return 0;
        }

        int Dequeue(QType q)
        {
            nekara.ContextSwitch();
            int x = q.Element[q.Head];

            nekara.ContextSwitch();
            q.Amount--;

            nekara.ContextSwitch();
            if (q.Head == this.Size)
            {
                nekara.ContextSwitch();
                q.Head = 1;
            }
            else
            {
                nekara.ContextSwitch();
                q.Head++;
            }

            return x;
        }

        public Task Run()
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

            var l = new Lock(1);

            Task t1 = Task.Run(() =>
            {
                int value;

                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    nekara.ContextSwitch();
                    value = 0;

                    nekara.ContextSwitch();
                    storedElements[0] = value;
                }

                for (int i = 0; i < (this.Size - 1); i++)
                {
                    nekara.ContextSwitch();
                    using (l.Acquire())
                    {
                        nekara.ContextSwitch();
                        if (enqueue)
                        {
                            nekara.ContextSwitch();
                            value++;

                            nekara.ContextSwitch();
                            this.Enqueue(queue, value);

                            nekara.ContextSwitch();
                            storedElements[i + 1] = value;

                            nekara.ContextSwitch();
                            enqueue = false;

                            nekara.ContextSwitch();
                            dequeue = true;
                        }
                    }
                }
            });

            Task t2 = Task.Run(() =>
            {
                for (int i = 0; i < this.Size; i++)
                {
                    nekara.ContextSwitch();
                    using (l.Acquire())
                    {
                        nekara.ContextSwitch();
                        if (dequeue)
                        {
                            nekara.Assert(this.Dequeue(queue) == storedElements[i], "<Queue> Bug found!");
                            nekara.ContextSwitch();
                            dequeue = false;

                            nekara.ContextSwitch();
                            enqueue = true;
                        }
                    }
                }
            });

            return Task.WhenAll(t1, t2);
        }
    }
}
