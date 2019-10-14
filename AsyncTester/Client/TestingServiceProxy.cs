using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using AsyncTester.Networking;
using AsyncTester.Core;
using Newtonsoft.Json.Linq;

namespace AsyncTester.Client
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
        public IClient socket;
        private TestRuntimeAPI testingAPI;
        private TestRuntimeLockFactory lockFactory;

        // string sessionId;   // analogous to topLevelMachineId - used to identify the top-level test session object
        private Assembly assembly;
        private Dictionary<string, TaskCompletionSource<bool>> sessions;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public TestingServiceProxy(IClient socket)
        {
            this.socket = socket;
            this.testingAPI = new TestRuntimeAPI(socket);
            this.lockFactory = new TestRuntimeLockFactory(testingAPI);

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

        public ITestingService Api { get { return this.testingAPI; } }
        public IAsyncLockFactory LockFactory { get { return this.lockFactory; } }

        /* API for managing test sessions */
        public void LoadTestSubject(string path)
        {
            Console.WriteLine("Loading program at {0}", path);
            assembly = Assembly.LoadFrom(path);
        }

        public List<MethodInfo> ListTestMethods()
        {
            List<MethodInfo> testMethods = null;
            var bindingFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.InvokeMethod;

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

            return testMethods;
        }

        public MethodInfo GetMethodToBeTested(string methodName = "RunTest")
        {
            var testMethods = ListTestMethods();

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
                var (request, canceller) = this.socket.SendRequest("InitializeTestSession", new JToken[] { assembly.FullName, assembly.Location, testMethod.DeclaringType.FullName, testMethod.Name, schedulingSeed });
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(sessionId =>
            {
                string sid = sessionId.ToString();
                this.sessions.Add(sid, new TaskCompletionSource<bool>());
                //this.requestQueues.Add(sid, new Queue<Task>());

                Console.WriteLine("Session Id : {0}", sid);

                this.testingAPI.SetSessionId(sid);

                try
                {
                    // Invoke the main test function, passing in the API
                    testMethod.Invoke(null, new[] { this });

                    // by this time server should have initialized main task 0
                    this.testingAPI.EndTask(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    this.sessions[sid].SetException(ex);
                }

                this.sessions[sid].Task.Wait();

                Console.WriteLine("  ... Deleting Test Session {0}", sid);

                this.sessions.Remove(sid);
                // cancel all pending requests
                // this.requestQueues[sid].Select<Task, void>(t => t.Wait());
                // this.requestQueues.Remove(sid);

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
                var (request, canceller) = this.socket.SendRequest("ReplayTestSession", new JToken[] { sessionId });
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(payload =>
            {
                // From here we don't have static typing.
                // The code below can throw arbitrary exceptions
                // if the JSON payload format changes
                JObject data = (JObject)payload;
                SessionInfo info = new SessionInfo(data["id"].ToObject<string>(), data["assemblyName"].ToObject<string>(), data["assemblyPath"].ToObject<string>(), data["methodDeclaringClass"].ToObject<string>(), data["methodName"].ToObject<string>(), data["schedulingSeed"].ToObject<int>());
                
                Console.WriteLine("Session Id : {0}", info.id);
                Console.WriteLine("    [{2} .{1}] in {0}", info.assemblyName, info.methodName, info.methodDeclaringClass);

                LoadTestSubject(info.assemblyPath);
                var testMethod = GetMethodToBeTested(info.methodName);

                this.sessions.Add(info.id, new TaskCompletionSource<bool>());
                // this.requestQueues.Add(info.id, new Queue<Task>());

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
                // this.requestQueues.Remove(info.id);

                return null;
            }).task;
        }
    }
}