using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using System.Reflection;

namespace Nekara.Core
{
    /* The objects below are transport-agnostic and deals only with the user-facing testing API.
     * The only thing related to the transport mechanism is the RemoteMethodAttribute
     */

    // This is the service object exposed to the client, and hosted on the server-side
    // The testing service API should be exposed by this object.
    public class NekaraServer : MarshalByRefObject
    {
        public static int gCount = 0;

        // private Dictionary<string, TestResult> testResults;
        private Dictionary<string, TestingSession> testSessions;

        private StreamWriter logFile;
        // private StreamWriter traceFile;
        private StreamWriter summaryFile;
        private OmniServer socket;      // keeping this reference is a temporary workaround to handle the InitializeTestSession notifyClient callback.
                                        // TODO: it should be handled more gracefully by revising the RemoteMethodAsync signature to accept a reference to ClientHandle
                                        //       and the respective reply/reject callbacks

        private TestingSession currentSession;

        public NekaraServer(OmniServer socket)
        {
            // this.testResults = new Dictionary<string, TestResult>();
            this.testSessions = new Dictionary<string, TestingSession>();

            string logPath = "logs/log-" + DateTime.Now.Ticks.ToString() + ".csv";
            this.logFile = File.AppendText(logPath);
            this.logFile.WriteLine("Counter,Method,Arguments,Tag,Thread,NumThreads");

            string sumPath = "logs/summary-" + DateTime.Now.Ticks.ToString() + ".csv";
            this.summaryFile = File.AppendText(sumPath);
            this.summaryFile.WriteLine("Assembly,Class,Method,SessionId,Seed,Result,Reason,Elapsed");

            this.socket = socket;

            this.currentSession = null;
        }

        [RemoteMethod(name = "InitializeTestSession", description = "Initializes server-side proxy program that will represent the actual program on the client-side")]
        // treating this method as a special case because it spawns another Task we have to resolve later
        public string InitializeTestSession(JToken arg0, JToken arg1, JToken arg2, JToken arg3, JToken arg4)
        {
            // HACK: Wait till previous session finishes
            // - a better way to deal with this is to keep a dictionary of sessions
            // and associate each request with a particular session so that the sessions are isolated
            while (this.currentSession != null)
            {
                Thread.Sleep(200);
            }

            string assemblyName = arg0.ToObject<string>();
            string assemblyPath = arg1.ToObject<string>();
            string methodDeclaringClass = arg2.ToObject<string>();
            string methodName = arg3.ToObject<string>();
            int schedulingSeed = arg4.ToObject<int>();

            var session = new TestingSession(assemblyName, assemblyPath, methodDeclaringClass, methodName, schedulingSeed);
            session.logger = this.logFile;

            session.OnComplete(finished =>
            {
                // It is important that we fetch the client socket dynamically in this callback and not outside it,
                // because the client socket may change when doing a session replay
                
                /*var client = this.socket.GetClient();   // HACK - this always returns the same client; should be updated to load client by session ID
                var message = new RequestMessage("Tester-Server", client.id, "FinishTest", new JToken[] { finished.id, finished.passed, finished.reason });
                var serialized = JsonConvert.SerializeObject(message);
                client.Send(serialized);*/

                Console.WriteLine("Test {0} Finished!", finished.id);

                // Append Summary
                string summary = String.Join(",", new string[] { assemblyName, methodDeclaringClass, methodName, finished.id, finished.schedulingSeed.ToString(), (finished.passed ? "pass" : "fail"), finished.reason, finished.ElapsedMilliseconds.ToString() });

                Console.WriteLine(summary);
                this.summaryFile.WriteLine(summary);
                this.summaryFile.Flush();

                Console.WriteLine("Results: {0}/{1}", this.testSessions.Where(item => item.Value.passed == true).Count(), this.testSessions.Count);

                this.currentSession = null;      // Clear the sessionId so the next session can begin
            });
            
            this.testSessions.Add(session.id, session);
            this.currentSession = session;

            Console.WriteLine("\n\n============================================\n");
            Console.WriteLine("Initialized session {3} for [{1}] in {0}, with seed = {2}\n", assemblyName, methodName, schedulingSeed, session.id);

            return this.currentSession.id;
        }

        [RemoteMethod(name = "GetSessionInfo", description = "Gets the Session info based on the session ID")]
        public SessionInfo GetSessionInfo(JToken arg)
        {
            string sessionId = arg.ToObject<string>();
            if (!this.testSessions.ContainsKey(sessionId)) throw new SessionRecordNotFoundException("Session " + sessionId + " not found");

            return this.testSessions[sessionId].info;
        }

