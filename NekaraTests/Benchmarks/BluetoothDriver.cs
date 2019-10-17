// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    public class BluetoothDriver
    {
        public static ITestingService nekara;

        [TestMethod]
        public static async void RunTest()
        {
            //System.Diagnostics.Debugger.Launch();

            BluetoothDriver.nekara = RuntimeEnvironment.Client.Api;

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

        bool Stopped;
        Lock Lock;

        private int BCSP_IoIncrement(DeviceExtension e)
        {
            nekara.ContextSwitch();
            if (e.StoppingFlag)
            {
                return -1;
            }

            nekara.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo++;
            }

            return 0;
        }

        private void BCSP_IoDecrement(DeviceExtension e)
        {
            int pendingIo;

            nekara.ContextSwitch();
            using (this.Lock.Acquire())
            {
                e.PendingIo--;
                pendingIo = e.PendingIo;
            }

            if (pendingIo == 0)
            {
                nekara.ContextSwitch();
                e.StoppingEvent = true;
            }
        }

        private void BCSP_PnpAdd(DeviceExtension e)
        {
            int status = BCSP_IoIncrement(e);
            if (status == 0)
            {
                // Do work here.
                nekara.ContextSwitch();
                nekara.Assert(!this.Stopped, "Bug found!");
            }

            BCSP_IoDecrement(e);
        }

        public async NativeTasks.Task Run()
        {
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
                nekara.ContextSwitch();

                e.StoppingFlag = true;
                BCSP_IoDecrement(e);

                nekara.ContextSwitch();
                if (e.StoppingEvent)
                {
                    // Release allocated resource.
                    nekara.ContextSwitch();
                    this.Stopped = true;
                }
            });

            BCSP_PnpAdd(e);

            await mt;
        }
    }
}
