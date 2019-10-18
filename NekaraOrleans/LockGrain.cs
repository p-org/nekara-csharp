using System;
using System.Threading.Tasks;
using Orleans;
using Nekara.Core;
using Nekara.Client;

namespace Nekara.Orleans
{
    public class LockGrain : Grain, ILockGrain
    {
        private static ITestingService Api = RuntimeEnvironment.Client.Api;

        private bool locked;
        private int id;

        public LockGrain()
        {
            this.id = (int)this.GetPrimaryKeyLong();
            Console.WriteLine("[LockGrain] ID {0} was instantiated", this.id);
        }

        public Task Acquire()
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
            return Task.CompletedTask;
        }

        public Task Release()
        {
            Api.Assert(this.locked == true, "Release called on non-acquired lock");

            this.locked = false;
            Api.SignalUpdatedResource(this.id);

            return Task.CompletedTask;
        }
    }
}
