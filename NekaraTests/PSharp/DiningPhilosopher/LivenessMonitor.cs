using Microsoft.PSharp;

namespace Nekara.Tests.PSharp.DiningPhilosopher
{
    class LivenessMonitor : Monitor
    {
        public class NotifyDone : Event
        {
        }

        [Start]
        [Hot]
        [OnEventGotoState(typeof(NotifyDone), typeof(Done))]
        private class Init : MonitorState
        {
        }

        [Cold]
        [OnEventGotoState(typeof(NotifyDone), typeof(Done))]
        private class Done : MonitorState
        {
        }
    }
}
