using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using Nekara.Core;
using System.Runtime.CompilerServices;

namespace Nekara.Client
{
    public struct TestDefinition
    {
        public MethodInfo Setup;
        public MethodInfo Run;
        public MethodInfo Teardown;

        public TestDefinition (MethodInfo Setup, MethodInfo Run, MethodInfo Teardown)
        {
            this.Setup = Setup;
            this.Run = Run;
            this.Teardown = Teardown;
        }
    }

    // This is the client-side proxy of the tester service.
    // Used when the system is operating in a network setting.
    public class NekaraClient : IDisposable
    {
        public IClient socket;
        private TestRuntimeApi testingApi;
        private Helpers.UniqueIdGenerator idGen;
        private Dictionary<string, TaskCompletionSource<bool>> sessions;
        private Dictionary<string, TestResult> results;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public NekaraClient(IClient socket)
        {
            this.socket = socket;
            this.testingApi = new TestRuntimeApi(socket);
            this.idGen = new Helpers.UniqueIdGenerator();

            this.sessions = new Dictionary<string, TaskCompletionSource<bool>>();
            this.results = new Dictionary<string, TestResult>();

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

                    // clean up
                    this.testingApi.Finish();
                    this.idGen.Reset();

                    Console.WriteLine("\n\n==========[ Test {0} {1} ]==========\n", sid, passed ? "PASSED" : "FAILED");
                    if (reason != "")
                    {
                        Console.WriteLine("  " + reason);
                        Console.WriteLine("\n==================================== END ===");
                    }

                    this.sessions[sid].SetResult(true);
                    this.results.Add(sid, new TestResult(passed, reason));
                    
                    this.sessions.Remove(sid);

                    return Task.FromResult(JToken.FromObject(true));
                }
                else return Task.FromResult(JToken.FromObject(false));
            });
        }

        public ITestingService Api { get { return this.testingApi; } }
        public Helpers.UniqueIdGenerator IdGen { get { return this.idGen; } }

        public void PrintTestResults()
        {
            Console.WriteLine("\n\n=====[ Test Result ]===============\n");
            int passed = 0;
            foreach(var item in this.results)
            {
                Console.WriteLine("Session {0}:\t{1}{2}", item.Key, item.Value.passed ? "Pass" : "Fail", item.Value.passed ? "" : "\t(" + item.Value.reason + ")");
                if (item.Value.passed) passed++;
            }
            Console.WriteLine("\n==========[ {0}/{1} Passed ]==========\n", passed, this.results.Count);
        }
        
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

            var testMethod = testMethods.Find(info => info.DeclaringType.FullName == typeName && info.Name == methodName);

            if (testMethod == null)
            {
                Console.WriteLine("Test method {0} .{1} not found", typeName, methodName);
                Console.WriteLine("Choose one of:\n{0}", String.Join("\n", testMethods.Select(info => $"\t {info.DeclaringType.FullName}.{info.Name}")));
                throw new TestMethodNotFoundException($"Test method {methodName} not found");
            }

            if (testMethod.GetParameters().Length != 0)
            {
                Console.WriteLine("Incorrect signature of the test method");
                throw new TestMethodLoadFailureException("Incorrect signature of the test method");
            }

            return testMethod;
        }

        public TestDefinition GetTestDefinition(MethodInfo testMethod)
        {
            var bindingFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.InvokeMethod;
            MethodInfo Setup = null;
            MethodInfo Teardown = null;

            var setupMethods = testMethod.DeclaringType.GetMethods(bindingFlags)
                .Where(m => m.GetCustomAttributes(typeof(TestSetupMethodAttribute), false).Length > 0).ToList();

            if (setupMethods.Count > 0) Setup = setupMethods[0];

            var teardownMethods = testMethod.DeclaringType.GetMethods(bindingFlags)
                .Where(m => m.GetCustomAttributes(typeof(TestTeardownMethodAttribute), false).Length > 0).ToList();

            if (teardownMethods.Count > 0) Teardown = teardownMethods[0];

            return new TestDefinition(Setup, testMethod, Teardown);
        }

        public Promise RunTest(MethodInfo testMethod, int schedulingSeed = 0)
        {
            var assembly = testMethod.DeclaringType.Assembly;

            // Communicate to the server here to notify the start of a test
            // and initialize the test session (receive a session ID)
            return new Promise((resolve, reject) =>
            {
                Console.WriteLine("\n\n============================================");
                Console.WriteLine(">>    Starting new session with seed = {0}", schedulingSeed);
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

        public Promise RunTest(TestDefinition definition, int numIteration)
        {
            return new Promise((resolve, reject) =>
            {
                if (definition.Setup != null) definition.Setup.Invoke(null, null);
                resolve(null);
            }).Then(prev =>
            {
                var run = Helpers.RepeatTask(() => this.RunTest(definition.Run, Helpers.RandomInt()).Task, numIteration);
                run.Wait();
                return null;
            }).Then(prev =>
            {
                if (definition.Teardown != null) definition.Teardown.Invoke(null, null);
                return null;
            });
        }

        public Task StartSession(string sessionId, MethodInfo testMethod)
        {
            var tcs = new TaskCompletionSource<bool>();
            this.sessions.Add(sessionId, tcs);

            Console.WriteLine(">>    Session Id : {0}", sessionId);
            Console.WriteLine("============================================\n");
            this.testingApi.SetSessionId(sessionId);

            // Create a main task so that we have control over
            // any exception thrown by arbitrary user code.
            //
            // The user code could be async or sync.
            // In either case, there could be multiple Tasks throwing exceptions.
            // This is challenging to deal with elegantly, so we simply silence exceptions
            // thrown by child Tasks, and only handle the exception thrown by the top-level Task.
            // See also: https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/
            var mainTask = new Promise((resolve, reject) =>
            {
                Task task;

                if (testMethod.ReturnType == typeof(Task))
                {
                    task = (Task)testMethod.Invoke(null, null);
                    this.testingApi.EndTask(0);
                    try
                    {
                        task.Wait();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("  [NekaraClient.StartSession] Main Task threw an Exception!\n{0}", ex.Message);
                    }
                    finally
                    {
                        resolve(null);
                    }
                }
                else
                {
                    task = Task.Factory.StartNew(() =>
                    {
                        testMethod.Invoke(null, null);

                    }, TaskCreationOptions.AttachedToParent);

                    task.ContinueWith(prev =>
                    {
                        this.testingApi.EndTask(0);
                        resolve(null);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);

                    task.ContinueWith(prev =>
                    {
                        Console.WriteLine("  [NekaraClient.StartSession] Main Task threw an Exception!\n{0}", prev.Exception.Message);
                        // do nothing
                        resolve(null);
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
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
                Console.WriteLine("  [{2} .{1}] in {0}", info.assemblyName, info.methodName, info.methodDeclaringClass);

                var assembly = Assembly.LoadFrom(info.assemblyPath);
                var testMethod = GetMethodToBeTested(assembly, info.methodDeclaringClass, info.methodName);
                var testDefinition = GetTestDefinition(testMethod);

                if (testDefinition.Setup != null) testDefinition.Setup.Invoke(null, null);
                var session = this.StartSession(info.id, testMethod);
                session.Wait();
                if (testDefinition.Teardown != null) testDefinition.Teardown.Invoke(null, null);

                return null;
            }).Task;
        }

        public void Dispose()
        {
            socket.Dispose();
            return;
        }
    }
}
