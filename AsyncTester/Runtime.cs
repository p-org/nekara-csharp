using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncTester
{
    class RuntimeConfiguation
    {
        public RuntimeConfiguation()
        {
        }
    }

    // A Runtime is like a sandbox, in which all the (distributed) states created will be maintained.
    class Runtime
    {
        private RuntimeConfiguation config;

        public Runtime(RuntimeConfiguation config)
        {
            this.config = config;

            var defer = GetDelayed(1000, 5);
            defer.ContinueWith(prev => Console.WriteLine("Deferred Object returned {0}", defer.Result));
            // Console.WriteLine(defer.Result.ToString());
            Console.WriteLine("    Created New Runtime");
        }

        // reset will delete the state and the schedule
        public void reset()
        {

        }

        public void run(int seed)
        {

        }

        public Schedule createSchedule (int seed)
        {
            // Create an empty Schedule, initializing it with the seed.
            // Seeding is important so that we can reproduce the same Schedule.
            Schedule schedule = new Schedule(seed);

            return schedule;
        }

        // Testing Task Behaviour
        public Task<Object> GetDelayed (int delay, Object thing)
        {
            var tcs = new TaskCompletionSource<Object>();
            var timer = new Timer((state) =>
            {
                Console.WriteLine("Running Task {0} on Thread {1}", Task.CurrentId, Thread.CurrentThread.ManagedThreadId);
                tcs.SetResult(thing);
            }, null, delay, Timeout.Infinite);

            return tcs.Task;
        }
    }
}
