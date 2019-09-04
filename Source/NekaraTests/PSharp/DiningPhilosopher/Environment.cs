using System.Collections.Generic;
using Microsoft.PSharp;

namespace Nekara.Tests.PSharp.DiningPhilosopher
{
    class Environment : Machine
    {
        private Dictionary<int, MachineId> LockMachines;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        private class Init : MachineState
        {
        }

        private void InitOnEntry()
        {
            this.LockMachines = new Dictionary<int, MachineId>();

            int n = 3;
            for (int i = 0; i < n; i++)
            {
                var lck = this.CreateMachine(typeof(Lock));
                this.LockMachines.Add(i, lck);
            }

            for (int i = 0; i < n; i++)
            {
                this.CreateMachine(typeof(Philosopher), new Philosopher.Config(this.LockMachines[i], this.LockMachines[(i + 1) % n]));
            }
        }
    }
}
