using System;
using AsyncTester.Core;

namespace AsyncTester.Client
{
    public class TestRuntimeLock : IAsyncLock
    {
        public class Releaser : IDisposable
        {
            private IAsyncLock lck;

            public Releaser(IAsyncLock lck)
            {
                this.lck = lck;
            }

            public void Dispose()
            {
                lck.Release();
            }
        }

        private ITestingService api;
        private int id;
        private bool locked;
        public TestRuntimeLock(ITestingService api, int resourceId, string label = "")
        {
            this.api = api;
            this.id = resourceId;
            this.locked = false;

            this.api.CreateResource(resourceId);
        }

        public IDisposable Acquire()
        {
            this.api.ContextSwitch();
            while (true)
            {
                if (this.locked == false)
                {
                    this.locked = true;
                    break;
                }
                else
                {
                    this.api.BlockedOnResource(this.id);
                    continue;
                }
            }
            return new Releaser(this);
        }

        public void Release()
        {
            this.api.Assert(this.locked == true, "Release called on non-acquired lock");

            this.locked = false;
            this.api.SignalUpdatedResource(this.id);
        }
    }
}
