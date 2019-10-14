// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    public class BluetoothDriver
    {
        public static TestingServiceProxy ts;

        [TestMethod]
        public static async void RunTest(TestingServiceProxy ts)
        {
            System.Diagnostics.Debugger.Launch();

            BluetoothDriver.ts = ts;

            // create an instance of stack
            var driver = new BluetoothDriver();

            await driver.Run();
        }

        private class DeviceExtension
        {
            public int PendingIo;
            public bool StoppingFlag;
            public bool StoppingEvent;
        }

        IAsyncLock Lock;
        bool Stopped;

        private int BCSP_IoIncrement(DeviceExtension e)
        {
            ts.Api.ContextSwitch();
            if (e.StoppingFlag)
            {
                return -1;
            }

            ts.Api.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo++;
            }

            return 0;
        }

        private void BCSP_IoDecrement(DeviceExtension e)
        {
            int pendingIo;

            ts.Api.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo--;
                pendingIo = e.PendingIo;
            }

            if (pendingIo == 0)
            {
                ts.Api.ContextSwitch();
                e.StoppingEvent = true;
            }
        }

        private void BCSP_PnpAdd(DeviceExtension e)
        {
            int status = BCSP_IoIncrement(e);
            if (status == 0)
            {
                // Do work here.
                ts.Api.ContextSwitch();
                ts.Api.Assert(!this.Stopped, "Bug found!");
            }

            BCSP_IoDecrement(e);
        }

        public async Task Run()
        {
            DeviceExtension e = new DeviceExtension
            {
                PendingIo = 1,
                StoppingFlag = false,
                StoppingEvent = false
            };

            this.Lock = ts.LockFactory.CreateLock(0);
            this.Stopped = false;

            var mt = MyRun(1, () =>
            {
                ts.Api.ContextSwitch();
                e.StoppingFlag = true;
                BCSP_IoDecrement(e);
                ts.Api.ContextSwitch();
                if (e.StoppingEvent)
                {
                    // Release allocated resource.
                    ts.Api.ContextSwitch();
                    this.Stopped = true;
                }
            });

            BCSP_PnpAdd(e);

            await MyAwait(mt);
        }

        class MyTask
        {
            public int Id;
            public Task InnerTask;
            public bool Completed;

            public MyTask(int id)
            {
                Id = id;
                InnerTask = null;
                Completed = false;
            }
        }

        static MyTask MyRun(int TaskId, System.Action action)
        {
            var mt = new MyTask(TaskId);
            ts.Api.CreateTask();
            ts.Api.CreateResource(TaskId);
            var t = Task.Run(() =>
                {
                    ts.Api.StartTask(TaskId);
                    action();
                    mt.Completed = true;
                    ts.Api.SignalUpdatedResource(TaskId);
                    ts.Api.EndTask(TaskId);
                });

            mt.InnerTask = t;
            return mt;
        }

        static async Task MyAwait(MyTask mt)
        {
            ts.Api.ContextSwitch();
            if (mt.Completed)
            {
                return;
            }
            else
            {
                ts.Api.BlockedOnResource(mt.Id);
                await mt.InnerTask;
            }
        }
    }
}
