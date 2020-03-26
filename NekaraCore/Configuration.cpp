#include "pch.h"
#include "Configuration.h"
#include <chrono>

namespace NS 
{
	Configuration::Configuration()
	{
		std::chrono::time_point<std::chrono::system_clock> now = std::chrono::system_clock::now();
		auto duration = now.time_since_epoch();
		auto nanoseconds = std::chrono::duration_cast<std::chrono::nanoseconds>(duration);

		test_seed = nanoseconds.count() % 9999;
	}
}