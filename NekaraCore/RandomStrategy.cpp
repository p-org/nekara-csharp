#pragma once

#include "pch.h"
#include "RandomStrategy.h"

namespace NS
{
	int RandomStrategy::GetNextThread(const std::vector<int> &enabledThreads, const ProjectState &projectState)
	{
		return rand() % enabledThreads.size();
	}
}