using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsyncTester
{
    public abstract class JsonP2P
    {
        private string id;
        private Dictionary<string, RemoteMethodAsync> remoteMethods;
        private Dictionary<string, TaskCompletionSource<JToken>> requests;
        private HashSet<string> peers;

        public JsonP2P()
        {
            this.id = Helpers.RandomString(16);
            this.remoteMethods = new Dictionary<string, RemoteMethodAsync>();
            this.requests = new Dictionary<string, TaskCompletionSource<JToken>>();
            this.peers = new HashSet<string>();
        }

        public abstract Task Send(string recipient, string payload);

        public void RegisterRemoteMethod(string name, RemoteMethodAsync handler)
        {
            // RemoteMethodAsync method = new RemoteMethodAsync(handler);
            this.remoteMethods[name] = handler;
        }

        public void HandleMessage(string payload)
        {
            // Console.WriteLine("    Trying to handle message: {0}", payload);
            try
            {
                HandleRequest(payload);
            }
            catch (UnexpectedRequestException e1)
            {
                try
                {
                    HandleResponse(payload);
                }
                catch (UnexpectedResponseException e2)
                {
                    throw new UnexpectedMessageException();
                }
            }
        }

        public void HandleRequest(string payload)
        {
            RequestMessage message = JsonConvert.DeserializeObject<RequestMessage>(payload);
            if (message.func != null && this.remoteMethods.ContainsKey(message.func))
            {
                this.remoteMethods[message.func](message.args.ToArray());
            }
            else
            {
                throw new UnexpectedRequestException();
            }
        }

        public void HandleResponse(string payload)
        {
            ResponseMessage message = JsonConvert.DeserializeObject<ResponseMessage>(payload);
            if (message.responseTo != null && this.requests.ContainsKey(message.responseTo))
            {
                this.requests[message.responseTo].SetResult(message.data);
            }
            else
            {
                throw new UnexpectedResponseException();
            }
        }

        public Task<JToken> Request(string recipient, string func, JArray args, int timeout = 60000)
        {
            // Console.WriteLine("    Requesting {0} ({1})", func, args);
            var tcs = new TaskCompletionSource<JToken>();   // This tcs will be settled when the response comes back
            var message = new RequestMessage(this.id, recipient, func, args);
            var serialized = JsonConvert.SerializeObject(message);
            this.requests.Add(message.id, tcs);
            this.Send(recipient, serialized);

            var timer = new Timer(_ => tcs.SetException(new RequestTimeoutException()), null, timeout, Timeout.Infinite);   // Set a timeout for the request
            tcs.Task.ContinueWith(prev => {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                this.requests.Remove(message.id);
                timer.Dispose();
            });
            return tcs.Task;
        }

        public Task Respond(string recipient, string requestId, JToken data)
        {
            // Console.WriteLine("    Responding to {0} ({1})", requestId, data);
            var message = new ResponseMessage(this.id, recipient, requestId, data);
            var serialized = JsonConvert.SerializeObject(message);
            return this.Send(recipient, serialized);
        }
    }

    public delegate Task<JToken> RemoteMethodAsync(params JToken[] args);

    public class RemoteMethodAttribute : Attribute
    {
        public string name;
        public string description;
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    public class RequestMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string sender;

        [DataMember]
        internal string recipient;

        [DataMember]
        internal string func;

        [DataMember]
        internal JArray args;

        public RequestMessage(string sender, string recipient, string func, JArray args)
        {
            this.id = "req-" + Helpers.RandomString(16);
            this.sender = sender;
            this.recipient = recipient;
            this.func = func;
            this.args = args;
        }

        public ResponseMessage CreateResponse(string sender, JToken data)
        {
            return new ResponseMessage(sender, this.sender, this.id, data);
        }
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    public class ResponseMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string sender;

        [DataMember]
        internal string recipient;

        [DataMember]
        internal string responseTo;

        [DataMember]
        internal JToken data;

        public ResponseMessage(string sender, string recipient, string requestId, JToken data)
        {
            this.id = "res-" + Helpers.RandomString(16);
            this.sender = sender;
            this.recipient = recipient;
            this.responseTo = requestId;
            this.data = data;
        }
    }
}
