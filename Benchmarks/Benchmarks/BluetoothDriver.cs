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

        private async Task<int> BCSP_IoIncrement(DeviceExtension e)
        {
            ts.Api.StartTask(1);
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

            ts.Api.EndTask(1);
            return 0;
        }

        private async Task BCSP_IoDecrement(DeviceExtension e)
        {
            ts.Api.StartTask(2);
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
            ts.Api.EndTask(2);
        }

        private async Task BCSP_PnpAdd(DeviceExtension e)
        {
            ts.Api.StartTask(3);
            ts.Api.CreateTask();
            int status = await BCSP_IoIncrement(e);
            if (status == 0)
            {
                // Do work here.
                ts.Api.ContextSwitch();
                ts.Api.Assert(!this.Stopped, "Bug found!");
            }

            ts.Api.CreateTask();
            await BCSP_IoDecrement(e);
            ts.Api.EndTask(3);
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

            ts.Api.CreateTask();
            Task t = Task.Run(async () =>
            {
                ts.Api.StartTask(1);
                ts.Api.ContextSwitch();
                e.StoppingFlag = true;
                ts.Api.CreateTask();
                await BCSP_IoDecrement(e);
                ts.Api.ContextSwitch();
                if (e.StoppingEvent)
                {
                    // Release allocated resource.
                    ts.Api.ContextSwitch();
                    this.Stopped = true;
                }
                ts.Api.EndTask(1);
            });

            ts.Api.CreateTask();
            await BCSP_PnpAdd(e);
            await Task.WhenAll(t);
        }
    }
}
