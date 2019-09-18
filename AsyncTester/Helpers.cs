using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester
{
    class Helpers
    {
        public static void AsyncLoop(Action action)
        {
            Task.Run(action).ContinueWith(prev => AsyncLoop(action));   // Will this lead to memory leak?
        }

        public static void AsyncTaskLoop(Func<Task> action)
        {
            action().ContinueWith(prev => AsyncTaskLoop(action));   // Will this lead to memory leak?
        }
    }
}
