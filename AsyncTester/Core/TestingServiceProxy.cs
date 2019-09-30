using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using Microsoft.PSharp;
using Microsoft.PSharp.TestingServices;
using Grpc.Core.Logging;
using AsyncTester.Networking;
using Newtonsoft.Json.Linq;

namespace AsyncTester.Core
{
    /// <summary>
    /// Attribute for declaring the entry point to
    /// a program test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestMethodAttribute : Attribute
    {
    }

    // This is the client-side proxy of the tester service.
    // Used when the system is operating in a network setting.
    public class TestingServiceProxy
    {
        private Assembly assembly;
        private IClient socket;
        private TestRuntimeAPI testingAPI;
        // string sessionId;   // analogous to topLevelMachineId - used to identify the top-level test session object
        private Dictionary<string, TaskCompletionSource<bool>> sessions;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public TestingServiceProxy(IClient socket)
        {
            this.socket = socket;
            this.testingAPI = new TestRuntimeAPI(socket);
            this.sessions = new Dictionary<string, TaskCompletionSource<bool>>();

            this.socket.AddRemoteMethod("FinishTest", args =>
            {
                Console.WriteLine("Test FINISHED");

                string sid = args[0].ToString();
                if (this.sessions.ContainsKey(sid))
                {
                    // this.EndTest(sid, true);
                    this.sessions[sid].SetResult(true);
                    return Task.FromResult(JToken.FromObject(true));
                }
                else return Task.FromResult(JToken.FromObject(false));
            });
        }

        /* API for managing test sessions */
        public void LoadTestSubject(string path)
        {
            Console.WriteLine("Loading program at {0}", path);
            assembly = Assembly.LoadFrom(path);
        }

