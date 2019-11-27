using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using Nekara.Core;
using System.Threading;
using System.Diagnostics;

namespace Nekara.Client
{
    // This is the client-side proxy of the tester service.
    // Used when the system is operating in a network setting.
    public class NekaraClient : IDisposable
    {
        public IClient socket;
        private TestRuntimeApi testingApi;
        private Dictionary<string, SessionRecord> records;
        private Helpers.UniqueIdGenerator SchedulingSeedGenerator;
        private StreamWriter summaryFile;

        // This object will "plug-in" the communication mechanism.
        // The separation between the transport architecture and the logical, abstract model is intentional.
        public NekaraClient(IClient socket)
        {
            this.socket = socket;
            this.testingApi = new TestRuntimeApi(socket);
            this.SchedulingSeedGenerator = new Helpers.UniqueIdGenerator(false, 0);
            this.records = new Dictionary<string, SessionRecord>();
        }

        public ITestingService Api { get { return this.testingApi; } }
        public Helpers.UniqueIdGenerator TaskIdGenerator {
            get {
                if (this.testingApi.CurrentSession != null) return this.testingApi.CurrentSession.TaskIdGenerator;
                throw new Exception("ClientSession has not yet been initialized! Are you calling ");
            }
        }
        public Helpers.UniqueIdGenerator ResourceIdGenerator {
            get
            {
                if (this.testingApi.CurrentSession != null) return this.testingApi.CurrentSession.ResourceIdGenerator;
                throw new Exception("ClientSession has not yet been initialized! Are you calling ");
            }
        }

