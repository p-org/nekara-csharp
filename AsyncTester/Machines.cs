using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PSharp;

namespace AsyncTester
{
    class ServerProxyMachine : Machine
    {
        MethodInfo testMethod;
        ControlledTestingService testingService;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        class Init : MachineState { }

        async Task InitOnEntry()
        {
            var ev = (this.ReceivedEvent as ServerProxyMachineInitEvent);
            this.testMethod = ev.testMethod;
            this.testingService = new ControlledTestingService(this.Id);

            testMethod.Invoke(null, new object[] { testingService });

            testingService.EndTask(0);

            await testingService.IsFinished();
        }
    }

    class ServerProxyMachineInitEvent : Event
    {
        public MethodInfo testMethod;
    }

    class ClientTestMachine
    {
    }
}
