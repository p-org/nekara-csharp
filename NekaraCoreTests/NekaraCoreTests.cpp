#include "pch.h"
#include "CppUnitTest.h"
#include "..\NekaraCore\NekaraService.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NekaraCoreTests
{
	TEST_CLASS(NekaraCoreTests)
	{
	public:
		
		TEST_METHOD(TestInitService)
		{
			NS::NekaraService* ns = new NS::NekaraService();

			ns->Attach();
			Assert::IsFalse(ns->IsDetached());
			ns->Detach();
			Assert::IsTrue(ns->IsDetached());
		}
	};
}
