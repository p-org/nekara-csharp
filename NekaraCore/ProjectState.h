#pragma once

#include <iostream>
#include <set>
#include <list>
#include <map>
#include <stack>
#include <mutex>
#include <cassert>
#include <condition_variable>

namespace NS 
{

	class ProjectState 
	{

	public:
		int numPendingTaskCreations;
		std::map<int, std::condition_variable*> _th_to_sem;
		std::map<int, std::set<int>*> _blocked_task;
		std::set<int> _resourceIDs;

		ProjectState();

		void ThreadCreation();

		void ThreadStarting(int _threadID);

		void ThreadEnded(int _threadID);

		void AddResource(int _resourceID);

		void RemoveResource(int _resourceID);

		void BlockThreadOnResource(int _threadID, int _resourceID);

		void BlockThreadonAnyResource(int _threadID, int _resourceID[], int _size);

		void UnblockThreads(int _resourceID);
	};
}