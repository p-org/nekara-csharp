using Microsoft.PSharp;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TestingService
{
    class TopLevelMachine : Machine
    {
        MethodInfo testMethod;
        ControlledTestingService testingService;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        class Init : MachineState { }

        async Task InitOnEntry()
        {
            var ev = (this.ReceivedEvent as TopLevelMachineInitEvent);
            this.testMethod = ev.testMethod;
            this.testingService = new ControlledTestingService(this.Id);            
            
            testMethod.Invoke(null, new object[] { testingService });

            testingService.EndTask(0);

            await testingService.IsFinished();
        }

    }

    class TopLevelMachineInitEvent : Event
    {
        public MethodInfo testMethod;
    }
}
