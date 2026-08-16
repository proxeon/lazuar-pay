using System;

namespace Modules.Commerce.Application;

public sealed class RefundRejectedException : InvalidOperationException
{
    public string Code { get; }

    public RefundRejectedException(string code, string message) : base(message)
    {
        Code = code;
    }
}
