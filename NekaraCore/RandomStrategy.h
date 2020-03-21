#pragma once

#include "SchedulingStrategy.h"

namespace NS
{
	class RandomStrategy : SchedulingStrategy
	{
	public:
		virtual int GetNextThread(std::vector<int> enabledThreads, ProjectState projectState);

	};

}