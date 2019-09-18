using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncTester;

namespace ClientProgram
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Test Client...");

            // Initialize a tester client (this should actually be done in a different process)
            TesterClient client = new TesterClient(new TesterConfiguration());

            Console.WriteLine("... Bye");
        }
    }
}
