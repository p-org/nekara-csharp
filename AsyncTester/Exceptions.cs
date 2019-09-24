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
    class AssertionFailureException : Exception { }

    /* Config related */

    class TesterConfigurationException : Exception
    {

    }
    public class TestMethodLoadFailureException : Exception { }

    /* Communication related */

    class InvalidRequestPayloadException : Exception
    {

    }

    class RequestTimeoutException : Exception { }
    class UnexpectedMessageException : Exception { }
    class UnexpectedResponseException : Exception { }
    class UnexpectedRequestException : Exception { }
}
