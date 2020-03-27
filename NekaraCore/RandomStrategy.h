#pragma once

#include "SchedulingStrategy.h"

namespace NS
{
	class RandomStrategy : SchedulingStrategy
	{
	public:
		virtual int GetNextThread(const std::vector<int> &enabledThreads, const ProjectState &projectState);

	};

}