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
    }
}
