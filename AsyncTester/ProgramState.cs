using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester
{
    class ProgramState
    {
        // All tasks in the program and their TCS
        public Dictionary<int, TaskCompletionSource<bool>> taskToTcs;

        // Set of all resources in the system
        public HashSet<int> resourceSet;

        // task -> the resource its blocked on (if any)
        public Dictionary<int, int> taskStatus;

        // current executing task
        public int currentTask;

        public ProgramState()
        {
            taskToTcs = new Dictionary<int, TaskCompletionSource<bool>>();
            resourceSet = new HashSet<int>();
            taskStatus = new Dictionary<int, int>();
            currentTask = 0;
        }
    }
}
