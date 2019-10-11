// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class Queue
    {
        public static ITestingService testingService;

        [TestMethod]
        public static async void RunTest(ITestingService testingService)
        {
            Queue.testingService = testingService;

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
            testingService.ContextSwitch();
            q.Element[q.Tail] = x;
            testingService.ContextSwitch();
            q.Amount++;
            testingService.ContextSwitch();
            if (q.Tail == this.Size)
            {
                testingService.ContextSwitch();
                q.Tail = 1;
            }
            else
            {
                testingService.ContextSwitch();
                q.Tail++;
            }

            return 0;
        }

        int Dequeue(QType q)
        {
            testingService.ContextSwitch();
            int x = q.Element[q.Head];
            testingService.ContextSwitch();
            q.Amount--;
            testingService.ContextSwitch();
            if (q.Head == this.Size)
            {
                testingService.ContextSwitch();
                q.Head = 1;
            }
            else
            {
                testingService.ContextSwitch();
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

            var l = testingService.CreateLock(1);

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);
                int value;

                testingService.ContextSwitch();
                using (l.Acquire())
                {
                    testingService.ContextSwitch();
                    value = 0;
                    testingService.ContextSwitch();
                    storedElements[0] = value;
                }

                for (int i = 0; i < (this.Size - 1); i++)
                {
                    testingService.ContextSwitch();
                    using (l.Acquire())
                    {
                        testingService.ContextSwitch();
                        if (enqueue)
                        {
                            testingService.ContextSwitch();
                            value++;
                            testingService.ContextSwitch();
                            this.Enqueue(queue, value);
                            testingService.ContextSwitch();
                            storedElements[i + 1] = value;
                            testingService.ContextSwitch();
                            enqueue = false;
                            testingService.ContextSwitch();
                            dequeue = true;
                        }
                    }
                }
                testingService.EndTask(1);
            });

            testingService.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                testingService.StartTask(2);
                for (int i = 0; i < this.Size; i++)
                {
                    testingService.ContextSwitch();
                    using (l.Acquire())
                    {
                        testingService.ContextSwitch();
                        if (dequeue)
                        {
                            testingService.Assert(this.Dequeue(queue) == storedElements[i], "<Queue> Bug found!");
                            testingService.ContextSwitch();
                            dequeue = false;
                            testingService.ContextSwitch();
                            enqueue = true;
                        }
                    }
                }
                testingService.EndTask(2);
            });

            await Task.WhenAll(t1, t2);
        }
    }
}
