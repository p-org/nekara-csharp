#pragma once

#include <iostream>
#include "ProjectState.h"
#include <mutex> 
#include <assert.h>
#include <thread>
#include "Configuration.h"

namespace NS 
{
	class NekaraService 
	{
	private:
		
		ProjectState projectState;
		int currentThread;
		int seed;
		bool _debug = false;
		int max_decisions;
		std::mutex nsLock;

	public:
		NekaraService();

		NekaraService(Configuration config);

		void CreateThread();

		void StartThread(int _threadID);

		void EndThread(int _threadID);

		void CreateResource(int _resourceID);

		void DeleteResource(int _resourceID);

		void BlockedOnResource(int _resourceID);

		void BlockedOnAnyResource(int _resourceID[], int _size);

		void SignalUpdatedResource(int _resourceID);

		bool CreateNondetBool();

		int CreateNondetInteger(int _maxValue);

		void Assert(bool value, std::string message);

		void ContextSwitch();

		void WaitforMainTask();

	private:
		void WaitForPendingTaskCreations();
	};
}
