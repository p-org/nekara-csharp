// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    public class BluetoothDriver
    {
        public static ITestingService testingService;

        [TestMethod]
        public static async void RunTest(ITestingService testingService)
        {
            BluetoothDriver.testingService = testingService;

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
            testingService.StartTask(1);
            testingService.ContextSwitch();
            if (e.StoppingFlag)
            {
                return -1;
            }

            testingService.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo++;
            }

            testingService.EndTask(1);
            return 0;
        }

        private async Task BCSP_IoDecrement(DeviceExtension e)
        {
            testingService.StartTask(2);
            int pendingIo;

            testingService.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo--;
                pendingIo = e.PendingIo;
            }

            if (pendingIo == 0)
            {
                testingService.ContextSwitch();
                e.StoppingEvent = true;
            }
            testingService.EndTask(2);
        }

        private async Task BCSP_PnpAdd(DeviceExtension e)
        {
            testingService.StartTask(3);
            testingService.CreateTask();
            int status = await BCSP_IoIncrement(e);
            if (status == 0)
            {
                // Do work here.
                testingService.ContextSwitch();
                testingService.Assert(!this.Stopped, "Bug found!");
            }

            testingService.CreateTask();
            await BCSP_IoDecrement(e);
            testingService.EndTask(3);
        }

        public async Task Run()
        {
            DeviceExtension e = new DeviceExtension
            {
                PendingIo = 1,
                StoppingFlag = false,
                StoppingEvent = false
            };

            this.Lock = testingService.CreateLock(0);
            this.Stopped = false;

            testingService.CreateTask();
            Task t = Task.Run(async () =>
            {
                testingService.StartTask(1);
                testingService.ContextSwitch();
                e.StoppingFlag = true;
                testingService.CreateTask();
                await BCSP_IoDecrement(e);
                testingService.ContextSwitch();
                if (e.StoppingEvent)
                {
                    // Release allocated resource.
                    testingService.ContextSwitch();
                    this.Stopped = true;
                }
                testingService.EndTask(1);
            });

            testingService.CreateTask();
            await BCSP_PnpAdd(e);
            await Task.WhenAll(t);
        }
    }
}