        [RemoteMethod(name = "ReplayTestSession", description = "Replays the test session identified by the given session ID")]
        public SessionInfo ReplayTestSession(JToken arg)
        {
            // HACK: Wait till previous session finishes
            // - a better way to deal with this is to keep a dictionary of sessions
            // and associate each request with a particular session so that the sessions are isolated
            while (this.currentSession != null)
            {
                Thread.Sleep(200);
            }

            string sessionId = arg.ToObject<string>();

            SessionInfo info = GetSessionInfo(sessionId);
            TestingSession session = this.testSessions[sessionId];
            session.Reset();

            Console.WriteLine("\n\n============================================\n");
            Console.WriteLine("Replaying test {0}: [{2}] in {1}\n", sessionId, session.assemblyName, session.methodName);

            this.currentSession = session;

            return info;
        }

        private object RouteRemoteCall(JToken sessionId, string methodName, object[] args)
        {
            var session = this.testSessions[sessionId.ToObject<string>()];
            if (session.IsFinished) throw new SessionAlreadyFinishedException(session.id + " has already finished");
            var method = session.GetType().GetMethod(methodName);
            try
            {
                return method.Invoke(session, args);
            }
            catch (TargetInvocationException ex)
            {
                // Console.WriteLine(ex);
                Console.WriteLine("\n[RouteRemoteCall] TargetInvocationException");
                if (ex.InnerException is AssertionFailureException) session.Finish(false, ex.InnerException.Message);
                else if (ex.InnerException is AggregateException)
                {
                    Console.WriteLine("  [RouteRemoteCall] TargetInvocationException/AggregateException");
                    Console.WriteLine("    {0}", ex.InnerException.InnerException);
                    throw ex.InnerException.InnerException;
                }
                else if (ex.InnerException is TargetInvocationException)
                {
                    Console.WriteLine("  [RouteRemoteCall] TargetInvocationException/TargetInvocationException");
                }
                else Console.WriteLine(ex);
                throw ex.InnerException;
            }
            /*catch (AggregateException aex)
            {
                Console.WriteLine("\n[RouteRemoteCall] AggregateException");
                Console.WriteLine(aex);
                aex.Flatten().Handle(ex =>
                {
                    if (ex is AssertionFailureException) session.Finish(false, ex.Message);
                    return false;
                });
                throw aex.InnerException;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[RouteRemoteCall] Exception");
                Console.WriteLine(ex);
                throw ex;
            }*/
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
        [RemoteMethod(name = "CreateTask", description = "Creates a new task")]
        public void CreateTask(JToken sessionId)
        {
            RouteRemoteCall(sessionId, "CreateTask", null);
        }

        [RemoteMethod(name = "StartTask", description = "Signals the start of a given task")]
        public void StartTask(JToken sessionId, JToken taskId)
        {
            RouteRemoteCall(sessionId, "StartTask", new object[] { taskId.ToObject<int>() });
        }

        [RemoteMethod(name = "EndTask", description = "Signals the end of a given task")]
        public void EndTask(JToken sessionId, JToken taskId)
        {
            RouteRemoteCall(sessionId, "EndTask", new object[] { taskId.ToObject<int>() });
        }

        [RemoteMethod(name = "CreateResource", description = "Notifies the creation of a new resource")]
        public void CreateResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "CreateResource", new object[] { resourceId.ToObject<int>() });
        }

        [RemoteMethod(name = "DeleteResource", description = "Signals the deletion of a given resource")]
        public void DeleteResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "DeleteResource", new object[] { resourceId.ToObject<int>() });
        }

        [RemoteMethod(name = "BlockedOnResource", description = "")]
        public void BlockedOnResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "BlockedOnResource", new object[] { resourceId.ToObject<int>() });
        }

        [RemoteMethod(name = "SignalUpdatedResource", description = "")]
        public void SignalUpdatedResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "SignalUpdatedResource", new object[] { resourceId.ToObject<int>() });
        }

        [RemoteMethod(name = "CreateNondetBool", description = "")]
        public bool CreateNondetBool(JToken sessionId)
        {
            return (bool)RouteRemoteCall(sessionId, "CreateNondetBool", null);
        }

        [RemoteMethod(name = "CreateNondetInteger", description = "")]
        public int CreateNondetInteger(JToken sessionId, JToken maxValue)
        {
            return (int)RouteRemoteCall(sessionId, "CreateNondetInteger", new object[] { maxValue.ToObject<int>() });
        }

        [RemoteMethod(name = "Assert", description = "")]
        public void Assert(JToken sessionId, JToken value, JToken message)
        {
            RouteRemoteCall(sessionId, "Assert", new object[] { value.ToObject<bool>(), message.ToObject<string>() });
        }

        [RemoteMethod(name = "ContextSwitch", description = "Signals the deletion of a given resource")]
        public void ContextSwitch(JToken sessionId)
        {
            RouteRemoteCall(sessionId, "ContextSwitch", null);
        }

        [RemoteMethod(name = "WaitForMainTask", description = "Wait for test to finish")]
        public string WaitForMainTask(JToken sessionId)
        {
            return (string)RouteRemoteCall(sessionId, "WaitForMainTask", null);
        }
    }
}
