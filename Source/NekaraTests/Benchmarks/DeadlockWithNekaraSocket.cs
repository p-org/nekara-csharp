using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    class DeadlockWithNekaraSocket
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        //static int x = 0;
        static Nekara.Models.Socket fooInbox;
        static Nekara.Models.Socket barInbox;

        [TestMethod]
        public static void Run()
        {
            fooInbox = new Nekara.Models.Socket(100);
            barInbox = new Nekara.Models.Socket(200);

            // initialize all relevant state
            nekara.CreateTask();
            Task.Run(() => Foo());

            nekara.CreateTask();
            Task.Run(() => Bar());
        }

        static void Foo()
        {
            nekara.StartTask(1);

            Console.WriteLine("Foo - writing to Bar Inbox");
            object message = new object();
            nekara.Assert(barInbox != null, "Bar socket not ready");
            barInbox.Write(message);

            Console.WriteLine("Foo - ContextSwitch");
            nekara.ContextSwitch();

            Console.WriteLine("Foo - reading from Foo Inbox");
            object response = fooInbox.Read();

            nekara.Assert(response == message, "Bug found!");

            Console.WriteLine("Foo EndTask");
            nekara.EndTask(1);
        }

        static void Bar()
        {
            nekara.StartTask(2);

            Console.WriteLine("Bar - reading from Bar Inbox");
            //object request = barInbox.Read();

            Console.WriteLine("Bar - ContextSwitch");
            nekara.ContextSwitch();

            Console.WriteLine("Bar - writing to Foo Inbox");
            //fooInbox.Write(request);

            Console.WriteLine("Bar EndTask");
            nekara.EndTask(2);
        }
    }
}
