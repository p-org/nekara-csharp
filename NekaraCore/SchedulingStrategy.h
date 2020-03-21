#pragma once

#include <vector>
#include "ProjectState.h"

namespace NS 
{
	class SchedulingStrategy
	{
	public:
		virtual int GetNextThread(std::vector<int> enabledThreads, ProjectState projectState) = 0;
	};
}