using System;
using System.Collections.Generic;
using System.Text;

namespace TestingService
{
    class NoopTestingService 
    {
        public void BlockedOnResource(int resourceId)
        {
            
        }

        public void ContextSwitch()
        {
            
        }

        public bool CreateNondetBool()
        {
            return true;
        }

        public int CreateNondetInteger(int maxValue)
        {
            return 0;            
        }

        public void CreateResource(int resourceId)
        {
            
        }

        public void DeleteResource(int resourceId)
        {
            
        }

        public void EndTask(int taskId)
        {
            
        }

        public void SignalUpdatedResource(int resourceId)
        {
            
        }

        public void StartTask(int taskId)
        {
            
        }
    }
}
