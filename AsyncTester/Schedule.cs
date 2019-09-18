using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester
{
    class Schedule
    {
        private int seed;
        private Task[] queue;

        public Schedule(int seed)
        {
            this.seed = seed;
        }

        public void push(Task task)
        {
        }
    }
}
