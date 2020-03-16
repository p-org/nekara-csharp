#pragma once
#include <mutex> 

namespace NS 
{
	std::mutex helper_obj;

	class Helpers
	{	
	public:
		static int GetThreadID()
		{
			static int thread_id = 1000;

			int threadID;
			helper_obj.lock();
			threadID = thread_id;
			thread_id++;
			helper_obj.unlock();

			return threadID;
		}

		static int GetResourceID()
		{
			static int resource_id = 100000;

			int resourceID;
			helper_obj.lock();
			resourceID = resource_id;
			resource_id++;
			helper_obj.unlock();

			return resourceID;
		}

	};
}
