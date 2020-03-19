using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class Queue
    {
        public static NekaraManagedClient nekara = RuntimeEnvironment.Client;
        public static bool bugFound = false;

        [Fact(Timeout = 5000)]
        public void RunQueueTest()
        {
            while (!bugFound)
            {
                nekara.Api.CreateSession();

                // create an instance of stack
                var queue = new Queue();
                queue.Run().Wait();

                nekara.Api.WaitForMainTask();
            }
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
            nekara.Api.ContextSwitch();
            q.Element[q.Tail] = x;

            nekara.Api.ContextSwitch();
            q.Amount++;

            nekara.Api.ContextSwitch();
            if (q.Tail == this.Size)
            {
                nekara.Api.ContextSwitch();
                q.Tail = 1;
            }
            else
            {
                nekara.Api.ContextSwitch();
                q.Tail++;
            }

            return 0;
        }

        int Dequeue(QType q)
        {
            nekara.Api.ContextSwitch();
            int x = q.Element[q.Head];

            nekara.Api.ContextSwitch();
            q.Amount--;

            nekara.Api.ContextSwitch();
            if (q.Head == this.Size)
            {
                nekara.Api.ContextSwitch();
                q.Head = 1;
            }
            else
            {
                nekara.Api.ContextSwitch();
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

                nekara.Api.ContextSwitch();
                using (l.Acquire())
                {
                    nekara.Api.ContextSwitch();
                    value = 0;

                    nekara.Api.ContextSwitch();
                    storedElements[0] = value;
                }

                for (int i = 0; i < (this.Size - 1); i++)
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        nekara.Api.ContextSwitch();
                        if (enqueue)
                        {
                            nekara.Api.ContextSwitch();
                            value++;

                            nekara.Api.ContextSwitch();
                            this.Enqueue(queue, value);

                            nekara.Api.ContextSwitch();
                            storedElements[i + 1] = value;

                            nekara.Api.ContextSwitch();
                            enqueue = false;

                            nekara.Api.ContextSwitch();
                            dequeue = true;
                        }
                    }
                }
            });

            Task t2 = Task.Run(() =>
            {
                for (int i = 0; i < this.Size; i++)
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        nekara.Api.ContextSwitch();
                        if (dequeue)
                        {
                            // nekara.Assert(this.Dequeue(queue) == storedElements[i], "<Queue> Bug found!");
                            if(!(this.Dequeue(queue) == storedElements[i]))
                            {
                                bugFound = true;
                            }

                            nekara.Api.ContextSwitch();
                            dequeue = false;

                            nekara.Api.ContextSwitch();
                            enqueue = true;
                        }
                    }
                }
            });

            return Task.WhenAll(t1, t2);
        }
    }
}
