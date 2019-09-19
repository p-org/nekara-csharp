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
            // TODO: Need to handle exceptions - either provide a way to handle it
            //       or throw the error to the main thread.
            //       Any exception thrown here will be swallowed silently!!!
            Task.Run(action).ContinueWith(prev => AsyncLoop(action));   // Will this lead to memory leak?
        }

        public static void AsyncTaskLoop(Func<Task> action)
        {
            // TODO: Need to handle exceptions - either provide a way to handle it
            //       or throw the error to the main thread.
            //       Any exception thrown here will be swallowed silently!!!
            action().ContinueWith(prev => AsyncTaskLoop(action));   // Will this lead to memory leak?
        }
    }
}
