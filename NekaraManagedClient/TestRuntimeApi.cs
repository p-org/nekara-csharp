using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Nekara;

namespace NekaraManaged.Client
{
    public class TestRuntimeApi : ITestingService
    {
        [DllImport("NekaraCore.dll")]
        public static extern void NS_WithoutSeed(int max_decisions);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_WithSeed(int _seed, int max_decisions);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_CreateTask();
        [DllImport("NekaraCore.dll")]
        public static extern void NS_StartTask(int _threadID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_EndTask(int _threadID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_ContextSwitch();
        [DllImport("NekaraCore.dll")]
        public static extern void NS_WaitforMainTask();
        [DllImport("NekaraCore.dll")]
        public static extern void NS_CreateResource(int _resourceID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_DeleteResource(int _resourceID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_SignalUpdatedResource(int _resourceID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_BlockedOnAnyResource(int[] _resourceID, int _size);
        [DllImport("NekaraCore.dll")]
        public static extern int NS_GenerateResourceID();
        [DllImport("NekaraCore.dll")]
        public static extern int NS_GenerateThreadTD();
        [DllImport("NekaraCore.dll")]
        public static extern bool NS_CreateNondetBool();
        [DllImport("NekaraCore.dll")]
        public static extern int NS_CreateNondetInteger(int _maxvalue);
        [DllImport("NekaraCore.dll")]
        public static extern bool NS_Dispose();
        [DllImport("NekaraCore.dll")]
        public static extern void NS_BlockedOnResource(int _resourceID);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_Test_forCS();
        [DllImport("NekaraCore.dll")]
        public static extern int NS_Test_Get_Seed();

        public TestRuntimeApi()
        {
           
        }

        public void CreateSession(int max_decisions)
        {
            NS_WithoutSeed(max_decisions);
        }

        public void CreateSessionWithSeed(int _seed, int max_decisions)
        {
            NS_WithSeed(_seed, max_decisions);
        }

        public void CreateTask()
        {
            NS_CreateTask();
        }

        public void StartTask(int taskId)
        {
            NS_StartTask(taskId);
        }

        public void EndTask(int taskId)
        {
            NS_EndTask(taskId);
        }

        public void CreateResource(int resourceId)
        {
            NS_CreateResource(resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            NS_DeleteResource(resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            NS_BlockedOnResource(resourceId);
        }

        public void BlockedOnAnyResource(params int[] resourceIds)
        {
            NS_BlockedOnAnyResource(resourceIds, resourceIds.Length);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            NS_SignalUpdatedResource(resourceId);
        }

        public bool CreateNondetBool()
        {
            return NS_CreateNondetBool();
        }

        public int CreateNondetInteger(int maxValue)
        {
            return NS_CreateNondetInteger(maxValue);
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
            NS_ContextSwitch();
        }

        public string WaitForMainTask()
        {
            NS_WaitforMainTask();

            return "";
        }
    }
}
