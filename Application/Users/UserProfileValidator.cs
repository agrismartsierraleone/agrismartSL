using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Users;

/// <summary>
/// API-level rules for the phone number and NIN. The database columns stay nullable for accounts
/// created before these fields existed, so the "required" rule lives here.
/// </summary>
public static class UserProfileValidator
{
    /// <summary>Validates a new account: buyers and sellers must supply both fields, admins may omit them.</summary>
    public static void EnsureValid(UserRole role, string? phoneNumber, string? nin)
    {
        if (role is UserRole.Buyer or UserRole.Seller)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                throw new ValidationException("Phone number is required.");
            }

            if (string.IsNullOrWhiteSpace(nin))
            {
                throw new ValidationException("NIN is required.");
            }
        }

        EnsureLengths(phoneNumber, nin);
    }

    /// <summary>Rejects values too long for their columns, so they return a 400 rather than a database error.</summary>
    public static void EnsureLengths(string? phoneNumber, string? nin)
    {
        if (phoneNumber?.Trim().Length > UserProfileLimits.PhoneNumberMaxLength)
        {
            throw new ValidationException($"Phone number must be at most {UserProfileLimits.PhoneNumberMaxLength} characters long.");
        }

        if (nin?.Trim().Length > UserProfileLimits.NinMaxLength)
        {
            throw new ValidationException($"NIN must be at most {UserProfileLimits.NinMaxLength} characters long.");
        }
    }
}
