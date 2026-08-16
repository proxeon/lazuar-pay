using System;

namespace Modules.Payments.Application;

/// <summary>
/// Thrown by vaulted off-session adapters so the decline code is not swallowed as a boolean false.
/// </summary>
public sealed class OffSessionDeclinedException : Exception
{
    public string? DeclineCode { get; }

    public OffSessionDeclinedException(string? declineCode, string? message = null)
        : base(message ?? "Off-session charge declined.")
    {
        DeclineCode = string.IsNullOrWhiteSpace(declineCode) ? null : declineCode.Trim();
    }
}
