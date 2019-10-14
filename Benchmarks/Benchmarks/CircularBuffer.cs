// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class CircularBuffer
    {
        public static TestingServiceProxy ts;

        [TestMethod]
        public static async void RunTest(TestingServiceProxy ts)
        {
            CircularBuffer.ts = ts;

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
            ts.Api.ContextSwitch();
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
            ts.Api.ContextSwitch();
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

            var l = ts.LockFactory.CreateLock(1);

            ts.Api.CreateTask();
            Task t1 = Task.Run(async () =>
            {
                ts.Api.StartTask(1);
                for (int i = 0; i < n; i++)
                {
                    ts.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        if (this.Send)
                        {
                            ts.Api.ContextSwitch();
                            InsertLogElement(i);
                            ts.Api.ContextSwitch();
                            this.Send = false;
                            this.Receive = true;
                        }
                    }
                }
                ts.Api.EndTask(1);
            });

            ts.Api.CreateTask();
            Task t2 = Task.Run(async () =>
            {
                ts.Api.StartTask(2);
                for (int i = 0; i < n; i++)
                {
                    ts.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        ts.Api.ContextSwitch();
                        if (this.Receive)
                        {
                            ts.Api.ContextSwitch();
                            ts.Api.Assert(RemoveLogElement() == i, "Bug found!");
                            this.Receive = false;
                            this.Send = true;
                        }
                    }
                }
                ts.Api.EndTask(2);
            });

            await Task.WhenAll(t1, t2);
        }
    }
}
