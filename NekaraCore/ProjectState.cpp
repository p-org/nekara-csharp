#include "pch.h"
#include "ProjectState.h"

namespace NS
{

	ProjectState::ProjectState()
	{
		this->numPendingTaskCreations = 0;
	}

	void ProjectState::ThreadCreation()
	{
		this->numPendingTaskCreations++;
	}

	void ProjectState::ThreadStarting(int _threadID)
	{
		if (numPendingTaskCreations < 1)
		{
			std::cerr << "ERROR: Unexpected StartTask/Thread! StartTask/Thread with id '" << _threadID << "' called without calling CreateTask/Thread" << '\n';
			abort();
		}

		std::map<int, std::condition_variable*>::iterator _it1 = threadToSem.find(_threadID);
		if (_it1 != threadToSem.end())
		{
			std::cerr << "ERROR: Duplicate declaration of Task/Thread id:" << _threadID << ".\n";
			abort();
		}

		this->numPendingTaskCreations--;

		threadToSem[_threadID] = new std::condition_variable();
	}

	void ProjectState::ThreadEnded(int _threadID)
	{
		std::map<int, std::condition_variable*>::iterator _it1 = threadToSem.find(_threadID);
		if (_it1 == threadToSem.end())
		{
			std::cerr << "ERROR: EndTask/Thread called on unknown or already completed Task/Thread:" << _threadID << ".\n";
			abort();
		}

		threadToSem.erase(_it1);
	}

	void ProjectState::AddResource(int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 != resourceIDs.end())
		{
			std::cerr << "ERROR: Duplicate declaration of resource:" << _resourceID << ".\n";
			abort();
		}

		resourceIDs.insert(_resourceID);
	}

	void ProjectState::RemoveResource(int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 == resourceIDs.end())
		{
			std::cerr << "ERROR: DeleteResource called on unknown or already deleted resource:" << _resourceID << ".\n";
			abort();
		}

		for (std::map<int, std::set<int>*>::iterator _it = blockedTasks.begin(); _it != blockedTasks.end(); ++_it)
		{
			std::set<int>* _set1 = _it->second;
			if (_set1->find(_resourceID) != _set1->end())
			{
				std::cerr << "ERROR: DeleteResource called on resource with id:" << _resourceID << ". But some tasks/threads are blocked on it.\n";
				abort();
			}
		}

		resourceIDs.erase(_resourceID);
	}

	void ProjectState::BlockThreadOnResource(int _threadID, int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 == resourceIDs.end())
		{
			std::cerr << "ERROR: Illegal operation, resource: " << _resourceID << " has not been declared/created.\n";
			abort();
		}

		std::map<int, std::set<int>*>::iterator _it2 = blockedTasks.find(_threadID);
		if (_it2 != blockedTasks.end())
		{
			std::cerr << "ERROR: Illegal operation, task/thread: " << _threadID << " already blocked on a resource.\n";
			abort();
		}

		std::set<int>* _set1 = new std::set<int>();
		_set1->insert(_resourceID);

		blockedTasks[_threadID] = _set1;
	}

	void ProjectState::BlockThreadonAnyResource(int _threadID, int _resourceID[], int _size)
	{
		std::map<int, std::set<int>*>::iterator _it2 = blockedTasks.find(_threadID);
		if (_it2 != blockedTasks.end())
		{
			std::cerr << "ERROR: Illegal operation, task/thread: " << _threadID << " already blocked on a resource.\n";
			abort();
		}

		std::set<int>* _set1 = new std::set<int>();

		for (int _i = 0; _i < _size; _i++)
		{
			std::set<int>::iterator _it1 = resourceIDs.find(_resourceID[_i]);
			if (_it1 == resourceIDs.end())
			{
				std::cerr << "ERROR: Illegal operation, resource: " << _resourceID[_i] << " has not been declared/created.\n";
				abort();
			}
			_set1->insert(_resourceID[_i]);
		}

		blockedTasks[_threadID] = _set1;
	}

	void ProjectState::UnblockThreads(int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 == resourceIDs.end())
		{
			std::cerr << "ERROR: Illegal operation, called on unknown or already deleted resource:" << _resourceID << ".\n";
			abort();
		}

		std::stack<int> _stack1;

		for (std::map<int, std::set<int>*>::iterator _it = blockedTasks.begin(); _it != blockedTasks.end(); ++_it)
		{
			std::set<int>* _set1 = _it->second;
			if (_set1->find(_resourceID) != _set1->end())
			{
				_stack1.push(_it->first);
			}
		}

		while (!_stack1.empty())
		{
			std::map<int, std::set<int>*>::iterator _it2 = blockedTasks.find(_stack1.top());
			blockedTasks.erase(_it2);
			_stack1.pop();
		}
	}

}