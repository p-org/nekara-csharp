using Microsoft.PSharp;

namespace Nekara.Tests.PSharp.DiningPhilosopher
{
    class Lock : Machine
    {
        public class TryLock : Event
        {
            public MachineId Target;

            public TryLock(MachineId target)
            {
                this.Target = target;
            }
        }

        public class Release : Event
        {
        }

        public class LockResp : Event
        {
            public bool LockResult;

            public LockResp(bool res)
            {
                this.LockResult = res;
            }
        }

        private bool LockVar;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        private class Init : MachineState
        {
        }

        [OnEventDoAction(typeof(TryLock), nameof(OnTryLock))]
        [OnEventDoAction(typeof(Release), nameof(OnRelease))]
        private class Waiting : MachineState
        {
        }

        private void InitOnEntry()
        {
            this.LockVar = false;
            this.Goto<Waiting>();
        }

        private void OnTryLock()
        {
            var target = (this.ReceivedEvent as TryLock).Target;
            if (this.LockVar)
            {
                this.Send(target, new LockResp(false));
            }
            else
            {
                this.LockVar = true;
                this.Send(target, new LockResp(true));
            }
        }

        private void OnRelease()
        {
            this.LockVar = false;
        }
    }
}
