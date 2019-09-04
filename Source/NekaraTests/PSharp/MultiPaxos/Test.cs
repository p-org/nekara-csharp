using Microsoft.PSharp;
using Nekara.Client;

namespace Nekara.Tests.PSharp.MultiPaxos
{
    class Test
    {
        [TestMethod(10000, 2500)]
        public static void Run()
        {
            var runtime = PSharpTestRuntime.Create();

            runtime.RegisterMonitor(typeof(ValidityCheck));
            runtime.CreateMachine(typeof(GodMachine));
        }
    }
}
