using Microsoft.PSharp;
using Nekara.Client;

namespace Nekara.Tests.PSharp.Chord
{
    class Test
    {
        [TestMethod]
        public static void Run()
        {
            //var configuration = Configuration.Create().WithVerbosityEnabled();
            var configuration = Configuration.Create();
            var runtime = PSharpTestRuntime.Create(configuration);

            runtime.RegisterMonitor(typeof(LivenessMonitor));
            runtime.CreateMachine(typeof(ClusterManager));
        }
    }
}
