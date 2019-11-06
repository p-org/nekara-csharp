using Microsoft.PSharp;
using Nekara.Client;

namespace Nekara.Tests.PSharp.FailureDetector
{
    class Test
    {
        [TestMethod]
        public static void Run()
        {
            var configuration = Configuration.Create().WithVerbosityEnabled();
            var runtime = PSharpTestRuntime.Create(configuration);

            // Monitors must be registered before the first P# machine
            // gets created (which will kickstart the runtime).
            runtime.RegisterMonitor(typeof(Safety));
            runtime.RegisterMonitor(typeof(Liveness));
            runtime.CreateMachine(typeof(Driver), new Driver.Config(2));
        }
    }
}