        public void PrintTestResults()
        {
            //lock (this.results)
            lock (this.records)
            {
                Console.WriteLine("\n\n=====[ Test Result ]===============\n");
                int passed = 0;
                var passedTime = new List<double>();
                var failedTime = new List<double>();
                foreach (var item in this.records.OrderBy(item => Int32.Parse(item.Key)))
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
            var testSummary = new TestSummary();

            this.summaryFile = File.AppendText("logs/client-summary-" + DateTime.Now.Ticks.ToString() + ".csv");
            this.summaryFile.WriteLine("Assembly,Class,Method,SessionId,Seed,Result,Reason,Elapsed");

            return new Promise((resolve, reject) =>
            {
                if (definition.Setup != null) definition.Setup.Invoke(null, null);
                resolve(null);
            }).Then(prev =>
            {
                var cts = new CancellationTokenSource();
                var run = Helpers.RepeatTask(() =>
                    {
                        long started = Stopwatch.GetTimestamp();
                        return this.RunNewTestSession(definition.Run, SchedulingSeedGenerator.Generate())
                        .Then(result => this.ReplayTestSession(((SessionRecord)result).sessionId, false))
                        .Then(result =>
                        {
                            SessionRecord record = (SessionRecord)result;

                            long elapsedMs = (Stopwatch.GetTimestamp() - started) / 10000;

                            // Append Summary
                            string summary = String.Join(",", new string[] {
                                definition.Run.DeclaringType.Assembly.FullName,
                                definition.Run.DeclaringType.FullName,
                                definition.Run.Name,
                                record.sessionId,
                                record.schedulingSeed.ToString(),
                                (record.passed ? "pass" : "fail"),
                                record.reason,
                                record.elapsedMs.ToString(),
                                elapsedMs.ToString()
                            });

                            testSummary.Update(record.numDecisions, record.elapsedMs, record.result == TestResult.MaxDecisionsReached);

                            this.summaryFile.WriteLine(summary);
                            this.summaryFile.Flush();

                            if (terminateOnFirstFail
                                && !record.passed
                                && record.result != TestResult.MaxDecisionsReached
                                && !cts.IsCancellationRequested) cts.Cancel();

                            return record;
                        }).Task;
                    },
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
#if DEBUG
                Console.WriteLine(TestRuntimeApi.Profiler.ToString());
#endif

                return testSummary;
            }).Then(prev =>
            {
                if (definition.Teardown != null) definition.Teardown.Invoke(null, null);
                return prev;
            });
        }

        public Task<object> StartSession(string sessionId, MethodInfo testMethod, int schedulingSeed)
        {
#if DEBUG
            Console.WriteLine(">>    Session Id : {0}", sessionId);
            Console.WriteLine("============================================\n");
#endif
            
            // Initialize a new AsyncLocal session data
            this.testingApi.InitializeNewSession(sessionId);

            Console.WriteLine("\tSession {0}, Run {1}", this.testingApi.CurrentSession.Id, this.testingApi.CurrentSession.RunNumber);

            var testDefinition = GetTestDefinition(testMethod);
            
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
                /*Console.WriteLine("User Method\n  Is Async: {0}\n  Returns Task-like: {1}\n  Returns NekaraTask: {2}",
                    testDefinition.Kind.HasFlag(TestDefinition.MethodKind.IsAsync),
                    testDefinition.Kind.HasFlag(TestDefinition.MethodKind.ReturnsTaskLike),
                    testDefinition.Kind.HasFlag(TestDefinition.MethodKind.ReturnsNekaraTask));*/

                Nekara.Models.Task task;
                try
                {
                    if (testDefinition.Kind.HasFlag(TestDefinition.MethodKind.ReturnsNekaraTask))
                    {
                        task = (Nekara.Models.Task)testMethod.Invoke(null, null);
                    }
                    /*else if (testDefinition.Kind.HasFlag(TestDefinition.MethodKind.ReturnsTaskLike))
                    {
                        task = Nekara.Models.Task.Run(() => testMethod.Invoke(null, null));
                    }*/
                    else
                    {
                        task = Nekara.Models.Task.Run(() => testMethod.Invoke(null, null));
                    }
                
                    task.Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  [NekaraClient.StartSession] Main Task threw {0}!\n{1}", ex.GetType().Name, ex.Message);
                }

                try
                {
                    var record = this.testingApi.WaitForMainTask();
                    resolve(record);
                }
                catch (Exception ex)
                {
                    reject(ex);
                }
            }).Then(data =>
            {
                SessionRecord record = SessionRecord.Deserialize((string)data);

                // if result exists, this is a replayed session
                lock (this.records)
                {
                    if (!this.records.ContainsKey(sessionId)) this.records.Add(sessionId, record);
                    else this.records[sessionId] = record;
                }

#if DEBUG
                Console.WriteLine("\n--------------------------------------------\n");
                Console.WriteLine("    Total Requests:\t{0}", this.testingApi.numRequests);
                Console.WriteLine("    Average RTT:\t{0} ms", this.testingApi.avgRtt);
                Console.WriteLine("\n\n==========[ Test {0} {1} ]==========\n", sessionId, record.reason == "" ? "PASSED" : "FAILED");
                if (record.reason != "")
                {
                    Console.WriteLine("  " + record.reason);
                    Console.WriteLine("\n==================================== END ===[ {0} ms ]===", record.elapsedMs);
                }
#endif

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
#if DEBUG
                Console.WriteLine("\n\n============================================");
                Console.WriteLine(">>    Starting new session with seed = {0}", schedulingSeed);
#endif
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

#if !DEBUG
                Console.WriteLine("Running new session '{0}'\tseed = {1}", sid, schedulingSeed);
#endif

                var session = this.StartSession(sid, testMethod, schedulingSeed);

                session.Wait();

                return session.Result;
            });
        }

        public Promise ReplayTestSession(string sessionId, bool setupAndTeardown = true)
        {
            return new Promise((resolve, reject) =>
            {
#if DEBUG
                Console.WriteLine("\n\n============================================");
                Console.WriteLine(">>    Replaying session {0}", sessionId);
#endif
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

#if DEBUG
                Console.WriteLine("  Seed : {0}", info.schedulingSeed);
                Console.WriteLine("  [{2} .{1}] in {0}", info.assemblyName, info.methodName, info.methodDeclaringClass);
#else
                Console.WriteLine("  Replaying session '{0}'\tseed = {1}", info.id, info.schedulingSeed);
#endif
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
