#pragma once

#include <iostream>
#include "ProjectState.h"
#include <mutex> 
#include <assert.h>
#include <chrono>
#include <thread>

#define MILLISEC 100
#define SEMAPHORETIMESPAN 100000L

namespace NS 
{

	std::mutex _obj;

	class NekaraService 
	{

	private:
		ProjectState _projectState;
		int _currentThread;
		int _seed;
		bool _debug = false;
		int _max_decisions;

		void WaitForPendingTaskCreations()
		{
			while (true)
			{
				_obj.lock();
				if (_projectState.numPendingTaskCreations == 0)
				{
					_obj.unlock();
					return;
				}
				_obj.unlock();

				std::this_thread::sleep_for(std::chrono::milliseconds(MILLISEC));
			}
		}

	public:
		NekaraService(int max_decisions)
		{
			// getting seed from Nanoseconds
			std::chrono::time_point<std::chrono::system_clock> now = std::chrono::system_clock::now();
			auto duration = now.time_since_epoch();
			auto nanoseconds = std::chrono::duration_cast<std::chrono::nanoseconds>(duration);

			_currentThread = 0;
			_max_decisions = max_decisions;
			_seed = nanoseconds.count() % 999;
			std::cout << "Your Program is being tested with random seed: " << _seed <<  " with max decisions: " << _max_decisions <<"\n";
			std::cout << "Give the same Seed and Max number of decision(s) to the Testing Service for a Re-Play." << "\n";
			srand(_seed);

			_obj.lock();
			HANDLE _obj1 = CreateSemaphoreW(NULL, 0, 1, NULL);
			_projectState._th_to_sem[0] = _obj1;
			_obj.unlock();
		}

		NekaraService(int _seed, int max_decisions)
		{
			_currentThread = 0;
			_max_decisions = max_decisions;
			this->_seed = _seed;
			std::cout << "Your Program is being tested with seed: " << this->_seed << "\n";
			srand(_seed);

			_obj.lock();
			HANDLE _obj1 = CreateSemaphoreW(NULL, 0, 1, NULL);
			_projectState._th_to_sem[0] = _obj1;
			_obj.unlock();
		}

		void CreateThread()
		{
			if (_debug) 
			{
				std::cout << "CT-entry" << "\n";
			}

			_obj.lock();
			_projectState.ThreadCreation();
			_obj.unlock();

			if (_debug) 
			{
				std::cout << "CT-exit" << "\n";
			}

		}

		void StartThread(int _threadID)
		{
			if (_debug) 
			{
				std::cout << "ST-entry: " << _threadID << "\n";
			}

			_obj.lock();
			_projectState.ThreadStarting(_threadID);
			HANDLE _obj1 = _projectState._th_to_sem.find(_threadID)->second;
			_obj.unlock();

			DWORD _dwWaitResult = WaitForSingleObject(_obj1, SEMAPHORETIMESPAN);
			if (_dwWaitResult == WAIT_TIMEOUT)
			{
				std::cerr << "Windows ERROR: Semaphore waiting Time-out." << ".\n";
				abort();
			}

			if (_debug) 
			{
				std::cout << "ST-exit: " << _threadID << "\n";
			}

		}

		void EndThread(int _threadID)
		{
			if (_debug) 
			{
				std::cout << "ET-entry: " << _threadID << "\n";
			}

			_obj.lock();
			_projectState.ThreadEnded(_threadID);
			_obj.unlock();

			ContextSwitch();

			if (_debug) 
			{
				std::cout << "ET-exit: " << _threadID << "\n";
			}

		}

		void CreateResource(int _resourceID)
		{
			_obj.lock();
			_projectState.AddResource(_resourceID);
			_obj.unlock();
		}

		void DeleteResource(int _resourceID)
		{
			_obj.lock();
			_projectState.RemoveResource(_resourceID);
			_obj.unlock();
		}

		void BlockedOnResource(int _resourceID)
		{
			_obj.lock();
			_projectState.BlockThreadOnResource(_currentThread, _resourceID);
			_obj.unlock();

			ContextSwitch();
		}

		void BlockedOnAnyResource(int _resourceID[], int _size)
		{
			_obj.lock();
			_projectState.BlockThreadonAnyResource(_currentThread, _resourceID, _size);
			_obj.unlock();

			ContextSwitch();
		}

		void SignalUpdatedResource(int _resourceID)
		{
			_obj.lock();
			_projectState.UnblockThreads(_resourceID);
			_obj.unlock();
		}

		bool CreateNondetBool()
		{
			bool _NondetBool;
			_obj.lock();
			_NondetBool = rand() % 2;
			_obj.unlock();

			return _NondetBool;
		}

		int CreateNondetInteger(int _maxValue)
		{
			int _NondetInteger;
			_obj.lock();
			_NondetInteger = rand() % _maxValue;
			_obj.unlock();

			return _NondetInteger;
		}

		void ContextSwitch()
		{
			if (_debug) 
			{
				std::cout << "CS-entry" << "\n";
			}

			WaitForPendingTaskCreations();

			int _next_threadID = -99;
			int _current_thread;
			bool _current_thread_running = false;
			HANDLE _next_obj1 = NULL;
			HANDLE _crr_obj1 = NULL;

			_obj.lock();

			
			if (_max_decisions < 0)
			{
				std::cerr << "ERROR: Maximum steps reached; the program might be in a live-lock state! (or the program might be a non-terminating program)" << ".\n";
				_obj.unlock();
				abort();
			}
			_max_decisions--;

			_current_thread = this->_currentThread;

			std::map<int, HANDLE>::iterator _ct_it = _projectState._th_to_sem.find(_current_thread);
			if (_ct_it != _projectState._th_to_sem.end())
			{
				_current_thread_running = true;
				_crr_obj1 = _ct_it->second;
			}

			int _size_t_s = _projectState._th_to_sem.size();
			int _size_b_t = _projectState._blocked_task.size();
			int _size = _size_t_s - _size_b_t;

			if (_size == 0 && _size_t_s != 0)
			{
				std::cerr << "ERROR: Deadlock detected" << ".\n";
				_obj.unlock();
				abort();
			}

			if (_size == 0 && _size_t_s == 0)
			{
				_obj.unlock();
				return;
			}

			int _randnum = rand() % _size;

			int _i = 0;

			for (std::map<int, HANDLE>::iterator _it = _projectState._th_to_sem.begin(); _it != _projectState._th_to_sem.end(); ++_it)
			{
				std::map<int, std::set<int>*>::iterator _bt_it = _projectState._blocked_task.find(_it->first);

				if (_bt_it == _projectState._blocked_task.end())
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
			_obj.unlock();

			if (_next_threadID == _current_thread)
			{
				// no op
			}
			else
			{
				_obj.lock();
				_currentThread = _next_threadID;
				_obj.unlock();

				ReleaseSemaphore(_next_obj1, 1, NULL);

				if (_current_thread_running)
				{
					DWORD _dwWaitResult = WaitForSingleObject(_crr_obj1, SEMAPHORETIMESPAN);
					if (_dwWaitResult)
					{
						std::cerr << "Windows ERROR: Semaphore waiting Time-out." << ".\n";
						abort();
					}
				}
			}

			if (_debug) 
			{
				std::cout << "CS-exit" << "\n";
			}
		}

		void WaitforMainTask()
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
	};
}
