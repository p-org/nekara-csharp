using Microsoft.PSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestingService
{
    class ControlledTestingService : ITestingService
    {
        private static int count = 0;

        ProgramState programState;
        MachineId topLevelMachineId;
        IMachineRuntime runtime;
        TaskCompletionSource<bool> IterFinished;
        int numPendingTaskCreations;

        public ControlledTestingService(MachineId topLevelMachineId)
        {
            this.programState = new ProgramState();
            this.topLevelMachineId = topLevelMachineId;
            this.runtime = topLevelMachineId.Runtime;
            this.IterFinished = new TaskCompletionSource<bool>();

            this.programState.taskToTcs.Add(0, new TaskCompletionSource<bool>());
            this.numPendingTaskCreations = 0;
        }
        
        public void CreateTask()
        {
            Console.WriteLine("{0}\tCreateTask()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            lock (programState)
            {
                this.numPendingTaskCreations++;
            }
            Console.WriteLine("{0}\tCreateTask()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void ContextSwitch()
        {
            Console.WriteLine("{0}\tContextSwitch()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            WaitForPendingTaskCreations();

            var tcs = new TaskCompletionSource<bool>();
            List<int> enabledTasks;
            int next;
            int currentTask;
            bool currentTaskEnabled = false;

            lock(programState)
            {
                currentTask = programState.currentTask;
                currentTaskEnabled = programState.taskToTcs.ContainsKey(currentTask);

                // pick next one to execute
                enabledTasks = new List<int>(
                    programState.taskToTcs.Keys
                    .Where(k => !programState.taskStatus.ContainsKey(k))
                    );                   

                if(enabledTasks.Count == 0)
                {
                    runtime.Assert(programState.taskToTcs.Count == 0, "Deadlock detected");

                    // all-done
                    IterFinished.SetResult(true);
                    Console.WriteLine("{0}\tContextSwitch()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
                    return;
                }

                next = runtime.RandomInteger(enabledTasks.Count);
            }

            if (enabledTasks[next] == currentTask)
            {
                // no-op
            }
            else
            {
                TaskCompletionSource<bool> nextTcs;

                lock (programState)
                {
                    nextTcs = programState.taskToTcs[enabledTasks[next]];
                    if (currentTaskEnabled)
                    {
                        programState.taskToTcs[programState.currentTask] = tcs;
                    }
                    programState.currentTask = enabledTasks[next];
                }

                nextTcs.SetResult(true);

                if (currentTaskEnabled)
                {
                    tcs.Task.Wait();
                }
            }
            Console.WriteLine("{0}\tContextSwitch()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void BlockedOnResource(int resourceId)
        {
            Console.WriteLine("{0}\tBlockedOnResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            lock (programState)
            {
                runtime.Assert(!programState.taskStatus.ContainsKey(programState.currentTask), 
                    $"Illegal operation, task {programState.currentTask} already blocked on a resource");
                programState.taskStatus[programState.currentTask] = resourceId;
            }

            ContextSwitch();
            Console.WriteLine("{0}\tBlockedOnResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            lock (programState)
            {
                var enabledTasks = programState.taskStatus.Where(tup => tup.Value == resourceId).Select(tup => tup.Key).ToList();
                foreach (var k in enabledTasks)
                {
                    programState.taskStatus.Remove(k);
                }
            }

            //ContextSwitch();
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public bool CreateNondetBool()
        {
            Console.WriteLine("{0}\tCreateNondetBool()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            return runtime.Random();
        }

        public int CreateNondetInteger(int maxValue)
        {
            Console.WriteLine("{0}\tCreateNondetInteger()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            return runtime.RandomInteger(maxValue);
        }

        public void CreateResource(int resourceId)
        {
            Console.WriteLine("{0}\tCreateResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            lock (programState)
            {
                runtime.Assert(!programState.resourceSet.Contains(resourceId), $"Duplicate declaration of resource: {resourceId}");
                programState.resourceSet.Add(resourceId);
            }
            Console.WriteLine("{0}\tCreateResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            Console.WriteLine("{0}\tDeleteResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            lock (programState)
            {
                runtime.Assert(programState.resourceSet.Contains(resourceId), $"DeleteResource called on unknown resource: {resourceId}");
                programState.resourceSet.Remove(resourceId);
            }
            Console.WriteLine("{0}\tDeleteResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void EndTask(int taskId)
        {
            Console.WriteLine("{0}\tEndTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            WaitForPendingTaskCreations();

            lock (programState)
            {
                runtime.Assert(programState.taskToTcs.ContainsKey(taskId), $"EndTask called on unknown task: {taskId}");
                programState.taskToTcs.Remove(taskId);
            }

            ContextSwitch();
            Console.WriteLine("{0}\tEndTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        public async Task IsFinished()
        {
            Console.WriteLine("{0}\tIsFinished\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            await IterFinished.Task;
        }

        public void StartTask(int taskId)
        {
            Console.WriteLine("{0}\tStartTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            lock (programState)
            {
                runtime.Assert(!programState.taskToTcs.ContainsKey(taskId), $"Duplicate declaration of task: {taskId}");
                programState.taskToTcs.Add(taskId, tcs);
                numPendingTaskCreations--;
            }

            tcs.Task.Wait();
            Console.WriteLine("{0}\tStartTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        public void Assert(bool value, string message)
        {
            Console.WriteLine("{0}\tAssert\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            runtime.Assert(value, message);
        }

        void WaitForPendingTaskCreations()
        {
            Console.WriteLine("{0}\tWaitForPendingTaskCreations()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            while (true)
            {
                lock (programState)
                {
                    if (numPendingTaskCreations == 0)
                    {
                        return;
                    }
                }

                Thread.Sleep(10);
            }
            Console.WriteLine("{0}\tWaitForPendingTaskCreations()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
    }
}
