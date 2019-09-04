using Microsoft.PSharp;

namespace Nekara.Tests.PSharp.Chord
{
    class LivenessMonitor : Monitor
    {
        #region events

        public class NotifyClientRequest : Event
        {
            public int Key;

            public NotifyClientRequest(int key)
                : base()
            {
                this.Key = key;
            }
        }

        public class NotifyClientResponse : Event
        {
            public int Key;

            public NotifyClientResponse(int key)
                : base()
            {
                this.Key = key;
            }
        }

        #endregion

        #region states

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        class Init : MonitorState { }

        void InitOnEntry()
        {
            this.Goto<Responded>();
        }

        [Cold]
        [OnEntry(nameof(CheckLivenessTemperature))]
        [OnEventGotoState(typeof(NotifyClientRequest), typeof(Requested))]
        class Responded : MonitorState { }

        void CheckLivenessTemperature()
        {
            if (this.State.IsHot)
            {
                this.LivenessTemperature++;
                this.Assert(
                    this.LivenessTemperature <= 1000,
                    "Monitor '{0}' detected potential liveness bug in hot state '{1}'.",
                    this.GetType().Name, this.CurrentStateName);
            }
        }

        [Hot]
        [OnEntry(nameof(CheckLivenessTemperature))]
        [OnEventGotoState(typeof(NotifyClientResponse), typeof(Responded))]
        class Requested : MonitorState { }

        #endregion
    }
}
