using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Nekara;

namespace NekaraManaged.Client
{
    public class TestRuntimeApi : ITestingService
    {
        [DllImport("nekara.dll")]
        public static extern IntPtr CreateScheduler();
        [DllImport("nekara.dll")]
        public static extern void Attach(IntPtr ip);
        [DllImport("nekara.dll")]
        public static extern void Detach(IntPtr ip);
        [DllImport("nekara.dll")]
        public static extern bool IsDetached(IntPtr ip);
        [DllImport("nekara.dll")]
        public static extern void CreateOperation(IntPtr ip);
        [DllImport("nekara.dll")]
        public static extern void StartOperation(IntPtr ip, int _threadID);
        [DllImport("nekara.dll")]
        public static extern void EndOperation(IntPtr ip, int _threadID);
        [DllImport("nekara.dll")]
        public static extern void ScheduleNextOperation(IntPtr ip);
        [DllImport("nekara.dll")]
        public static extern void CreateResource(IntPtr ip, int _resourceID);
        [DllImport("nekara.dll")]
        public static extern void DeleteResource(IntPtr ip, int _resourceID);
        [DllImport("nekara.dll")]
        public static extern void SignalUpdatedResource(IntPtr ip, int _resourceID);
        [DllImport("nekara.dll")]
        public static extern void BlockedOnAnyResource(IntPtr ip, int[] _resourceID, int _size);
        [DllImport("nekara.dll")]
        public static extern void BlockedOnResource(IntPtr ip, int _resourceID);
        [DllImport("nekara.dll")]
        public static extern bool GetNextBoolean(IntPtr ip);
        [DllImport("nekara.dll")]
        public static extern int GetNextInteger(IntPtr ip, int _maxvalue);
        [DllImport("nekara.dll")]
        public static extern void WaitforMainOperation(IntPtr ip);

        internal IntPtr ns_handle;

        public TestRuntimeApi()
        {
           
        }

        public void CreateSession()
        {
            ns_handle = CreateScheduler();
        }

        public void Attach()
        {
            Attach(ns_handle);
        }

        public void Detach()
        {
            Detach(ns_handle);
        }

        public bool IsDetached()
        {
            return IsDetached(ns_handle);
        }

        public void CreateTask()
        {
            CreateOperation(ns_handle);
        }

        public void StartTask(int taskId)
        {
            StartOperation(ns_handle, taskId);
        }

        public void EndTask(int taskId)
        {
            EndOperation(ns_handle, taskId);
        }

        public void CreateResource(int resourceId)
        {
            CreateResource(ns_handle, resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            DeleteResource(ns_handle, resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            BlockedOnResource(ns_handle, resourceId);
        }

        public void BlockedOnAnyResource(params int[] resourceIds)
        {
            BlockedOnAnyResource(ns_handle, resourceIds, resourceIds.Length);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            SignalUpdatedResource(ns_handle, resourceId);
        }

        public bool CreateNondetBool()
        {
            return GetNextBoolean(ns_handle);
        }

        public int CreateNondetInteger(int maxValue)
        {
            return GetNextInteger(ns_handle, maxValue);
        }

        public void Assert(bool predicate, string s)
        {
            if (!predicate)
            {
                // TODO: Replacement code-to be written
                throw new AssertionFailureException(s);
            }
        }

        public void ContextSwitch()
        {
            ScheduleNextOperation(ns_handle);
        }

        public string WaitForMainTask()
        {
            WaitforMainOperation(ns_handle);

            return "";
        }
    }
}
