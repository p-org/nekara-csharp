#pragma once

#include <vector>
#include "ProjectState.h"

namespace NS 
{
	class SchedulingStrategy
	{
	public:
		virtual int GetNextThread(const std::vector<int> &enabledThreads, const ProjectState &projectState) = 0;
	};
}