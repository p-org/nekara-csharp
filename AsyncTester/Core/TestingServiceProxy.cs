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
        string sessionId;   // analogous to topLevelMachineId - used to identify the top-level test session object

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public TestingServiceProxy(IClient socket)
        {
            this.socket = socket;
            this.testingAPI = new TestRuntimeAPI(socket);
        }

        /* API for managing test sessions */
        public Promise LoadTestSubject(string path)
        {
            // Communicate to the server here to notify the start of a test and initialize the test session
            return new Promise((resolve, reject) =>
            {
                Console.WriteLine("Loading program at {0}", path);
                assembly = Assembly.LoadFrom(path);
                this.socket.SendRequest("InitializeTestSession", new JArray(new[] { assembly.FullName }))
                    .ContinueWith(prev => resolve(prev.Result));
            }).Then(sessionId =>
            {
                this.sessionId = sessionId.ToString();
                Console.WriteLine("Session Id : {0}", sessionId);
                // service = new TestingServiceProxy(sessionId.ToString());
                return null;
            });
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
            // Invoke the main test function, passing in the API
            return Promise.Resolve(testMethod.Invoke(null, new[] { this.testingAPI }));
        }
    }

    public class TestRuntimeAPI : ITestingService
    {
        private IClient socket;
        TaskCompletionSource<bool> IterFinished;

        public TestRuntimeAPI(IClient socket)
        {
            this.socket = socket;
            this.IterFinished = new TaskCompletionSource<bool>();
        }

        // ad-hoc Assert method - because there is no actual MachineRuntime in the client side
        public void Assert(bool predicate, string s)
        {
            if (!predicate)
            {
                throw new AssertionFailureException();
            }
        }

        public void CreateTask()
        {
            Console.WriteLine("CreateTask\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.socket.SendRequest("CreateTask").Wait();
        }

        public void StartTask(int taskId)
        {
            Console.WriteLine("StartTask {2}\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            this.socket.SendRequest("StartTask", taskId).Wait();
        }

        public void EndTask(int taskId)
        {
            Console.WriteLine("EndTask {2}\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            this.socket.SendRequest("EndTask", taskId).Wait();
        }

        void WaitForPendingTaskCreations()
        {
            Console.WriteLine("WaitForPendingTaskCreations\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.socket.SendRequest("WaitForPendingTaskCreations").Wait();
        }

        public void ContextSwitch()
        {
            Console.WriteLine("ContextSwitch\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.socket.SendRequest("ContextSwitch").Wait();
        }

        public void BlockedOnResource(int resourceId)
        {
            Console.WriteLine("BlockedOnResource {2}\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            this.socket.SendRequest("BlockedOnResource", resourceId).Wait();
        }

        public void SignalUpdatedResource(int resourceId)
        {
            Console.WriteLine("SignalUpdateResource {2}\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            this.socket.SendRequest("SignalUpdateResource", resourceId).Wait();
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
            Console.WriteLine("CreateNondetInteger\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var request = this.socket.SendRequest("CreateNondetInteger");
            request.Wait();
            return request.Result.ToObject<int>();
        }

        public void CreateResource(int resourceId)
        {
            Console.WriteLine("CreateResource {2}\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            this.socket.SendRequest("CreateResource", resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            Console.WriteLine("DeleteResource {2}\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            this.socket.SendRequest("DeleteResource", resourceId);
        }

        public async Task IsFinished()
        {
            Console.WriteLine("Test Finished\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            await IterFinished.Task;
        }
    }
}
