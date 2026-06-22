namespace MediaPlatform.Application.Auth;

/// <summary>Email déjà utilisé lors de l'inscription → 409.</summary>
public class EmailAlreadyUsedException(string email)
    : Exception($"L'adresse « {email} » est déjà utilisée.");

/// <summary>Identifiants invalides / compte inactif → 401.</summary>
public class InvalidCredentialsException() : Exception("Identifiants invalides.");
