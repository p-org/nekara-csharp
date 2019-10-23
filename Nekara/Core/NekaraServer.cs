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

        private Dictionary<string, TestingSession> testSessions;

        private StreamWriter logFile;
        private StreamWriter summaryFile;
        private OmniServer socket;      // keeping this reference is a temporary workaround to handle the InitializeTestSession notifyClient callback.
                                        // TODO: it should be handled more gracefully by revising the RemoteMethodAsync signature to accept a reference to ClientHandle
                                        //       and the respective reply/reject callbacks

        private TestingSession currentSession;

        public NekaraServer(OmniServer socket)
        {
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
                Console.WriteLine("\n\n==========[ Test {0} {1} ]==========\n", finished.id, finished.passed ? "PASSED" : "FAILED");
                if (finished.reason != "")
                {
                    Console.WriteLine("  " + finished.reason);
                }

                // Append Summary
                string summary = String.Join(",", new string[] { assemblyName, methodDeclaringClass, methodName, finished.id, finished.schedulingSeed.ToString(), (finished.passed ? "pass" : "fail"), finished.reason, finished.ElapsedMilliseconds.ToString() });
                this.summaryFile.WriteLine(summary);
                this.summaryFile.Flush();

                Console.WriteLine("\n===== END of {0} (ran in {1} ms) =====[ Results: {2}/{3} ]=====\n\n", finished.id, finished.ElapsedMilliseconds.ToString(), this.testSessions.Where(item => item.Value.passed == true).Count(), this.testSessions.Count);

                this.currentSession = null;      // Clear the sessionId so the next session can begin
            });
            
            this.testSessions.Add(session.id, session);
            this.currentSession = session;

            Console.WriteLine("\n\n===== BEGIN {0} ================================\n", session.id);
            Console.WriteLine("  [{1}]\n  in {0}, with seed = {2}\n", assemblyName, methodDeclaringClass + "." + methodName, schedulingSeed);
            Console.WriteLine("\nIndex\tCurThrd\t#Thrds\tCurTask\t#Tasks\tStage\tMethod\tArgs");

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

            Console.WriteLine("\n\n===== BEGIN REPLAY {0} =========================\n", sessionId);
            Console.WriteLine("  [{1}]\n  in {0}\n", session.assemblyName, session.methodDeclaringClass + "." + session.methodName);

            this.currentSession = session;

            return info;
        }

        private object RouteRemoteCall(JToken sessionId, string methodName, params object[] args)
        {
            var session = this.testSessions[sessionId.ToObject<string>()];

            return session.InvokeAndHandleException(methodName, args);
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
        [RemoteMethod(name = "CreateTask", description = "Creates a new task")]
        public void CreateTask(JToken sessionId)
        {
            RouteRemoteCall(sessionId, "CreateTask");
        }

        [RemoteMethod(name = "StartTask", description = "Signals the start of a given task")]
        public void StartTask(JToken sessionId, JToken taskId)
        {
            RouteRemoteCall(sessionId, "StartTask", taskId.ToObject<int>());
        }

        [RemoteMethod(name = "EndTask", description = "Signals the end of a given task")]
        public void EndTask(JToken sessionId, JToken taskId)
        {
            RouteRemoteCall(sessionId, "EndTask", taskId.ToObject<int>());
        }

        [RemoteMethod(name = "CreateResource", description = "Notifies the creation of a new resource")]
        public void CreateResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "CreateResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "DeleteResource", description = "Signals the deletion of a given resource")]
        public void DeleteResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "DeleteResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "BlockedOnResource", description = "")]
        public void BlockedOnResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "BlockedOnResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "SignalUpdatedResource", description = "")]
        public void SignalUpdatedResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId, "SignalUpdatedResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "CreateNondetBool", description = "")]
        public bool CreateNondetBool(JToken sessionId)
        {
            return (bool)RouteRemoteCall(sessionId, "CreateNondetBool");
        }

        [RemoteMethod(name = "CreateNondetInteger", description = "")]
        public int CreateNondetInteger(JToken sessionId, JToken maxValue)
        {
            return (int)RouteRemoteCall(sessionId, "CreateNondetInteger", maxValue.ToObject<int>());
        }

        [RemoteMethod(name = "Assert", description = "")]
        public void Assert(JToken sessionId, JToken value, JToken message)
        {
            RouteRemoteCall(sessionId, "Assert", value.ToObject<bool>(), message.ToObject<string>());
        }

        [RemoteMethod(name = "ContextSwitch", description = "Signals the deletion of a given resource")]
        public void ContextSwitch(JToken sessionId)
        {
            RouteRemoteCall(sessionId, "ContextSwitch");
        }

        [RemoteMethod(name = "WaitForMainTask", description = "Wait for test to finish")]
        public string WaitForMainTask(JToken sessionId)
        {
            return (string)RouteRemoteCall(sessionId, "WaitForMainTask");
        }
    }
}
