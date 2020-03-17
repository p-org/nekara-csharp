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

		ProjectState()
		{
			this->numPendingTaskCreations = 0;
		}

		void ThreadCreation()
		{
			this->numPendingTaskCreations++;
		}

		void ThreadStarting(int _threadID)
		{
			if (numPendingTaskCreations < 1)
			{
				std::cerr << "ERROR: Unexpected StartTask/Thread! StartTask/Thread with id '" << _threadID << "' called without calling CreateTask/Thread" << '\n';
				abort();
			}

			std::map<int, std::condition_variable*>::iterator _it1 = _th_to_sem.find(_threadID);
			if (_it1 != _th_to_sem.end())
			{
				std::cerr << "ERROR: Duplicate declaration of Task/Thread id:" << _threadID << ".\n";
				abort();
			}

			this->numPendingTaskCreations--;

			std::condition_variable* _obj1 = new std::condition_variable();
			_th_to_sem[_threadID] = _obj1;

		}

		void ThreadEnded(int _threadID)
		{
			std::map<int, std::condition_variable*>::iterator _it1 = _th_to_sem.find(_threadID);
			if (_it1 == _th_to_sem.end())
			{
				std::cerr << "ERROR: EndTask/Thread called on unknown or already completed Task/Thread:" << _threadID << ".\n";
				abort();
			}

			_th_to_sem.erase(_it1);
		}

		void AddResource(int _resourceID)
		{
			std::set<int>::iterator _it1 = _resourceIDs.find(_resourceID);
			if (_it1 != _resourceIDs.end())
			{
				std::cerr << "ERROR: Duplicate declaration of resource:" << _resourceID << ".\n";
				abort();
			}

			_resourceIDs.insert(_resourceID);
		}

		void RemoveResource(int _resourceID)
		{
			std::set<int>::iterator _it1 = _resourceIDs.find(_resourceID);
			if (_it1 == _resourceIDs.end())
			{
				std::cerr << "ERROR: DeleteResource called on unknown or already deleted resource:" << _resourceID << ".\n";
				abort();
			}

			for (std::map<int, std::set<int>*>::iterator _it = _blocked_task.begin(); _it != _blocked_task.end(); ++_it)
			{
				std::set<int>* _set1 = _it->second;
				if (_set1->find(_resourceID) != _set1->end())
				{
					std::cerr << "ERROR: DeleteResource called on resource with id:" << _resourceID << ". But some tasks/threads are blocked on it.\n";
					abort();
				}
			}

			_resourceIDs.erase(_resourceID);
		}

		void BlockThreadOnResource(int _threadID, int _resourceID)
		{
			std::set<int>::iterator _it1 = _resourceIDs.find(_resourceID);
			if (_it1 == _resourceIDs.end())
			{
				std::cerr << "ERROR: Illegal operation, resource: " << _resourceID << " has not been declared/created.\n";
				abort();
			}

			std::map<int, std::set<int>*>::iterator _it2 = _blocked_task.find(_threadID);
			if (_it2 != _blocked_task.end())
			{
				std::cerr << "ERROR: Illegal operation, task/thread: " << _threadID << " already blocked on a resource.\n";
				abort();
			}

			std::set<int>* _set1 = new std::set<int>();
			_set1->insert(_resourceID);

			_blocked_task[_threadID] = _set1;
		}

		void BlockThreadonAnyResource(int _threadID, int _resourceID[], int _size)
		{
			std::map<int, std::set<int>*>::iterator _it2 = _blocked_task.find(_threadID);
			if (_it2 != _blocked_task.end())
			{
				std::cerr << "ERROR: Illegal operation, task/thread: " << _threadID << " already blocked on a resource.\n";
				abort();
			}

			std::set<int>* _set1 = new std::set<int>();

			for (int _i = 0; _i < _size; _i++)
			{
				std::set<int>::iterator _it1 = _resourceIDs.find(_resourceID[_i]);
				if (_it1 == _resourceIDs.end())
				{
					std::cerr << "ERROR: Illegal operation, resource: " << _resourceID[_i] << " has not been declared/created.\n";
					abort();
				}
				_set1->insert(_resourceID[_i]);
			}

			_blocked_task[_threadID] = _set1;
		}

		void UnblockThreads(int _resourceID)
		{
			std::set<int>::iterator _it1 = _resourceIDs.find(_resourceID);
			if (_it1 == _resourceIDs.end())
			{
				std::cerr << "ERROR: Illegal operation, called on unknown or already deleted resource:" << _resourceID << ".\n";
				abort();
			}

			std::stack<int> _stack1;

			for (std::map<int, std::set<int>*>::iterator _it = _blocked_task.begin(); _it != _blocked_task.end(); ++_it)
			{
				std::set<int>* _set1 = _it->second;
				if (_set1->find(_resourceID) != _set1->end())
				{
					_stack1.push(_it->first);
				}
			}

			while (!_stack1.empty())
			{
				std::map<int, std::set<int>*>::iterator _it2 = _blocked_task.find(_stack1.top());
				_blocked_task.erase(_it2);
				_stack1.pop();
			}
		}
	};
}