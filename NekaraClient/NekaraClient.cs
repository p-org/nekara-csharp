using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using Nekara.Core;
using System.Threading;

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
        // private Dictionary<string, TestResult> results;
        private Dictionary<string, SessionRecord> records;
        private Helpers.UniqueIdGenerator SchedulingSeedGenerator;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public NekaraClient(IClient socket)
        {
            this.socket = socket;
            this.testingApi = new TestRuntimeApi(socket);
            this.SchedulingSeedGenerator = new Helpers.UniqueIdGenerator(false, 0);
            this.TaskIdGenerator = new Helpers.UniqueIdGenerator(true, 1000);
            this.ResourceIdGenerator = new Helpers.UniqueIdGenerator(true, 1000000);

            //this.results = new Dictionary<string, TestResult>();
            this.records = new Dictionary<string, SessionRecord>();
        }

        public ITestingService Api { get { return this.testingApi; } }
        public Helpers.UniqueIdGenerator TaskIdGenerator { get; private set; }
        public Helpers.UniqueIdGenerator ResourceIdGenerator { get; private set; }

        public void PrintTestResults()
        {
            //lock (this.results)
            lock (this.records)
            {
                Console.WriteLine("\n\n=====[ Test Result ]===============\n");
                int passed = 0;
                var passedTime = new List<double>();
                var failedTime = new List<double>();
                foreach (var item in this.records)
                {
                    Console.WriteLine("  {0} (seed = {1}):\t{2} reqs ({3} ms/req)\t{4} decisions\t{5} ms\t{6}{7}", 
                        item.Key, 
                        item.Value.schedulingSeed,
                        item.Value.numRequests,
                        Math.Round((decimal)item.Value.avgInvokeTime, 2),
                        item.Value.numDecisions,
                        Math.Round((decimal)item.Value.elapsedMs, 2),
                        item.Value.passed ? "Pass" : "Fail", 
                        item.Value.passed ? "" : "\n\t\t  (" + item.Value.reason + ")");

                    if (item.Value.passed)
                    {
                        passed++;
                        passedTime.Add(item.Value.elapsedMs);
                    }
                    else
                    {
                        failedTime.Add(item.Value.elapsedMs);
                    }
                }
                Console.WriteLine("\n----------[ {0}/{1} Passed ]----------\n", passed, this.records.Count);
                if (passedTime.Count > 0) Console.WriteLine("    Average Time for Bug-free Sessions:\t{0} ms", Math.Round((decimal)passedTime.Average(), 2));
                if (failedTime.Count > 0) Console.WriteLine("    Average Time for Buggy Sessions:\t{0} ms", Math.Round((decimal)failedTime.Average(), 2));
                Console.WriteLine("\n==========[ {0}/{1} Passed ]==========\n", passed, this.records.Count);
            }
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

        public Promise RunTest(TestDefinition definition, int numIteration,
            int timeoutMs = Constants.SessionTimeoutMs,
            int maxDecisions = Constants.SessionMaxDecisions,
            bool terminateOnFirstFail = false)
        {
            return new Promise((resolve, reject) =>
            {
                if (definition.Setup != null) definition.Setup.Invoke(null, null);
                resolve(null);
            }).Then(prev =>
            {
                var cts = new CancellationTokenSource();
                var run = Helpers.RepeatTask(() => 
                        this.RunNewTestSession(definition.Run, SchedulingSeedGenerator.Generate())
                        .Then(result => this.ReplayTestSession(((SessionRecord)result).sessionId, false))
                        .Then(result => {
                            if (terminateOnFirstFail && !((SessionRecord)result).passed) cts.Cancel();
                            return null;
                        }).Task,
                    numIteration, cts.Token);

                try
                {
                    run.Wait();
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerExceptions[0] is TaskCanceledException && terminateOnFirstFail)
                    {
                        Console.WriteLine("Bug found! Suspending further tests because 'terminateOnFirstFail=true'");
                    }
                    else
                    {
                        Console.WriteLine("!!! Unexpected Exception !!!");
                        throw ex;
                    }
                }
                Console.WriteLine(TestRuntimeApi.Profiler.ToString());

                return null;
            }).Then(prev =>
            {
                if (definition.Teardown != null) definition.Teardown.Invoke(null, null);
                return null;
            });
        }

        public Task<object> StartSession(string sessionId, MethodInfo testMethod, int schedulingSeed)
        {
            Console.WriteLine(">>    Session Id : {0}", sessionId);
            Console.WriteLine("============================================\n");
            
            // reset task ID generators
            this.TaskIdGenerator.Reset();
            this.ResourceIdGenerator.Reset();

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
                if (testMethod.ReturnType == typeof(Task))
                {
                    // invoke user method asynchronously
                    Task task = (Task)testMethod.Invoke(null, null);
                    var record = this.testingApi.WaitForMainTask();
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
                        resolve(record);
                    }
                }
                else
                {
                    // invoke user method asynchronously
                    Task task = Task.Factory.StartNew(() =>
                    {
                        testMethod.Invoke(null, null);

                    }, TaskCreationOptions.AttachedToParent);

                    task.ContinueWith(prev =>
                    {
                        Console.WriteLine("  [NekaraClient.StartSession] Main Task threw an Exception!\n{0}", prev.Exception.Message);
                        var record = this.testingApi.WaitForMainTask();
                        resolve(record);
                    }, TaskContinuationOptions.OnlyOnFaulted);

                    task.ContinueWith(prev =>
                    {
                        var record = this.testingApi.WaitForMainTask();
                        resolve(record);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);

                    /*var record = this.testingApi.WaitForMainTask();
                    resolve(record);*/

                    /*Task task = Task.Factory.StartNew(() =>
                    {
                        testMethod.Invoke(null, null);

                    }, TaskCreationOptions.AttachedToParent);

                    task.ContinueWith(prev =>
                    {
                        //this.testingApi.EndTask(0);
                        var record = this.testingApi.WaitForMainTask();
                        resolve(record);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);

                    task.ContinueWith(prev =>
                    {
                        Console.WriteLine("  [NekaraClient.StartSession] Main Task threw an Exception!\n{0}", prev.Exception.Message);
                        var record = this.testingApi.WaitForMainTask();
                        // do nothing
                        resolve(record);
                    }, TaskContinuationOptions.OnlyOnFaulted);*/
                }

            }).Then(data =>
            {
                // TestResult result = TestResult.Deserialize((string)data);
                SessionRecord record = SessionRecord.Deserialize((string)data);

                // if result exists, this is a replayed session
                if (!this.records.ContainsKey(sessionId)) this.records.Add(sessionId, record);
                else this.records[sessionId] = record;

                // clean up
                // this.testingApi.Finish();
                // Task.Delay(200).Wait();

                Console.WriteLine("\n--------------------------------------------\n");
                Console.WriteLine("    Total Requests:\t{0}", this.testingApi.numRequests);
                Console.WriteLine("    Average RTT:\t{0} ms", this.testingApi.avgRtt);
                Console.WriteLine("\n\n==========[ Test {0} {1} ]==========\n", sessionId, record.reason == "" ? "PASSED" : "FAILED");
                if (record.reason != "")
                {
                    Console.WriteLine("  " + record.reason);
                    Console.WriteLine("\n==================================== END ===[ {0} ms ]===", record.elapsedMs);
                }

                return record;
            }).Catch(ex =>
            {
                Console.WriteLine(ex);
                throw ex;
            });

            return mainTask.Task;
        }

        public Promise RunNewTestSession(MethodInfo testMethod, int schedulingSeed = 0)
        {
            var assembly = testMethod.DeclaringType.Assembly;
            var testMeta = (TestMethodAttribute)testMethod.GetCustomAttribute(typeof(TestMethodAttribute));

            // Communicate to the server here to notify the start of a test
            // and initialize the test session (receive a session ID)
            return new Promise((resolve, reject) =>
            {
                Console.WriteLine("\n\n============================================");
                Console.WriteLine(">>    Starting new session with seed = {0}", schedulingSeed);
                var (request, canceller) = this.socket.SendRequest("InitializeTestSession", new SessionInfo("",
                    assembly.FullName,
                    assembly.Location,
                    testMethod.DeclaringType.FullName,
                    testMethod.Name,
                    schedulingSeed,
                    testMeta.TimeoutMs,
                    testMeta.MaxDecisions
                ).Jsonify());
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(sessionId =>
            {
                string sid = sessionId.ToString();

                var session = this.StartSession(sid, testMethod, schedulingSeed);

                session.Wait();

                return session.Result;
            });
        }

        public Promise ReplayTestSession(string sessionId, bool setupAndTeardown = true)
        {
            return new Promise((resolve, reject) =>
            {
                Console.WriteLine("\n\n============================================");
                Console.WriteLine(">>    Replaying session {0}", sessionId);
                var (request, canceller) = this.socket.SendRequest("ReplayTestSession", new JToken[] { sessionId });
                request.ContinueWith(prev => resolve(prev.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                request.ContinueWith(prev => reject(prev.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }).Then(payload =>
            {
                // WARN: From here we don't have static typing.
                // The code below can throw arbitrary exceptions
                // if the JSON payload format changes
                JObject data = (JObject)payload;
                SessionInfo info = SessionInfo.FromJson(data);

                Console.WriteLine("  Seed : {0}", info.schedulingSeed);
                Console.WriteLine("  [{2} .{1}] in {0}", info.assemblyName, info.methodName, info.methodDeclaringClass);

                var assembly = Assembly.LoadFrom(info.assemblyPath);
                var testMethod = GetMethodToBeTested(assembly, info.methodDeclaringClass, info.methodName);
                var testDefinition = GetTestDefinition(testMethod);

                if (testDefinition.Setup != null && setupAndTeardown) testDefinition.Setup.Invoke(null, null);
                var session = this.StartSession(info.id, testMethod, info.schedulingSeed);
                session.Wait();
                if (testDefinition.Teardown != null && setupAndTeardown) testDefinition.Teardown.Invoke(null, null);

                return session.Result;
            });
        }

        public void Dispose()
        {
            socket.Dispose();
            return;
        }
    }
}
