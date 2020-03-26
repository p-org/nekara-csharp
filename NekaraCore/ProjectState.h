#pragma once

#include <iostream>
#include <set>
#include <list>
#include <map>
#include <stack>
#include <mutex>
#include <cassert>
#include <condition_variable>
#include "NekaraErrorCode.h"

namespace NS 
{
	class ProjectState 
	{

	public:
		int numPendingTaskCreations;
		std::map<int, std::condition_variable*> threadToSem;
		std::map<int, std::set<int>*> blockedTasks;
		std::set<int> resourceIDs;
		NekaraErrorCode* nec;

		ProjectState();

		void ThreadCreation();

		std::error_code ThreadStarting(int _threadID);

		std::error_code ThreadEnded(int _threadID);

		std::error_code AddResource(int _resourceID);

		std::error_code RemoveResource(int _resourceID);

		std::error_code BlockThreadOnResource(int _threadID, int _resourceID);

		std::error_code BlockThreadonAnyResource(int _threadID, int _resourceID[], int _size);

		std::error_code UnblockThreads(int _resourceID);
	};
}