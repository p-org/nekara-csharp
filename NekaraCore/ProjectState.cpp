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

	std::error_code ProjectState::ThreadStarting(int _threadID)
	{
		if (numPendingTaskCreations < 1)
		{
			return std::error_code(error_id::CreateThreadNotCalled, nec);
		}

		std::map<int, std::condition_variable*>::iterator _it1 = threadToSem.find(_threadID);
		if (_it1 != threadToSem.end())
		{
			return std::error_code(error_id::DuplicateThread, nec);
		}

		this->numPendingTaskCreations--;
		threadToSem[_threadID] = new std::condition_variable();

		return std::error_code(error_id::Success, nec);
	}

	std::error_code ProjectState::ThreadEnded(int _threadID)
	{
		std::map<int, std::condition_variable*>::iterator _it1 = threadToSem.find(_threadID);
		if (_it1 == threadToSem.end())
		{
			// std::cerr << "ERROR: EndTask/Thread called on unknown or already completed Task/Thread:" << _threadID << ".\n";
			return std::error_code(error_id::EndThreadError, nec);
		}

		threadToSem.erase(_it1);

		return std::error_code(error_id::Success, nec);
	}

	std::error_code ProjectState::AddResource(int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 != resourceIDs.end())
		{
			// std::cerr << "ERROR: Duplicate declaration of resource:" << _resourceID << ".\n";
			return std::error_code(error_id::DuplicateResource, nec);
		}

		resourceIDs.insert(_resourceID);

		return std::error_code(error_id::Success, nec);
	}

	std::error_code ProjectState::RemoveResource(int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 == resourceIDs.end())
		{
			// std::cerr << "ERROR: DeleteResource called on unknown or already deleted resource:" << _resourceID << ".\n";
			return std::error_code(error_id::ResourceAlreadyDeleted, nec);
		}

		for (std::map<int, std::set<int>*>::iterator _it = blockedTasks.begin(); _it != blockedTasks.end(); ++_it)
		{
			std::set<int>* _set1 = _it->second;
			if (_set1->find(_resourceID) != _set1->end())
			{
				// std::cerr << "ERROR: DeleteResource called on resource with id:" << _resourceID << ". But some tasks/threads are blocked on it.\n";
				return std::error_code(error_id::ThreadBlockedOnResource, nec);
			}
		}

		resourceIDs.erase(_resourceID);

		return std::error_code(error_id::Success, nec);
	}

	std::error_code ProjectState::BlockThreadOnResource(int _threadID, int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 == resourceIDs.end())
		{
			// std::cerr << "ERROR: Illegal operation, resource: " << _resourceID << " has not been declared/created.\n";
			return std::error_code(error_id::ResourceNotDecleared, nec);
		}

		std::map<int, std::set<int>*>::iterator _it2 = blockedTasks.find(_threadID);
		if (_it2 != blockedTasks.end())
		{
			// std::cerr << "ERROR: Illegal operation, task/thread: " << _threadID << " already blocked on a resource.\n";
			return std::error_code(error_id::ThreadAlreadyBlocked, nec);
		}

		std::set<int>* _set1 = new std::set<int>();
		_set1->insert(_resourceID);
		blockedTasks[_threadID] = _set1;

		return std::error_code(error_id::Success, nec);
	}

	std::error_code ProjectState::BlockThreadonAnyResource(int _threadID, int _resourceID[], int _size)
	{
		std::map<int, std::set<int>*>::iterator _it2 = blockedTasks.find(_threadID);
		if (_it2 != blockedTasks.end())
		{
			// std::cerr << "ERROR: Illegal operation, task/thread: " << _threadID << " already blocked on a resource.\n";
			return std::error_code(error_id::ThreadAlreadyBlockedAnyResource, nec);
		}

		std::set<int>* _set1 = new std::set<int>();

		for (int _i = 0; _i < _size; _i++)
		{
			std::set<int>::iterator _it1 = resourceIDs.find(_resourceID[_i]);
			if (_it1 == resourceIDs.end())
			{
				// std::cerr << "ERROR: Illegal operation, resource: " << _resourceID[_i] << " has not been declared/created.\n";
				return std::error_code(error_id::ResourceNotDeclearedAnyResource, nec);
			}
			_set1->insert(_resourceID[_i]);
		}

		blockedTasks[_threadID] = _set1;

		return std::error_code(error_id::Success, nec);
	}

	std::error_code ProjectState::UnblockThreads(int _resourceID)
	{
		std::set<int>::iterator _it1 = resourceIDs.find(_resourceID);
		if (_it1 == resourceIDs.end())
		{
			// std::cerr << "ERROR: Illegal operation, called on unknown or already deleted resource:" << _resourceID << ".\n";
			return std::error_code(error_id::DeletedResource, nec);
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

		return std::error_code(error_id::Success, nec);
	}

	void ProjectState::Reset()
	{
		numPendingTaskCreations = 0;
		threadToSem.clear();
		blockedTasks.clear();
		resourceIDs.clear();
	}
}