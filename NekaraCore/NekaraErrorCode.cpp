#include "pch.h"
#include "NekaraErrorCode.h"

enum class custom_errc { success = 0, nekara_error, other };
namespace std 
{
    template<> struct is_error_condition_enum<custom_errc> : public true_type {};
}

std::error_condition make_error_condition(custom_errc e) 
{
    NS::NekaraErrorCode custom_category;
    return std::error_condition(static_cast<int>(e), custom_category);
}

namespace NS
{
    const char* NekaraErrorCode::name() const noexcept
    {
        return "custom";
    }

    std::error_condition NekaraErrorCode::default_error_condition(int ev) const noexcept
    {
        if ((ev >= 1000) && (ev < 1020))
        {
            return std::error_condition(custom_errc::nekara_error);
        }
        else
        {
            return std::error_condition(custom_errc::other);
        }
    }

    bool NekaraErrorCode::equivalent(const std::error_code& code, int condition) const noexcept 
    {
        return *this == code.category() &&
            static_cast<int>(default_error_condition(code.value()).value()) == condition;
    }

    std::string NekaraErrorCode::message(int ev) const 
    {
        switch (ev) {
        case 0:
            return "SUCCESS: No Error!";
        case 1000:
            return "StartThread: Unexpected StartTask/Thread! StartTask/Thread called without calling CreateTask/Thread.";
        case 1001:
            return "StartThread: Duplicate declaration of Task/Thread.";
        case 1002:
            return "EndThread: EndTask/Thread called on unknown or already completed Task/Thread.";
        case 1003:
            return "CreateResource: Duplicate declaration of resource.";
        case 1004:
            return "DeleteResource: DeleteResource called on unknown or already deleted resource.";
        case 1005:
            return "DeleteResource: DeleteResource called on a resource. But some tasks/threads are blocked on it.";
        case 1006:
            return "BlockThreadOnResource: Illegal operation, resource has not been declared/created.";
        case 1007:
            return "BlockThreadOnResource: Illegal operation, task/thread already blocked on a resource.";
        case 1008:
            return "BlockedOnAnyResource: Illegal operation, task/thread already blocked on a resource.";
        case 1009:
            return "BlockedOnAnyResource: Illegal operation, resource has not been declared/created.";
        case 1010:
            return "SignalUpdatedResource: Illegal operation, called on unknown or already deleted resource.";
        case 1011:
            return "!!Deadlock!!";
        case 1012:
            return "NekaraService is Detached! Either not attached or got detached because of an error.";
        default:
            return "Unknown error.";
        }
    }
}