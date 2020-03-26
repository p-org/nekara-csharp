#pragma once
#include <system_error> 

namespace NS
{
    class NekaraErrorCode : public std::error_category {
    public:
        virtual const char* name() const noexcept;
        virtual std::error_condition default_error_condition(int ev) const noexcept;
        virtual bool equivalent(const std::error_code& code, int condition) const noexcept;
        virtual std::string message(int ev) const;
    };
}