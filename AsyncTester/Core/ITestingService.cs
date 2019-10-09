using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester.Core
{
    public interface ITestingService
    {
        void CreateTask();

        void StartTask(int taskId);

        void EndTask(int taskId);

        IAsyncLock CreateLock(int resourceId);

        void CreateResource(int resourceId);

        void DeleteResource(int resourceId);

        void BlockedOnResource(int resourceId);

        void SignalUpdatedResource(int resourceId);

        bool CreateNondetBool();

        int CreateNondetInteger(int maxValue);

        void Assert(bool value, string message);

        void ContextSwitch();
    }
}
