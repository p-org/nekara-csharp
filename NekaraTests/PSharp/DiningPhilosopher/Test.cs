using Microsoft.PSharp;
using Nekara.Client;

namespace Nekara.Tests.PSharp.DiningPhilosopher
{
    class Test
    {
        [TestMethod]
        public static void Run()
        {
            var configuration = Configuration.Create().WithVerbosityEnabled();
            var runtime = PSharpTestRuntime.Create(configuration);

            runtime.RegisterMonitor(typeof(LivenessMonitor));
            runtime.CreateMachine(typeof(Environment));
        }
    }
}