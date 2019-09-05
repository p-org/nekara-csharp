using Microsoft.PSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestingService
{
    class ControlledTestingService : ITestingService
    {
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
            lock (programState)
            {
                this.numPendingTaskCreations++;
            }
        }

        public void ContextSwitch()
        {
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
        }

        public void BlockedOnResource(int resourceId)
        {
            lock(programState)
            {
                runtime.Assert(!programState.taskStatus.ContainsKey(programState.currentTask), 
                    $"Illegal operation, task {programState.currentTask} already blocked on resource {programState.taskStatus[programState.currentTask]}");
                programState.taskStatus[programState.currentTask] = resourceId;
            }

            ContextSwitch();
        }

        public void SignalUpdatedResource(int resourceId)
        {
            lock (programState)
            {
                var enabledTasks = programState.taskStatus.Where(tup => tup.Value == resourceId).Select(tup => tup.Key).ToList();
                foreach (var k in enabledTasks)
                {
                    programState.taskStatus.Remove(k);
                }
            }

            //ContextSwitch();
        }

        public bool CreateNondetBool()
        {
            return runtime.Random();
        }

        public int CreateNondetInteger(int maxValue)
        {
            return runtime.RandomInteger(maxValue);
        }

        public void CreateResource(int resourceId)
        {
            lock(programState)
            {
                runtime.Assert(!programState.resourceSet.Contains(resourceId), $"Duplicate declaration of resource: {resourceId}");
                programState.resourceSet.Add(resourceId);
            }
        }

        public void DeleteResource(int resourceId)
        {
            lock (programState)
            {
                runtime.Assert(programState.resourceSet.Contains(resourceId), $"DeleteResource called on unknown resource: {resourceId}");
                programState.resourceSet.Remove(resourceId);
            }
        }

        public void EndTask(int taskId)
        {
            WaitForPendingTaskCreations();

            lock (programState)
            {
                runtime.Assert(programState.taskToTcs.ContainsKey(taskId), $"EndTask called on unknown task: {taskId}");
                programState.taskToTcs.Remove(taskId);
            }

            ContextSwitch();
        }

        public async Task IsFinished()
        {
            await IterFinished.Task;
        }

        public void StartTask(int taskId)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            lock (programState)
            {
                runtime.Assert(!programState.taskToTcs.ContainsKey(taskId), $"Duplicate declaration of task: {taskId}");
                programState.taskToTcs.Add(taskId, tcs);
                numPendingTaskCreations--;
            }

            tcs.Task.Wait();
        }

        public void Assert(bool value, string message)
        {
            runtime.Assert(value, message);
        }

        void WaitForPendingTaskCreations()
        {
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
        }
    }
}
