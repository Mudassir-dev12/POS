using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Models;

/// <summary>
/// Result of an authentication or login attempt.
/// </summary>
public class AuthResult
{
    /// <summary>
    /// Gets whether authentication was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the failure error message, if unsuccessful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets whether the authenticated user must immediately change their password.
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>
    /// Gets the authenticated user entity upon success.
    /// </summary>
    public User? User { get; init; }

    public static AuthResult Succeeded(User user, bool mustChangePassword = false) =>
        new()
        {
            Success = true,
            User = user,
            MustChangePassword = mustChangePassword
        };

    public static AuthResult Failed(string message) =>
        new()
        {
            Success = false,
            ErrorMessage = message
        };
}

