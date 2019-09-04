using System;
// using Nekara.Core;
// using Nekara.Client;
using NekaraManaged.Client;

namespace Nekara.Models
{
    public class Lock : ILock
    {
        private static ITestingService Api = RuntimeEnvironment.Client.Api;

        public class Releaser : IDisposable
        {
            private ILock lck;

            public Releaser(ILock lck)
            {
                this.lck = lck;
            }

            public void Dispose()
            {
                this.lck.Release();
            }
        }

        private int id;
        private bool locked;

        public Lock(int resourceId, string label = "")
        {
            this.id = resourceId;
            this.locked = false;

            Api.CreateResource(resourceId);
        }

        public IDisposable Acquire()
        {
            Api.ContextSwitch();
            while (true)
            {
                if (this.locked == false)
                {
                    this.locked = true;
                    break;
                }
                else
                {
                    Api.BlockedOnResource(this.id);
                    continue;
                }
            }
            return new Releaser(this);
        }

        public void Release()
        {
            Api.Assert(this.locked == true, "Release called on non-acquired lock");

            this.locked = false;
            Api.SignalUpdatedResource(this.id);
        }
    }
}
