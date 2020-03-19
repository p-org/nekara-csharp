using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class Potpourri
    {
        [Fact(Timeout = 5000)]
        public void WrongLock()
        {
            bool bugfound = false;

            while (!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                int iNum1 = 1;
                int iNum2 = 7;
                int dataValue = 0;

                var dataLock = new Lock(1);
                var thisLock = new Lock(2);

                Task[] num1Pool = new Task[iNum1];
                Task[] num2Pool = new Task[iNum2];

                for (int i = 0; i < iNum1; i++)
                {
                    num1Pool[i] = Task.Run(() =>
                    {
                        nekara.Api.ContextSwitch();
                        using (dataLock.Acquire())
                        {
                            nekara.Api.ContextSwitch();
                            int x = dataValue;

                            nekara.Api.ContextSwitch();
                            dataValue++;

                            nekara.Api.ContextSwitch();
                            // nekara.Assert(dataValue == (x + 1), "Bug Found!");
                            if (!(dataValue == (x + 1)))
                            {
                                bugfound = true;
                            }
                        }
                    });
                }

                for (int i = 0; i < iNum2; i++)
                {
                    num2Pool[i] = Task.Run(() =>
                    {
                        nekara.Api.ContextSwitch();
                        using (thisLock.Acquire())
                        {
                            nekara.Api.ContextSwitch();
                            dataValue++;
                        }
                    });
                }

                Task.WaitAll(num1Pool);
                Task.WaitAll(num2Pool);

                nekara.Api.WaitForMainTask();
            } 
        }

        [Fact(Timeout = 5000)]
        public void TwoStage()
        {
            bool bugfound = false;
            while (!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                int numTTasks = 1;
                int numRTasks = 1;
                int data1Value = 0;
                int data2Value = 0;

                var data1Lock = new Lock(1);
                var data2Lock = new Lock(2);

                Task[] tPool = new Task[numTTasks];
                Task[] rPool = new Task[numRTasks];

                for (int i = 0; i < numTTasks; i++)
                {
                    tPool[i] = Task.Run(() =>
                    {
                        nekara.Api.ContextSwitch();
                        using (data1Lock.Acquire())
                        {
                            data1Value = 1;
                        }

                        nekara.Api.ContextSwitch();
                        using (data2Lock.Acquire())
                        {
                            data2Value = data1Value + 1;
                        }

                        nekara.Api.ContextSwitch();
                    });
                }

                for (int i = 0; i < numRTasks; i++)
                {
                    rPool[i] = Task.Run(() =>
                    {
                        int t1 = -1;
                        int t2 = -1;

                        nekara.Api.ContextSwitch();
                        using (data1Lock.Acquire())
                        {
                            if (data1Value == 0)
                            {
                                return;
                            }
                            t1 = data1Value;
                        }

                        nekara.Api.ContextSwitch();
                        using (data2Lock.Acquire())
                        {
                            t2 = data2Value;
                        }

                        nekara.Api.ContextSwitch();
                        int localT1 = t1;

                        nekara.Api.ContextSwitch();
                        int localT2 = t2;

                        // nekara.Assert(localT2 == localT1 + 1, "Bug found!");
                        if (!(localT2 == localT1 + 1))
                        {
                            bugfound = true;
                        }
                    });
                }

                Task.WaitAll(tPool);
                Task.WaitAll(rPool);

                nekara.Api.WaitForMainTask();
            }
        }

        [Fact(Timeout = 5000)]
        public void TokenRing()
        {
            bool bugfound = false;

            while (!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                int x1 = 1;
                int x2 = 2;
                int x3 = 1;

                bool flag1 = false;
                bool flag2 = false;
                bool flag3 = false;

                var l = new Lock(1);

                Task t1 = Task.Run(() =>
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        x1 = (x3 + 1) % 4;
                        flag1 = true;
                    }
                });

                Task t2 = Task.Run(() =>
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        x2 = x1;
                        flag2 = true;
                    }
                });

                Task t3 = Task.Run(() =>
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        x3 = x2;
                        flag3 = true;
                    }
                });

                Task t4 = Task.Run(() =>
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        if (flag1 && flag2 && flag3)
                        {
                            // nekara.Assert(x1 == x2 && x2 == x3, "Bug found!");
                            if (!(x1 == x2 && x2 == x3))
                            {
                                bugfound = true;
                            }
                        }
                    }
                });

                Task.WaitAll(t1, t2, t3, t4);

                nekara.Api.WaitForMainTask();
            }
        }

        public static bool bugFoundBluetoothDriver = false;

        [Fact(Timeout = 5000)]
        public void BluetoothDriver()
        {
            while (!bugFoundBluetoothDriver)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                RunBluetoothDriver().Wait();

                nekara.Api.WaitForMainTask();
            } 
        }

        private class DeviceExtension
        {
            public int PendingIo;
            public bool StoppingFlag;
            public bool StoppingEvent;
        }

        bool Stopped;
        Lock Lock;

        private int BCSP_IoIncrement(DeviceExtension e)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.ContextSwitch();
            if (e.StoppingFlag)
            {
                return -1;
            }

            nekara.Api.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo++;
            }

            return 0;
        }

        private void BCSP_IoDecrement(DeviceExtension e)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            int pendingIo;

            nekara.Api.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo--;
                pendingIo = e.PendingIo;
            }

            if (pendingIo == 0)
            {
                nekara.Api.ContextSwitch();
                e.StoppingEvent = true;
            }
        }

        private void BCSP_PnpAdd(DeviceExtension e)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            int status = BCSP_IoIncrement(e);
            if (status == 0)
            {
                // Do work here.
                nekara.Api.ContextSwitch();
                // nekara.Assert(!this.Stopped, "Bug found!");
                if (this.Stopped)
                {
                    bugFoundBluetoothDriver = true;
                }
            }

            BCSP_IoDecrement(e);
        }

        public Task RunBluetoothDriver()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            DeviceExtension e = new DeviceExtension
            {
                PendingIo = 1,
                StoppingFlag = false,
                StoppingEvent = false
            };

            this.Lock = new Lock(1);
            this.Stopped = false;

            var mt = Task.Run(() =>
            {
                nekara.Api.ContextSwitch();

                e.StoppingFlag = true;
                BCSP_IoDecrement(e);

                nekara.Api.ContextSwitch();
                if (e.StoppingEvent)
                {
                    // Release allocated resource.
                    nekara.Api.ContextSwitch();
                    this.Stopped = true;
                }
            });

            BCSP_PnpAdd(e);

            return mt;
        }

        public static bool bugFoundCircularBuffer = false;

        [Fact(Timeout = 5000)]
        public void CircularBuffer()
        {
            while (!bugFoundCircularBuffer)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                RunCircularBuffer().Wait();

                nekara.Api.WaitForMainTask();
            }   
        }

        char[] Buffer;
        uint First;
        uint Next;
        int BufferSize;
        bool Send;
        bool Receive;

        int RemoveLogElement()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.ContextSwitch();
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
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.ContextSwitch();
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

        public Task RunCircularBuffer()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

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
                    nekara.Api.ContextSwitch();
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
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        if (this.Receive)
                        {
                            // nekara.Assert(RemoveLogElement() == i, "Bug found!");
                            if (!(RemoveLogElement() == i))
                            {
                                bugFoundCircularBuffer = true;
                            }
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
