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
        public static extern void NS_WithoutSeed();
        [DllImport("NekaraCore.dll")]
        public static extern void NS_WithSeed(int _seed);
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

        public void CreateSession()
        {
            // Console.WriteLine("1");
            NS_WithoutSeed();
        }

        public void CreateSessionWithSeed(int _seed)
        {
            // Console.WriteLine("2");
            NS_WithSeed(_seed);
        }

        public void CreateTask()
        {
            // Console.WriteLine("3");
            NS_CreateTask();
        }

        public void StartTask(int taskId)
        {
            // Console.WriteLine("4");
            NS_StartTask(taskId);
        }

        public void EndTask(int taskId)
        {
            // Console.WriteLine("5");
            NS_EndTask(taskId);
        }

        public void CreateResource(int resourceId)
        {
            // Console.WriteLine("6");
            NS_CreateResource(resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            // Console.WriteLine("7");
            NS_DeleteResource(resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            // Console.WriteLine("8");
            NS_BlockedOnResource(resourceId);
        }

        public void BlockedOnAnyResource(params int[] resourceIds)
        {
            // Console.WriteLine("9");
            NS_BlockedOnAnyResource(resourceIds, resourceIds.Length);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            // Console.WriteLine("10");
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
            // Console.WriteLine("11: {0}", predicate);
            if (!predicate)
            {
                // TODO: Replacement code-to be written
                throw new AssertionFailureException(s);
                // Debug.Assert(predicate, s);
                
            }
        }

        public void ContextSwitch()
        {
            // Console.WriteLine("12");
            NS_ContextSwitch();
        }

        public string WaitForMainTask()
        {
            // Console.WriteLine("13");
            NS_WaitforMainTask();

            return "";
        }
    }
}
