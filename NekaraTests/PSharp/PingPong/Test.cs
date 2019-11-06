using Microsoft.PSharp;
using Nekara.Client;

namespace Nekara.Tests.PSharp.PingPong
{
    class Test
    {
        [TestMethod]
        public static void Run()
        {
            var configuration = Configuration.Create().WithVerbosityEnabled();
            var runtime = PSharpTestRuntime.Create(configuration);

            runtime.CreateMachine(typeof(NetworkEnvironment));
        }
    }
}
