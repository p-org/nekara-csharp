using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Nekara.Core
{
    class ProgramState
    {
        // All tasks in the program and their TCS
        public Dictionary<int, TaskCompletionSource<bool>> taskToTcs;

        // Set of all resources in the system
        public HashSet<int> resourceSet;

        // task -> the DISJUNCTIVE set of resources it's blocked on (if any). If any of the resources are freed, the task should unblock.
        public Dictionary<int, int[]> blockedTasks;

        // current executing task
        public int currentTask;

        // pending Task creations
        public int numPendingTaskCreations { get; private set; }

        public ProgramState()
        {
            taskToTcs = new Dictionary<int, TaskCompletionSource<bool>>();
            resourceSet = new HashSet<int>();
            blockedTasks = new Dictionary<int, int[]>();

            InitMainTask();
        }

        private void InitMainTask()
        {
            numPendingTaskCreations = 0;
            currentTask = 0;
            taskToTcs.Add(0, new TaskCompletionSource<bool>());
            taskToTcs[0].SetResult(true);
        }

        public string GetCurrentStateString()
        {
            return string.Join(", ", GetAllTasksTuple().Select(tup => (tup.Item1 == currentTask ? "*" : "") + tup.Item1.ToString() + (tup.Item2.Length > 0 ? "|" + string.Join(",", tup.Item2) + "|" : "")));
        }

        public (int, int[])[] GetAllTasksTuple()
        {
            return this.taskToTcs.Keys
                .OrderBy(key => key)
                .Select(taskId => this.blockedTasks.ContainsKey(taskId) ? (taskId, this.blockedTasks[taskId]) : (taskId, new int[0]))
                .ToArray();
        }

        public bool HasTask(int taskId)
        {
            return this.taskToTcs.ContainsKey(taskId);
        }

        public bool HasResource(int resourceId)
        {
            return this.resourceSet.Contains(resourceId);
        }

        public bool IsBlockedOnTask(int taskId)
        {
            return this.blockedTasks.ContainsKey(taskId);
        }

        public void InitTaskCreation()
        {
            numPendingTaskCreations++;
        }

        public TaskCompletionSource<bool> AddTask(int taskId)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            this.taskToTcs.Add(taskId, tcs);
            numPendingTaskCreations--;
            return tcs;
        }

        public void RemoveTask(int taskId)
        {
            this.taskToTcs.Remove(taskId);
        }

        public void AddResource(int resourceId)
        {
            this.resourceSet.Add(resourceId);
        }

        public void RemoveResource(int resourceId)
        {
            this.resourceSet.Remove(resourceId);
        }

        public bool SafeToDeleteResource(int resourceId)
        {
            return this.resourceSet.Contains(resourceId) && !this.blockedTasks.Values.Any(val => val.Contains(resourceId));
        }

        public void BlockTaskOnAnyResource(int taskId, params int[] resourceIds)
        {
            this.blockedTasks[taskId] = resourceIds;
        }

        public void UnblockTask(int taskId)
        {
            this.blockedTasks.Remove(taskId);
        }
    }
}
