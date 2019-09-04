using System;
using System.Collections.Generic;
using System.Text;

namespace TestingService
{
    interface ITestable
    {
        void StartTask(int taskId);

        void EndTask(int taskId);

        void CreateResource(int resourceId);

        void DeleteResource(int resourceId);

        void BlockedOnResource(int resourceId);

        void SignalUpdatedResource(int resourceId);

        bool CreateNondetBool();

        int CreateNondetInteger(int maxValue);
    }
}
