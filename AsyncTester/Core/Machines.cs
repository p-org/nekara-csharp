using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PSharp;

namespace AsyncTester.Core
{
    class ServerProxyMachine : Machine
    {
        MethodInfo testMethod;
        ITestingService testingService;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        class Init : MachineState { }

        async Task InitOnEntry()
        {
            var ev = (this.ReceivedEvent as ServerProxyMachineInitEvent);
            this.testMethod = ev.testMethod;
            // this.testingService = new ControlledTestingService(this.Id);
            var proxy = new TestingServiceProxy(ev.socket);
            this.testingService = proxy.testingAPI;

            this.testMethod.Invoke(null, new object[] { this.testingService });

            this.testingService.EndTask(0);

            await proxy.IsFinished(proxy.testingAPI.sessionId);
        }

        static void ProxyTestMethod(ITestingService testingService)
        {
            // This method is the entry point of the server-side test runtime
            // and not the actual target method.

        }
    }

    class ServerProxyMachineInitEvent : Event
    {
        public MethodInfo testMethod;
        public OmniClient socket;
    }

    class ClientProxyRuntime
    {
    }
}
