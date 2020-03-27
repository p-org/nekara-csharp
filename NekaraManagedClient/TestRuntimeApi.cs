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
        [DllImport("NekaraCore.dll")]
        public static extern IntPtr NS_NekaraService();
        [DllImport("NekaraCore.dll")]
        public static extern void NS_CreateTask(IntPtr ip);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_Attach(IntPtr ip);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_Detach(IntPtr ip);
        [DllImport("NekaraCore.dll")]
        public static extern bool NS_IsDetached(IntPtr ip);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_StartTask(IntPtr ip, int _threadID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_EndTask(IntPtr ip, int _threadID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_ContextSwitch(IntPtr ip);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_WaitforMainTask(IntPtr ip);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_CreateResource(IntPtr ip, int _resourceID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_DeleteResource(IntPtr ip, int _resourceID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_SignalUpdatedResource(IntPtr ip, int _resourceID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_BlockedOnAnyResource(IntPtr ip, int[] _resourceID, int _size);
        [DllImport("NekaraCore.dll")]
        public static extern bool NS_CreateNondetBool(IntPtr ip);
        [DllImport("NekaraCore.dll")]
        public static extern int NS_CreateNondetInteger(IntPtr ip, int _maxvalue);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_BlockedOnResource(IntPtr ip, int _resourceID);

        internal IntPtr ns_handle;

        public TestRuntimeApi()
        {
           
        }

        public void CreateSession()
        {
            ns_handle =  NS_NekaraService();
        }

        public void Attach()
        {
            NS_Attach(ns_handle);
        }

        public void Detach()
        {
            NS_Detach(ns_handle);
        }

        public bool IsDetached()
        {
            return NS_IsDetached(ns_handle);
        }

        public void CreateTask()
        {
            NS_CreateTask(ns_handle);
        }

        public void StartTask(int taskId)
        {
            NS_StartTask(ns_handle, taskId);
        }

        public void EndTask(int taskId)
        {
            NS_EndTask(ns_handle, taskId);
        }

        public void CreateResource(int resourceId)
        {
            NS_CreateResource(ns_handle, resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            NS_DeleteResource(ns_handle, resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            NS_BlockedOnResource(ns_handle, resourceId);
        }

        public void BlockedOnAnyResource(params int[] resourceIds)
        {
            NS_BlockedOnAnyResource(ns_handle, resourceIds, resourceIds.Length);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            NS_SignalUpdatedResource(ns_handle, resourceId);
        }

        public bool CreateNondetBool()
        {
            return NS_CreateNondetBool(ns_handle);
        }

        public int CreateNondetInteger(int maxValue)
        {
            return NS_CreateNondetInteger(ns_handle, maxValue);
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
            NS_ContextSwitch(ns_handle);
        }

        public string WaitForMainTask()
        {
            NS_WaitforMainTask(ns_handle);

            return "";
        }
    }
}
