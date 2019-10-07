using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester
{
    class Exceptions
    {
    }

    /* Test related */
    internal sealed class AssertionFailureException : Exception
    {
        internal AssertionFailureException(string message)
            : base(message)
        {
        }
        internal AssertionFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /* Config related */

    class TesterConfigurationException : Exception
    {

    }
    public class TestMethodLoadFailureException : Exception { }

    /* Communication related */
    class ServerThrownException : Exception
    {
        private JObject serialized;

        public ServerThrownException(JToken payload)
        {
            // we assume that the payload is the serialized Exception object, and cast it to JObject
            this.serialized = (JObject)payload;
        }

        public string ClassName { get { return this.serialized["ClassName"].ToObject<string>();  } }
        public override string Message { get { return this.serialized["Message"].ToObject<string>(); } }
    }

    class InvalidRequestPayloadException : Exception
    {

    }

    class RequestTimeoutException : Exception { }
    class UnexpectedMessageException : Exception { }
    class UnexpectedResponseException : Exception { }
    class UnexpectedRequestException : Exception { }
}
