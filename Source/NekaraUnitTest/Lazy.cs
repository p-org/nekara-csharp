using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class Lazy
    {
        [Fact(Timeout = 5000)]
        public void RunLazyTest()
        {
            bool bugfound = false;

            while (!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                int data = 0;

                var l = new Lock(1);

                Task t1 = Task.Run(() =>
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        data++;
                    }
                });

                Task t2 = Task.Run(() =>
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        data += 2;
                    }
                });

                Task t3 = Task.Run(() =>
                {
                    nekara.Api.ContextSwitch();
                    using (l.Acquire())
                    {
                        // nekara.Api.Assert(data < 3, "Bug found!");
                        if (!(data < 3))
                        {
                            bugfound = true;
                        }
                    }
                });

                Task.WaitAll(t1, t2, t3);

                nekara.Api.WaitForMainTask();
            }
            Assert.True(bugfound);
        }
    }
}
