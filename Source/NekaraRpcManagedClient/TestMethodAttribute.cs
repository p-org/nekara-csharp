using Nekara.Core;
using System;

namespace Nekara.Client
{
    /// <summary>
    /// Attribute for declaring the entry point to
    /// a program test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestMethodAttribute : Attribute
    {
        public int TimeoutMs = Constants.SessionTimeoutMs;
        public int MaxDecisions = Constants.SessionMaxDecisions;

        public TestMethodAttribute() {}

        public TestMethodAttribute(int timeout, int maxDecisions)
        {
            this.TimeoutMs = timeout;
            this.MaxDecisions = maxDecisions;
        }
    }
}