        public MethodInfo GetMethodToBeTested(string methodName = "")
        {
            // find test method
            List<MethodInfo> testMethods = null;
            var bindingFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod;

            try
            {
                testMethods = assembly.GetTypes().SelectMany(t => t.GetMethods(bindingFlags))
                    .Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), false).Length > 0).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var le in ex.LoaderExceptions)
                {
                    Console.WriteLine("{0}", le.Message);
                }

                Console.WriteLine($"Failed to load assembly '{assembly.FullName}'");
                throw new TestMethodLoadFailureException();
            }

            if (testMethods.Count == 0)
            {
                Console.WriteLine("Did not find any test method");
                throw new TestMethodLoadFailureException();
            }

            if (testMethods.Count > 1)
            {
                Console.WriteLine("Found multiple test methods");
                foreach (var tm in testMethods)
                {
                    Console.WriteLine("Method: {0}", tm.Name);
                }
                Console.WriteLine("Only one test method supported");
                throw new TestMethodLoadFailureException();
            }

            var testMethod = testMethods[0];

            if (testMethod.GetParameters().Length != 1 ||
                testMethod.GetParameters()[0].ParameterType != typeof(ITestingService))
            {
                Console.WriteLine("Incorrect signature of the test method");
                throw new TestMethodLoadFailureException();
            }

            return testMethod;
        }

        public Promise RunTest(MethodInfo testMethod, int schedulingSeed = 0)
        {
            // Communicate to the server here to notify the start of a test and initialize the test session
            return new Promise((resolve, reject) =>
            {
                this.socket.SendRequest("InitializeTestSession", new JArray(new[] { assembly.FullName }))
                    .ContinueWith(prev => resolve(prev.Result));
            }).Then(sessionId =>
            {
                string sid = sessionId.ToString();                
                this.sessions.Add(sid, new TaskCompletionSource<bool>());

                Console.WriteLine("Session Id : {0}", sid);

                this.testingAPI.SetSessionId(sid);

                try
                {
                    // Invoke the main test function, passing in the API
                    testMethod.Invoke(null, new[] { this.testingAPI });

                    // by this time server should have initialized main task 0
                    this.testingAPI.EndTask(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception was Caught during test!");
                    this.sessions[sid].SetException(ex);
                }

                this.sessions[sid].Task.Wait();

                Console.WriteLine("  ... Deleting Test Session {0}", sid);

                this.sessions.Remove(sid);

                return null;
            });
        }

        /*public void EndTest(string sessionId, bool passed)
        {
            this.sessions[sessionId].SetResult(passed);
        }*/

        /*public async Task IsFinished()
        {
            Console.WriteLine("{0}\tIsFinished\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            await IterFinished.Task;
        }*/
    }

    public class TestRuntimeAPI : ITestingService
    {
        private IClient socket;
        private string sessionId;

        private int count = 0;

        public TestRuntimeAPI(IClient socket)
        {
            this.socket = socket;
            this.sessionId = null;
        }

        // Called by the parent object (TestingServiceProxy) to give 
        // control to the RuntimeAPI to stop the test
        public void SetSessionId(string sessionId)
        {
            this.sessionId = sessionId;
        }

        public void AcknowledgeTestTimeException(string message)
        {
            Console.WriteLine("{0}\tAcknowledgeTestTimeException()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.socket.SendRequest("AcknowledgeTestTimeException", message).Wait();
            Console.WriteLine("{0}\tAcknowledgeTestTimeException()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        // ad-hoc Assert method - because there is no actual MachineRuntime in the client side
        public void Assert(bool predicate, string s)
        {
            Console.WriteLine("{0}\tAssert()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            try
            {
                this.socket.SendRequest("Assert", predicate, s).Wait();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                Console.WriteLine("Exception caught during Assert!\n{0}", messages);
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tAssert()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void CreateTask()
        {
            Console.WriteLine("{0}\tCreateTask()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            try
            {
                this.socket.SendRequest("CreateTask").Wait();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during CreateTask!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tCreateTask()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

        }

        public void StartTask(int taskId)
        {
            Console.WriteLine("{0}\tStartTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            try
            {
                this.socket.SendRequest("StartTask", taskId).Wait();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during StartTask!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tStartTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        public void EndTask(int taskId)
        {
            Console.WriteLine("{0}\tEndTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            try
            {
                this.socket.SendRequest("EndTask", taskId).Wait();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during EndTask!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tEndTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        // this is never called by the client-side
        /*void WaitForPendingTaskCreations()
        {
            Console.WriteLine("{0}\tWaitForPendingTaskCreations()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.socket.SendRequest("WaitForPendingTaskCreations").Wait();
            Console.WriteLine("{0}\tWaitForPendingTaskCreations()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }*/

        public void ContextSwitch()
        {
            Console.WriteLine("{0}\tContextSwitch()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            try
            {
                this.socket.SendRequest("ContextSwitch").Wait();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during ContextSwitch!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tContextSwitch()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void BlockedOnResource(int resourceId)
        {
            Console.WriteLine("{0}\tBlockedOnResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            try
            {
                this.socket.SendRequest("BlockedOnResource", resourceId).Wait();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during BlockedOnResource!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tBlockedOnResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            try
            {
                this.socket.SendRequest("SignalUpdateResource", resourceId).Wait();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during SignalUpdateResource!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public bool CreateNondetBool()
        {
            Console.WriteLine("CreateNondetBool\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var request = this.socket.SendRequest("CreateNondetBool");
            request.Wait();
            return request.Result.ToObject<bool>();
        }

        public int CreateNondetInteger(int maxValue)
        {
            Console.WriteLine("{0}\tCreateNondetInteger()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var request = this.socket.SendRequest("CreateNondetInteger");
            request.Wait();
            return request.Result.ToObject<int>();
        }

        public void CreateResource(int resourceId)
        {
            Console.WriteLine("{0}\tCreateResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            try
            {
                this.socket.SendRequest("CreateResource", resourceId);
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during CreateResource!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tCreateResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            Console.WriteLine("{0}\tDeleteResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            try
            {
                this.socket.SendRequest("DeleteResource", resourceId);
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("Exception caught during DeleteResource!\n{0}", aex.Message);
                string messages = String.Join(",", aex.Flatten().InnerExceptions.Select(ex => ex.Message));
                this.AcknowledgeTestTimeException(messages);
            }
            Console.WriteLine("{0}\tDeleteResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }
    }
}