namespace RUNAapp.Models;

/// <summary>
/// Represents an authenticated user in the RUNA application.
/// </summary>
public class User
{
    public string Uid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Firebase Authentication response model.
/// </summary>
public class FirebaseAuthResponse
{
    public string IdToken { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ExpiresIn { get; set; } = string.Empty;
    public string LocalId { get; set; } = string.Empty;
    public bool Registered { get; set; }
}

/// <summary>
/// Firebase error response model.
/// </summary>
public class FirebaseErrorResponse
{
    public FirebaseError? Error { get; set; }
}

public class FirebaseError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<FirebaseErrorDetail>? Errors { get; set; }
}

public class FirebaseErrorDetail
{
    public string Message { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
