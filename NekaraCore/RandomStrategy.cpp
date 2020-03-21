#pragma once

#include "pch.h"
#include "RandomStrategy.h"

namespace NS
{
	int RandomStrategy::GetNextThread(std::vector<int> enabledThreads, ProjectState projectState)
	{
		return rand() % enabledThreads.size();
	}
}