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
using System.Collections;
using System.Runtime.CompilerServices;

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
        private Dictionary<string, Queue<Task>> requestQueues;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public TestingServiceProxy(IClient socket)
        {
            this.socket = socket;
            this.testingAPI = new TestRuntimeAPI(socket);
            this.sessions = new Dictionary<string, TaskCompletionSource<bool>>();
            this.requestQueues = new Dictionary<string, Queue<Task>>();

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

            var testMethod = testMethods.Find(info => info.Name == methodName);

            /*if (testMethods.Count > 1)
            {
                Console.WriteLine("Found multiple test methods");
                foreach (var tm in testMethods)
                {
                    Console.WriteLine("Method: {0}", tm.Name);
                }
                Console.WriteLine("Only one test method supported");
                throw new TestMethodLoadFailureException();
            }*/

            // var testMethod = testMethods[0];

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
                Console.WriteLine("\n\nStarting new test session with seed = {0}", schedulingSeed);
                var request = this.socket.SendRequest("InitializeTestSession", new JToken[] { assembly.FullName, assembly.Location, testMethod.Name, schedulingSeed });
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(sessionId =>
            {
                string sid = sessionId.ToString();
                this.sessions.Add(sid, new TaskCompletionSource<bool>());
                this.requestQueues.Add(sid, new Queue<Task>());

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
                // cancel all pending requests
                // this.requestQueues[sid].Select<Task, void>(t => t.Wait());
                this.requestQueues.Remove(sid);

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
                var request = this.socket.SendRequest("ReplayTestSession", new JToken[] { sessionId });
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(payload =>
            {
                // From here we don't have static typing.
                // The code below can throw arbitrary exceptions
                // if the JSON payload format changes
                JObject data = (JObject)payload;
                SessionInfo info = new SessionInfo(data["id"].ToObject<string>(), data["assemblyName"].ToObject<string>(), data["assemblyPath"].ToObject<string>(), data["methodName"].ToObject<string>(), data["schedulingSeed"].ToObject<int>());
                
                Console.WriteLine("Session Id : {0}", info.id);
                Console.WriteLine("    [{1}] in {0}", info.assemblyName, info.methodName);

                LoadTestSubject(info.assemblyPath);
                var testMethod = GetMethodToBeTested(info.methodName);

                this.sessions.Add(info.id, new TaskCompletionSource<bool>());
                this.requestQueues.Add(info.id, new Queue<Task>());

                Console.WriteLine("Session Id : {0}", info.id);

                this.testingAPI.SetSessionId(info.id);

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
                    this.sessions[info.id].SetException(ex);
                }

                this.sessions[info.id].Task.Wait();

                Console.WriteLine("  ... Deleting Test Session {0}", info.id);

                this.sessions.Remove(info.id);
                // cancel all pending requests
                // this.requestQueues[sid].Select<Task, void>(t => t.Wait());
                this.requestQueues.Remove(info.id);

                return null;
            }).task;
        }
    }

    public class TestRuntimeLock : IAsyncLock
    {
        private TestRuntimeAPI api;
        private int id;
        private bool locked;
        public TestRuntimeLock(TestRuntimeAPI api, int resourceId, string label = "")
        {
            this.api = api;
            this.id = resourceId;
            this.locked = false;

            this.api.CreateResource(resourceId);
        }

        public void Acquire()
        {
            this.api.ContextSwitch();
            while (true)
            {
                if (this.locked == false)
                {
                    this.locked = true;
                    break;
                }
                else
                {
                    this.api.BlockedOnResource(this.id);
                    continue;
                }
            }
        }

        public void Release()
        {
            this.api.Assert(this.locked == true, "Release called on non-acquired lock");

            this.locked = false;
            this.api.SignalUpdatedResource(this.id);
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
            this.socket.SendRequest("AcknowledgeServerThrownException", this.sessionId, message).Wait();
            Console.WriteLine("{0}\tAcknowledgeServerThrownException()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        /* API methods */
        public void CreateTask()
        {
            Console.WriteLine("{0}\tCreateTask()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("CreateTask", this.sessionId).Wait(), "CreateTask");
            Console.WriteLine("{0}\tCreateTask()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

        }

        public void StartTask(int taskId)
        {
            Console.WriteLine("{0}\tStartTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            InvokeAndHandleException(() => this.socket.SendRequest("StartTask", this.sessionId, taskId).Wait(), "StartTask");
            Console.WriteLine("{0}\tStartTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        public void EndTask(int taskId)
        {
            Console.WriteLine("{0}\tEndTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            InvokeAndHandleException(() => this.socket.SendRequest("EndTask", this.sessionId, taskId).Wait(), "ContextSwitch");
            Console.WriteLine("{0}\tEndTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        // this is never called by the client-side
        /*void WaitForPendingTaskCreations()
        {
            Console.WriteLine("{0}\tWaitForPendingTaskCreations()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.socket.SendRequest("WaitForPendingTaskCreations").Wait();
            Console.WriteLine("{0}\tWaitForPendingTaskCreations()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }*/

        public IAsyncLock CreateLock(int resourceId)
        {
            return new TestRuntimeLock(this, resourceId);
        }

        public void CreateResource(int resourceId)
        {
            Console.WriteLine("{0}\tCreateResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("CreateResource", this.sessionId, resourceId), "CreateResource");
            Console.WriteLine("{0}\tCreateResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            Console.WriteLine("{0}\tDeleteResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("DeleteResource", this.sessionId, resourceId), "DeleteResource");
            Console.WriteLine("{0}\tDeleteResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            Console.WriteLine("{0}\tBlockedOnResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("BlockedOnResource", this.sessionId, resourceId).Wait(), "BlockedOnResource");
            Console.WriteLine("{0}\tBlockedOnResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("SignalUpdatedResource", this.sessionId, resourceId).Wait(), "SignalUpdatedResource");
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public bool CreateNondetBool()
        {
            Console.WriteLine("CreateNondetBool\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var request = this.socket.SendRequest("CreateNondetBool", this.sessionId);
            request.Wait();
            return request.Result.ToObject<bool>();
        }

        public int CreateNondetInteger(int maxValue)
        {
            Console.WriteLine("{0}\tCreateNondetInteger()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var request = this.socket.SendRequest("CreateNondetInteger", this.sessionId);
            request.Wait();
            return request.Result.ToObject<int>();
        }

        public void Assert(bool predicate, string s)
        {
            Console.WriteLine("{0}\tAssert()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("Assert", this.sessionId, predicate, s).Wait(), "Assert");
            Console.WriteLine("{0}\tAssert()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void ContextSwitch()
        {
            Console.WriteLine("{0}\tContextSwitch()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("ContextSwitch", this.sessionId).Wait(), "ContextSwitch");
            Console.WriteLine("{0}\tContextSwitch()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
    }
}