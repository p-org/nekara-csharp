using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester
{
    public static class Exceptions
    {
        public static Exception DeserializeServerSideException(JToken payload)
        {
            // we assume that the payload is the serialized Exception object, and cast it to JObject
            // WARNING: if the server sends anything other than JObject,
            // this will throw an exception silently and will be swallowed!
            Console.WriteLine("--> Server Threw an Exception. {0}", payload.ToString());
            var serialized = payload.ToObject<JObject>();

            var ExceptionType = Assembly.GetExecutingAssembly().GetType(serialized["ClassName"].ToObject<string>());
            var exception = (Exception)Activator.CreateInstance(ExceptionType, new[] { serialized["Message"].ToObject<string>() });

            return exception;
        }
    }

    /* Test related */
    public abstract class TestingServiceException : Exception, ISerializable
    {
        public TestingServiceException(string message) : base(message) { }

        override public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ClassName", this.GetType().FullName, typeof(string));
            info.AddValue("Message", this.Message, typeof(string));
        }
    }

    [Serializable]
    public class AssertionFailureException : TestingServiceException
    {
        public AssertionFailureException(string message) : base(message)
        {
        }
    }

    /* Config related */

    class TesterConfigurationException : Exception
    {

    }
    public class TestMethodLoadFailureException : Exception { }

    /* Communication related */
    // this is the client-side representation of the exception thrown by the server
    public class ServerThrownException : Exception
    {
        private JObject serialized;

        public ServerThrownException(JToken payload)
        {
            // we assume that the payload is the serialized Exception object, and cast it to JObject
            // WARNING: if the server sends anything other than JObject,
            // this will throw an exception silently and will be swallowed!
            this.serialized = payload.ToObject<JObject>();
        }

        public string ClassName { get { return this.serialized["ClassName"].ToObject<string>(); } }

        public override string Message { get { return this.serialized["Message"].ToObject<string>(); } }
    }

    public abstract class LogisticalException : Exception, ISerializable
    {
        public LogisticalException (string message) : base (message) { }

        override public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ClassName", this.GetType().FullName, typeof(string));
            info.AddValue("Message", this.Message, typeof(string));
        }
    }

    [Serializable]
    public class InvalidRequestPayloadException : LogisticalException
    {
        public InvalidRequestPayloadException(string message) : base(message) { }
    }

    [Serializable]
    public class RemoteMethodDoesNotExistException : LogisticalException
    {
        public RemoteMethodDoesNotExistException(string message) : base(message) { }
    }

    class RequestTimeoutException : Exception { }
    class UnexpectedMessageException : Exception { }
    class UnexpectedResponseException : Exception { }
    class UnexpectedRequestException : Exception { }
}
