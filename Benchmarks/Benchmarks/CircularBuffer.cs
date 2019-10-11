// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class CircularBuffer
    {
        public static ITestingService testingService;

        [TestMethod]
        public static async void RunTest(ITestingService testingService)
        {
            CircularBuffer.testingService = testingService;

            // create an instance of stack
            var buffer = new CircularBuffer();

            await buffer.Run();
        }

        char[] Buffer;
        uint First;
        uint Next;
        int BufferSize;
        bool Send;
        bool Receive;

        int RemoveLogElement()
        {
            //Specification.Assert(this.First >= 0, "Bug found!");
            testingService.ContextSwitch();
            if (this.Next > 0 && this.First < this.BufferSize)
            {
                this.First++;
                return this.Buffer[this.First - 1];
            }
            else
            {
                return -1;
            }
        }

        int InsertLogElement(int b)
        {
            testingService.ContextSwitch();
            if (this.Next < this.BufferSize && this.BufferSize > 0)
            {
                this.Buffer[this.Next] = (char)b;
                this.Next = (this.Next + 1) % (uint)this.BufferSize;
                //Specification.Assert(this.Next < this.BufferSize, "Bug found!");
            }
            else
            {
                return -1;
            }

            return b;
        }

        public async Task Run()
        {
            this.Buffer = new char[10];
            this.BufferSize = 10;
            this.First = 0;
            this.Next = 0;
            this.Send = true;
            this.Receive = false;
            int n = 7;

            var l = testingService.CreateLock(1);

            testingService.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                testingService.StartTask(1);
                for (int i = 0; i < n; i++)
                {
                    testingService.ContextSwitch();
                    using (l.Acquire())
                    {
                        testingService.ContextSwitch();
                        if (this.Send)
                        {
                            testingService.ContextSwitch();
                            InsertLogElement(i);
                            testingService.ContextSwitch();
                            this.Send = false;
                            this.Receive = true;
                        }
                    }
                }
                testingService.EndTask(1);
            });

            testingService.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                testingService.StartTask(2);
                for (int i = 0; i < n; i++)
                {
                    testingService.ContextSwitch();
                    using (l.Acquire())
                    {
                        testingService.ContextSwitch();
                        if (this.Receive)
                        {
                            testingService.ContextSwitch();
                            testingService.Assert(RemoveLogElement() == i, "Bug found!");
                            this.Receive = false;
                            this.Send = true;
                        }
                    }
                }
                testingService.EndTask(2);
            });

            await Task.WhenAll(t1, t2);
        }
    }
}
