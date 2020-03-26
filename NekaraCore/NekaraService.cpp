#pragma once

#include "pch.h"
#include "NekaraService.h"
#include "RandomStrategy.h"
#include "PctStrategy.h"

#define WAITFORPENDINGTASKSLEEPTIME 1

namespace NS
{
	NekaraService::NekaraService()
		: NekaraService(Configuration())
	{
	}

	NekaraService::NekaraService(Configuration config)
	{
		currentThread = 0;
		seed = config.test_seed;

		std::cout << "Your Program is being tested with random seed: " << seed << ".\n";
		std::cout << "Give the same Seed to the Testing Service for a Re-Play." << "\n";
		srand(seed);

		projectState.threadToSem[0] = new std::condition_variable();
		sch = (SchedulingStrategy*) new RandomStrategy();
	}

	void NekaraService::Attach()
	{
		attach_ns = true;
	}

	void NekaraService::Detach()
	{
		attach_ns = false;

		nsLock.lock();

		std::map<int, std::condition_variable*>::iterator _it1 = projectState.threadToSem.find(currentThread);
		if (_it1 != projectState.threadToSem.end())
		{
			projectState.threadToSem.erase(_it1);
		}

		for (std::map<int, std::condition_variable*>::iterator it = projectState.threadToSem.begin();
			it != projectState.threadToSem.end(); ++it)
		{
			it->second->notify_one();
		}

		nsLock.unlock();
	}

	bool NekaraService::IsDetached()
	{
		return attach_ns;
	}

	void NekaraService::CreateThread()
	{
		if (!attach_ns)
		{
			return;
		}

		nsLock.lock();
		projectState.ThreadCreation();
		nsLock.unlock();
	}

	std::error_code NekaraService::StartThread(int _threadID)
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		nsLock.lock();
		std::error_code ps_ec = projectState.ThreadStarting(_threadID);
		if (ps_ec.value() != 0)
		{
			nsLock.unlock();
			Detach();
			return ps_ec;
		}
		std::condition_variable* cv = projectState.threadToSem.find(_threadID)->second;
		nsLock.unlock();

		std::mutex dummy_mutex;
		std::unique_lock<std::mutex> unique_lock(dummy_mutex);
		cv->wait(unique_lock);
		unique_lock.unlock();

		return ps_ec;
	}

	std::error_code NekaraService::EndThread(int _threadID)
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		nsLock.lock();
		std::error_code ps_ec = projectState.ThreadEnded(_threadID);
		if (ps_ec.value() != 0)
		{
			nsLock.unlock();
			Detach();
			return ps_ec;
		}
		nsLock.unlock();

		return ContextSwitch();
	}

	std::error_code NekaraService::CreateResource(int _resourceID)
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		nsLock.lock();
		std::error_code ps_ec = projectState.AddResource(_resourceID);
		if (ps_ec.value() != 0)
		{
			nsLock.unlock();
			Detach();
			return ps_ec;
		}
		nsLock.unlock();

		return ps_ec;
	}

	std::error_code NekaraService::DeleteResource(int _resourceID)
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		nsLock.lock();
		std::error_code ps_ec = projectState.RemoveResource(_resourceID);
		if (ps_ec.value() != 0)
		{
			nsLock.unlock();
			Detach();
			return ps_ec;
		}
		nsLock.unlock();

		return ps_ec;
	}

	std::error_code NekaraService::BlockedOnResource(int _resourceID)
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		nsLock.lock();
		std::error_code ps_ec = projectState.BlockThreadOnResource(currentThread, _resourceID);
		if (ps_ec.value() != 0)
		{
			nsLock.unlock();
			Detach();
			return ps_ec;
		}
		nsLock.unlock();

		return ContextSwitch();
	}

	std::error_code NekaraService::BlockedOnAnyResource(int _resourceID[], int _size)
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		nsLock.lock();
		std::error_code ps_ec = projectState.BlockThreadonAnyResource(currentThread, _resourceID, _size);
		if (ps_ec.value() != 0)
		{
			nsLock.unlock();
			Detach();
			return ps_ec;
		}
		nsLock.unlock();

		return ContextSwitch();
	}

	std::error_code NekaraService::SignalUpdatedResource(int _resourceID)
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		nsLock.lock();
		std::error_code ps_ec = projectState.UnblockThreads(_resourceID);
		if (ps_ec.value() != 0)
		{
			nsLock.unlock();
			Detach();
			return ps_ec;
		}
		nsLock.unlock();

		return ps_ec;
	}

	bool NekaraService::CreateNondetBool()
	{
		return rand() % 2;
	}

	int NekaraService::CreateNondetInteger(int _maxValue)
	{
		return rand() % _maxValue;
	}

	std::error_code NekaraService::ContextSwitch()
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		WaitForPendingTaskCreations();

		int _next_threadID = -99;
		int _current_thread;
		bool _current_thread_running = false;
		std::condition_variable* _next_obj1 = NULL;
		std::condition_variable* _crr_obj1 = NULL;

		nsLock.lock();

		_current_thread = this->currentThread;

		std::map<int, std::condition_variable*>::iterator _ct_it = projectState.threadToSem.find(_current_thread);
		if (_ct_it != projectState.threadToSem.end())
		{
			_current_thread_running = true;
			_crr_obj1 = _ct_it->second;
		}


		int numThreads = (int) projectState.threadToSem.size();
		std::vector<int> enabledThreads;

		for (std::map<int, std::condition_variable*>::iterator it = projectState.threadToSem.begin();
			it != projectState.threadToSem.end(); ++it)
		{
			if (projectState.blockedTasks.find(it->first) == projectState.blockedTasks.end())
			{
				enabledThreads.push_back(it->first);
			}
		}
		int numEnabledThreads = (int) enabledThreads.size();

		if (numEnabledThreads == 0 && numThreads != 0)
		{
			// std::cerr << "ERROR: Deadlock detected" << ".\n";
			nsLock.unlock();
			Detach();
			return std::error_code(1011, *(projectState.nec));
		}

		if (numEnabledThreads == 0 && numThreads == 0)
		{
			nsLock.unlock();
			return std::error_code(0, *(projectState.nec));
		}

		int next = sch->GetNextThread(enabledThreads, projectState);
		_next_threadID = enabledThreads[next];
		_next_obj1 = projectState.threadToSem.find(_next_threadID)->second;

		nsLock.unlock();

		if (_next_threadID == _current_thread)
		{
			// no op
		}
		else
		{
			nsLock.lock();
			currentThread = _next_threadID;
			nsLock.unlock();

			_next_obj1->notify_one();
			if (_current_thread_running)
			{
				std::mutex dummy_mutex;
				std::unique_lock<std::mutex> unique_lock(dummy_mutex);
				_crr_obj1->wait(unique_lock);
				unique_lock.unlock();
			}
		}

		return std::error_code(0, *(projectState.nec));
	}

	std::error_code NekaraService::WaitforMainTask()
	{
		if (!attach_ns)
		{
			return std::error_code(1012, *(projectState.nec));
		}

		EndThread(0);

		return std::error_code(0, *(projectState.nec));
	}

	void NekaraService::WaitForPendingTaskCreations()
	{
		while (true)
		{
			nsLock.lock();
			if (projectState.numPendingTaskCreations == 0)
			{
				nsLock.unlock();
				return;
			}
			nsLock.unlock();

			std::this_thread::sleep_for(std::chrono::milliseconds(WAITFORPENDINGTASKSLEEPTIME));
		}
	}
}
