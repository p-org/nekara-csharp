using System;
using System.Collections.Generic;
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
        public ServerThrownException(string message)
        {
        }
    }

    class InvalidRequestPayloadException : Exception
    {

    }

    class RequestTimeoutException : Exception { }
    class UnexpectedMessageException : Exception { }
    class UnexpectedResponseException : Exception { }
    class UnexpectedRequestException : Exception { }
}
