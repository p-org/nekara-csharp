using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using Nekara.Core;

namespace Nekara.Client
{
    // This is the client-side proxy of the tester service.
    // Used when the system is operating in a network setting.
    public class NekaraClient
    {
        public IClient socket;
        private TestRuntimeApi testingApi;
        private Helpers.UniqueIdGenerator idGen;
        private Dictionary<string, TaskCompletionSource<bool>> sessions;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public NekaraClient(IClient socket)
        {
            this.socket = socket;
            this.testingApi = new TestRuntimeApi(socket);
            this.idGen = new Helpers.UniqueIdGenerator();

            this.sessions = new Dictionary<string, TaskCompletionSource<bool>>();

            // FinishTest will be called during the test if an assert fails
            // or the test runs to completion.
            this.socket.AddRemoteMethod("FinishTest", args =>
            {
                string sid = args[0].ToString();
                bool passed = args[1].ToObject<bool>();
                string reason = args[2].ToObject<string>();
                
                if (this.sessions.ContainsKey(sid))
                {
                    // this.EndTest(sid, true);                    
                    
                    this.testingApi.Finish();   // clean up

                    Console.WriteLine("\n==========[ Test {0} {1} ]==========\n", sid, passed ? "PASSED" : "FAILED");
                    if (reason != "") Console.WriteLine(reason);

                    this.sessions[sid].SetResult(true);

                    return Task.FromResult(JToken.FromObject(true));
                }
                else return Task.FromResult(JToken.FromObject(false));
            });
        }

        public ITestingService Api { get { return this.testingApi; } }
        public Helpers.UniqueIdGenerator IdGen { get { return this.idGen; } }
        
        /* API for managing test sessions */
        public List<MethodInfo> ListTestMethods(Assembly assembly)
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
                throw new TestMethodLoadFailureException($"Failed to load assembly '{assembly.FullName}'");
            }

            if (testMethods.Count == 0)
            {
                Console.WriteLine("Did not find any test method");
                throw new TestMethodLoadFailureException("Did not find any test method");
            }

            return testMethods;
        }

        public MethodInfo GetMethodToBeTested(Assembly assembly, string typeName, string methodName)
        {
            var testMethods = ListTestMethods(assembly);

            var testMethod = testMethods.Find(info => info.DeclaringType.Name == typeName && info.Name == methodName);

            if (testMethod == null)
            {
                Console.WriteLine("Test method {0} not found", methodName);
                Console.WriteLine("Choose one of:\n{0}", String.Join("\n", testMethods.Select(info => $"\t {info.DeclaringType.Name}.{info.Name}")));
                throw new TestMethodNotFoundException($"Test method {methodName} not found");
            }

            if (testMethod.GetParameters().Length != 1 ||
                testMethod.GetParameters()[0].ParameterType != typeof(NekaraClient))
            {
                Console.WriteLine("Incorrect signature of the test method");
                throw new TestMethodLoadFailureException("Incorrect signature of the test method");
            }

            return testMethod;
        }

        public Promise RunTest(MethodInfo testMethod, int schedulingSeed = 0)
        {
            var assembly = testMethod.DeclaringType.Assembly;

            // Communicate to the server here to notify the start of a test
            // and initialize the test session (receive a session ID)
            return new Promise((resolve, reject) =>
            {
                Console.WriteLine("\n\nStarting new test session with seed = {0}", schedulingSeed);
                var (request, canceller) = this.socket.SendRequest("InitializeTestSession", new JToken[] { assembly.FullName, assembly.Location, testMethod.DeclaringType.FullName, testMethod.Name, schedulingSeed });
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(sessionId =>
            {
                string sid = sessionId.ToString();

                var session = this.StartSession(sid, testMethod);
                
                session.Wait();

                return null;
            });
        }

        public Task StartSession(string sessionId, MethodInfo testMethod)
        {
            var tcs = new TaskCompletionSource<bool>();
            this.sessions.Add(sessionId, tcs);

            Console.WriteLine("Session Id : {0}", sessionId);
            this.testingApi.SetSessionId(sessionId);

            // Create a main task so that we have control over
            // any exception thrown by arbitrary user code
            var mainTask = new Promise((resolve, reject) =>
            {
                var t = Task.Factory.StartNew(() =>
                {
                    testMethod.Invoke(null, null);
                }, TaskCreationOptions.AttachedToParent);
                
                t.ContinueWith(prev =>
                {
                    this.testingApi.EndTask(0);
                    resolve(null);
                }, TaskContinuationOptions.OnlyOnRanToCompletion);

                t.ContinueWith(prev =>
                {
                    Console.WriteLine("  [NekaraClient.StartSession] Main Task threw an Exception!\n{0}", prev.Exception.Message);
                    // do nothing
                    resolve(null);
                }, TaskContinuationOptions.OnlyOnFaulted);
            });

            var finished = Task.WhenAll(tcs.Task, mainTask.Task);

            return finished;
        }

        public async Task<bool> IsFinished(string sessionId)
        {
            return await this.sessions[sessionId].Task;
        }

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

                var assembly = Assembly.LoadFrom(info.assemblyPath);
                var testMethod = GetMethodToBeTested(assembly, info.methodDeclaringClass, info.methodName);

                var session = this.StartSession(info.id, testMethod);

                session.Wait();

                return null;
            }).Task;
        }
    }
}
