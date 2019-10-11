using System;

namespace AsyncTester.Core
{
    public class ControlledTask
    {
        public static ITestingService testingService;

        public static System.Threading.Tasks.Task Run(Action action)
        {
            testingService.CreateTask();
            return System.Threading.Tasks.Task.Run(() =>
            {
                testingService.StartTask(1);
                action();
                testingService.EndTask(1);
            });
        }
    }
}
