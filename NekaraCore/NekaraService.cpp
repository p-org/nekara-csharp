#pragma once

#include "pch.h"
#include "NekaraService.h"

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
		max_decisions = config.max_decisions;
		seed = config.test_seed;

		std::cout << "Your Program is being tested with random seed: " << seed << " with max decisions: " << max_decisions << "\n";
		std::cout << "Give the same Seed and Max number of decision(s) to the Testing Service for a Re-Play." << "\n";
		srand(seed);

		projectState.threadToSem[0] = new std::condition_variable();
	}

	void NekaraService::CreateThread()
	{
		if (_debug)
		{
			std::cout << "CT-entry" << "\n";
		}

		nsLock.lock();
		projectState.ThreadCreation();
		nsLock.unlock();

		if (_debug)
		{
			std::cout << "CT-exit" << "\n";
		}

	}

	void NekaraService::StartThread(int _threadID)
	{
		if (_debug)
		{
			std::cout << "ST-entry: " << _threadID << "\n";
		}

		nsLock.lock();
		projectState.ThreadStarting(_threadID);
		std::condition_variable* cv = projectState.threadToSem.find(_threadID)->second;
		nsLock.unlock();

		std::mutex dummy_mutex;
		std::unique_lock<std::mutex> unique_lock(dummy_mutex);

		cv->wait(unique_lock);
		unique_lock.unlock();

		if (_debug)
		{
			std::cout << "ST-exit: " << _threadID << "\n";
		}

	}

	void NekaraService::EndThread(int _threadID)
	{
		if (_debug)
		{
			std::cout << "ET-entry: " << _threadID << "\n";
		}

		nsLock.lock();
		projectState.ThreadEnded(_threadID);
		nsLock.unlock();

		ContextSwitch();

		if (_debug)
		{
			std::cout << "ET-exit: " << _threadID << "\n";
		}

	}

	void NekaraService::CreateResource(int _resourceID)
	{
		nsLock.lock();
		projectState.AddResource(_resourceID);
		nsLock.unlock();
	}

	void NekaraService::DeleteResource(int _resourceID)
	{
		nsLock.lock();
		projectState.RemoveResource(_resourceID);
		nsLock.unlock();
	}

	void NekaraService::BlockedOnResource(int _resourceID)
	{
		nsLock.lock();
		projectState.BlockThreadOnResource(currentThread, _resourceID);
		nsLock.unlock();

		ContextSwitch();
	}

	void NekaraService::BlockedOnAnyResource(int _resourceID[], int _size)
	{
		nsLock.lock();
		projectState.BlockThreadonAnyResource(currentThread, _resourceID, _size);
		nsLock.unlock();

		ContextSwitch();
	}

	void NekaraService::SignalUpdatedResource(int _resourceID)
	{
		nsLock.lock();
		projectState.UnblockThreads(_resourceID);
		nsLock.unlock();
	}

	bool NekaraService::CreateNondetBool()
	{
		return rand() % 2;
	}

	int NekaraService::CreateNondetInteger(int _maxValue)
	{
		return rand() % _maxValue;
	}

	void NekaraService::Assert(bool value, std::string message)
	{
		if (!value)
		{
			std::cerr << message << ".\n";
			abort();
		}
	}

	void NekaraService::ContextSwitch()
	{
		if (_debug)
		{
			std::cout << "CS-entry" << "\n";
		}

		WaitForPendingTaskCreations();

		int _next_threadID = -99;
		int _current_thread;
		bool _current_thread_running = false;
		std::condition_variable* _next_obj1 = NULL;
		std::condition_variable* _crr_obj1 = NULL;

		nsLock.lock();


		if (max_decisions < 0)
		{
			std::cerr << "ERROR: Maximum steps reached; the program might be in a live-lock state! (or the program might be a non-terminating program)" << ".\n";
			nsLock.unlock();
			abort();
		}
		max_decisions--;

		_current_thread = this->currentThread;

		std::map<int, std::condition_variable*>::iterator _ct_it = projectState.threadToSem.find(_current_thread);
		if (_ct_it != projectState.threadToSem.end())
		{
			_current_thread_running = true;
			_crr_obj1 = _ct_it->second;
		}

		int _size_t_s = (int) projectState.threadToSem.size();
		int _size_b_t = (int) projectState.blockedTasks.size();
		int _size = _size_t_s - _size_b_t;

		if (_size == 0 && _size_t_s != 0)
		{
			std::cerr << "ERROR: Deadlock detected" << ".\n";
			nsLock.unlock();
			abort();
		}

		if (_size == 0 && _size_t_s == 0)
		{
			nsLock.unlock();
			return;
		}

		int _randnum = rand() % _size;

		int _i = 0;

		for (std::map<int, std::condition_variable*>::iterator _it = projectState.threadToSem.begin(); _it != projectState.threadToSem.end(); ++_it)
		{
			std::map<int, std::set<int>*>::iterator _bt_it = projectState.blockedTasks.find(_it->first);

			if (_bt_it == projectState.blockedTasks.end())
			{
				if (_i == _randnum)
				{
					// std::cout << "Ctrl given to TaskID:" << _it->first << " Random:" << _t1 << " A:" << _size_t_s << " B: " << _size_b_t  <<  "\n";

					_next_threadID = _it->first;
					_next_obj1 = _it->second;
					break;
				}
				_i++;
			}
		}
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

		if (_debug)
		{
			std::cout << "CS-exit" << "\n";
		}
	}

	void NekaraService::WaitforMainTask()
	{
		if (_debug)
		{
			std::cout << "WMT-entry" << "\n";
		}

		EndThread(0);

		if (_debug)
		{
			std::cout << "WMT-exit" << "\n";
		}
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
