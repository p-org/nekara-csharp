// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    public class CircularBuffer
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;

        [TestMethod]
        public static void RunTest()
        {
            // create an instance of stack
            var buffer = new CircularBuffer();

            buffer.Run().Wait();
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
            nekara.ContextSwitch();
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
            nekara.ContextSwitch();
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

        public Task Run()
        {
            this.Buffer = new char[10];
            this.BufferSize = 10;
            this.First = 0;
            this.Next = 0;
            this.Send = true;
            this.Receive = false;
            int n = 7;

            var l = new Lock(1);

            Task t1 = Task.Run(() =>
            {
                for (int i = 0; i < n; i++)
                {
                    nekara.ContextSwitch();
                    using (l.Acquire())
                    {
                        if (this.Send)
                        {
                            InsertLogElement(i);
                            this.Send = false;
                            this.Receive = true;
                        }
                    }
                }
            });

            Task t2 = Task.Run(() =>
            {
                for (int i = 0; i < n; i++)
                {
                    nekara.ContextSwitch();
                    using (l.Acquire())
                    {
                        if (this.Receive)
                        {
                            nekara.Assert(RemoveLogElement() == i, "Bug found!");
                            this.Receive = false;
                            this.Send = true;
                        }
                    }
                }
            });

            return Task.WhenAll(t1, t2);
        }
    }
}