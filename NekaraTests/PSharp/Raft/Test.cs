using Microsoft.PSharp;
using Nekara.Client;

namespace Nekara.Tests.PSharp.Raft
{
    class Test
    {
        [TestMethod(10000, 2000)]
        public static void Run()
        {
            var runtime = PSharpTestRuntime.Create();

            runtime.RegisterMonitor(typeof(SafetyMonitor));
            runtime.CreateMachine(typeof(ClusterManager));
        }
    }
}
