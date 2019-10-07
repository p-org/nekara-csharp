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
        public IClient socket;
        public TestRuntimeAPI testingAPI;
        // string sessionId;   // analogous to topLevelMachineId - used to identify the top-level test session object
        private Dictionary<string, TaskCompletionSource<bool>> sessions;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public TestingServiceProxy(IClient socket)
        {
            this.socket = socket;
            this.testingAPI = new TestRuntimeAPI(socket);
            this.sessions = new Dictionary<string, TaskCompletionSource<bool>>();

            // the methods below are called by the server

            // FinishTest will be called during the test if an assert fails
            // or the test runs to completion.
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

        public List<MethodInfo> ListTestMethods()
        {
            List<MethodInfo> testMethods = null;

            try
            {
                testMethods = assembly.GetTypes().SelectMany(t => t.GetMethods())
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

            return testMethods;
        }

        public MethodInfo GetMethodToBeTested(string methodName = "")
        {
            // find test method
            List<MethodInfo> testMethods = null;
            // var bindingFlags = BindingFlags.Default; // BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod;

            try
            {
                testMethods = assembly.GetTypes().SelectMany(t => t.GetMethods())
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

        public Promise RunTest(Assembly assembly, MethodInfo testMethod, int schedulingSeed = 0)
        {
            this.assembly = assembly;
            return this.RunTest(testMethod, schedulingSeed);
        }

        public Promise RunTest(MethodInfo testMethod, int schedulingSeed = 0)
        {
            // Communicate to the server here to notify the start of a test and initialize the test session
            return new Promise((resolve, reject) =>
            {
                var request = this.socket.SendRequest("InitializeTestSession", new JArray(new[] { assembly.FullName, assembly.Location, testMethod.Name }));
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
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

        public async Task<bool> IsFinished(string sessionId)
        {
            return await this.sessions[sessionId].Task;
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

        public Task ReplayTestSession(string sessionId)
        {
            return new Promise((resolve, reject) =>
            {
                var request = this.socket.SendRequest("ReplayTestSession", new JArray(new[] { sessionId }));
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(payload =>
            {
                // From here we don't have static typing.
                // The code below can throw arbitrary exceptions
                // if the JSON payload format changes
                JObject info = (JObject)payload;

                string path = info["assemblyPath"].ToObject<string>();
                string methodName = info["methodName"].ToObject<string>();

                Console.WriteLine("Session Id : {0}", sessionId);
                Console.WriteLine("    [{1}] in {0}", info["assemblyName"], methodName);

                LoadTestSubject(path);
                var testMethod = GetMethodToBeTested(methodName);

                this.sessions[sessionId] = new TaskCompletionSource<bool>();                

                this.testingAPI.SetSessionId(sessionId);

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
                    this.sessions[sessionId].SetException(ex);
                }

                this.sessions[sessionId].Task.Wait();

                Console.WriteLine("  ... Deleting Test Session {0}", sessionId);

                this.sessions.Remove(sessionId);

                return null;
            }).task;
        }
    }

    public class TestRuntimeAPI : ITestingService
    {
        private static int gCount = 0;

        public IClient socket;
        public string sessionId;
        private int count;

        public TestRuntimeAPI(IClient socket)
        {
            this.socket = socket;
            this.sessionId = null;
            this.count = 0;
        }

        // Called by the parent object (TestingServiceProxy) to give 
        // control to the RuntimeAPI to stop the test
        public void SetSessionId(string sessionId)
        {
            this.sessionId = sessionId;
            this.count = 0;
        }

        private void InvokeAndHandleException(Action action, string func = "Anonymous Function")
        {
            try
            {
                action();
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("AggregateException caught during {0}!", func);
                aex.Handle(ex => {
                    if (ex is TestingServiceException)
                    {
                        Console.WriteLine("    !!! Exception during test: {0}", ex.Message);
                        this.AcknowledgeServerThrownException(ex.Message);
                        return true;
                    }
                    else if (ex is LogisticalException)
                    {
                        Console.WriteLine(ex.Message);
                        return false;
                    }
                    else return false;
                });
            }
        }

        public void AcknowledgeServerThrownException(string message)
        {
            Console.WriteLine("{0}\tAcknowledgeServerThrownException()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.socket.SendRequest("AcknowledgeServerThrownException", message).Wait();
            Console.WriteLine("{0}\tAcknowledgeServerThrownException()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        // ad-hoc Assert method - because there is no actual MachineRuntime in the client side
        public void Assert(bool predicate, string s)
        {
            Console.WriteLine("{0}\tAssert()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("Assert", predicate, s).Wait(), "Assert");
            Console.WriteLine("{0}\tAssert()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void CreateTask()
        {
            Console.WriteLine("{0}\tCreateTask()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("CreateTask").Wait(), "CreateTask");
            Console.WriteLine("{0}\tCreateTask()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

        }

        public void StartTask(int taskId)
        {
            Console.WriteLine("{0}\tStartTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            InvokeAndHandleException(() => this.socket.SendRequest("StartTask", taskId).Wait(), "StartTask");
            Console.WriteLine("{0}\tStartTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        public void EndTask(int taskId)
        {
            Console.WriteLine("{0}\tEndTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            InvokeAndHandleException(() => this.socket.SendRequest("EndTask", taskId).Wait(), "ContextSwitch");
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
            InvokeAndHandleException(() => this.socket.SendRequest("ContextSwitch").Wait(), "ContextSwitch");
            Console.WriteLine("{0}\tContextSwitch()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void BlockedOnResource(int resourceId)
        {
            Console.WriteLine("{0}\tBlockedOnResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("BlockedOnResource", resourceId).Wait(), "BlockedOnResource");
            Console.WriteLine("{0}\tBlockedOnResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("SignalUpdatedResource", resourceId).Wait(), "SignalUpdatedResource");
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
            InvokeAndHandleException(() => this.socket.SendRequest("CreateResource", resourceId), "CreateResource");
            Console.WriteLine("{0}\tCreateResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            Console.WriteLine("{0}\tDeleteResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("DeleteResource", resourceId), "DeleteResource");
            Console.WriteLine("{0}\tDeleteResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }
    }
}