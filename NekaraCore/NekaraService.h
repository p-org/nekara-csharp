#pragma once

#include <iostream>
#include "ProjectState.h"
#include <mutex> 
#include <assert.h>
#include <thread>
#include "Configuration.h"
#include "SchedulingStrategy.h"

namespace NS 
{
	class NekaraService 
	{
	private:
		
		ProjectState projectState;
		int currentThread;
		int seed;
		bool attach_ns = false;
		std::mutex nsLock;
		SchedulingStrategy* sch;

	public:
		NekaraService();

		NekaraService(Configuration config);

		void Attach();

		void Detach();

		bool IsDetached();

		void CreateThread();

		std::error_code StartThread(int _threadID);

		std::error_code EndThread(int _threadID);

		std::error_code CreateResource(int _resourceID);

		std::error_code DeleteResource(int _resourceID);

		std::error_code BlockedOnResource(int _resourceID);

		std::error_code BlockedOnAnyResource(int _resourceID[], int _size);

		std::error_code SignalUpdatedResource(int _resourceID);

		bool CreateNondetBool();

		int CreateNondetInteger(int _maxValue);

		std::error_code ContextSwitch();

		std::error_code WaitforMainTask();

	private:
		void WaitForPendingTaskCreations();
	};
}
