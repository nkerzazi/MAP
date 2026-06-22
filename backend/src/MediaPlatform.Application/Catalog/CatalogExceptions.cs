namespace MediaPlatform.Application.Catalog;

public class VideoNotFoundException() : Exception("Vidéo introuvable.");
public class NotVideoOwnerException() : Exception("Action réservée au propriétaire de la vidéo.");
public class InvalidVideoStateException(string detail) : Exception(detail);
public class CategoryNotFoundException(Guid id) : Exception($"Catégorie introuvable : {id}.");
public class DuplicateCategoryException(string name) : Exception($"Une catégorie nommée « {name} » existe déjà.");
public class CategoryInUseException(Guid id) : Exception($"Catégorie rattachée à des vidéos : {id}.");
