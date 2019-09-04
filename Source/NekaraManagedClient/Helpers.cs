using System;
using System.Collections.Generic;
using System.Text;

namespace NekaraManaged.Client
{
    public class Helpers
    {
        int _generate_task_ID;
        int _generate_resource_ID;

        public Helpers()
        {
            _generate_task_ID = 1000;
            _generate_resource_ID = 100000;
        }

        public int GenerateThreadID()
        {
            int _task_ID;
            lock(this)
            {
                _task_ID = _generate_task_ID;
                _generate_task_ID++;
            }
            return _task_ID;
        }

        public int GenerateResourceID()
        {
            int _resource_ID;
            lock(this)
            {
                _resource_ID = _generate_resource_ID;
                _generate_resource_ID++;
            }
            return _resource_ID;
        }
    }
}
