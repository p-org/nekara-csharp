using System.Threading.Tasks;
using Microsoft.PSharp;

namespace Nekara.Tests.PSharp.DiningPhilosopher
{
    class Philosopher : Machine
    {
        public class Config : Event
        {
            public MachineId Left;
            public MachineId Right;

            public Config(MachineId left, MachineId right)
            {
                this.Left = left;
                this.Right = right;
            }
        }

        private class TryAgain : Event
        {
        }

        private MachineId left;
        private MachineId right;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        private class Init : MachineState
        {
        }

        [OnEntry(nameof(TryAccess))]
        [OnEventDoAction(typeof(TryAgain), nameof(TryAccess))]
        private class Trying : MachineState
        {
        }

        [OnEntry(nameof(OnDone))]
        private class Done : MachineState
        {
        }

        private void InitOnEntry()
        {
            var e = this.ReceivedEvent as Config;
            this.left = e.Left;
            this.right = e.Right;
            this.Goto<Trying>();
        }

        private async Task TryAccess()
        {
            this.Send(this.left, new Lock.TryLock(this.Id));
            var ev = await this.Receive(typeof(Lock.LockResp));
            if ((ev as Lock.LockResp).LockResult)
            {
                this.Send(this.right, new Lock.TryLock(this.Id));
                var evr = await this.Receive(typeof(Lock.LockResp));
                if ((evr as Lock.LockResp).LockResult)
                {
                    this.Goto<Done>();
                    return;
                }
                else
                {
                    this.Send(this.left, new Lock.Release());
                }
            }

            this.Send(this.Id, new TryAgain());
        }

        private void OnDone()
        {
            this.Send(this.left, new Lock.Release());
            this.Send(this.right, new Lock.Release());
            this.Monitor<LivenessMonitor>(new LivenessMonitor.NotifyDone());
            this.Raise(new Halt());
        }
    }
}
