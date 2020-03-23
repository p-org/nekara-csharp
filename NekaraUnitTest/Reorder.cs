using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class Reorder
    {
        [Fact(Timeout = 5000)]
        public void N_3()
        {
            PerformReorder(2, 1);
        }

        [Fact(Timeout = 5000)]
        public void N_4()
        {
            PerformReorder(3, 1);
        }

        [Fact(Timeout = 5000)]
        public void N_5()
        {
            PerformReorder(4, 1);
        }

        [Fact(Timeout = 5000)]
        public void N_7()
        {
            PerformReorder(6, 1);
        }

        [Fact(Timeout = 5000)]
        public void N_10()
        {
            PerformReorder(9, 1);
        }

        [Fact(Timeout = 5000)]
        public void N_20()
        {
            PerformReorder(19,1);
        }

        internal void PerformReorder(int numSTasks, int numCTasks)
        {
            bool bugfound = false;
            while (!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                int numSetTasks = numSTasks;
                int numCheckTasks = numCTasks;

                int a = 0;
                int b = 0;

                Task[] setPool = new Task[numSetTasks];
                Task[] checkPool = new Task[numCheckTasks];

                for (int i = 0; i < numSetTasks; i++)
                {
                    setPool[i] = Task.Run(() =>
                    {
                        nekara.Api.ContextSwitch();
                        a = 1;

                        nekara.Api.ContextSwitch();
                        b = -1;
                    });
                }

                for (int i = 0; i < numCheckTasks; i++)
                {
                    checkPool[i] = Task.Run(() =>
                    {
                        nekara.Api.ContextSwitch();
                        int localA = a;

                        nekara.Api.ContextSwitch();
                        int localB = b;

                        // nekara.Assert((localA == 0 && localB == 0) || (localA == 1 && localB == -1), "Bug found!");
                        if (!((localA == 0 && localB == 0) || (localA == 1 && localB == -1)))
                        {
                            bugfound = true;
                        }

                    });
                }

                Task.WaitAll(setPool);
                Task.WaitAll(checkPool);

                nekara.Api.WaitForMainTask();
            }
            Assert.True(bugfound);  
        }
    }
}
