namespace MediaPlatform.Application.Admin;

public class UserNotFoundException() : Exception("Utilisateur introuvable.");
public class UnknownRoleException(string role) : Exception($"Rôle inconnu : {role}.");
