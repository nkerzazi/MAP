namespace MediaPlatform.Application.Engagement;

public class CommentNotFoundException() : Exception("Commentaire introuvable.");
public class NotCommentAuthorException() : Exception("Action réservée à l'auteur du commentaire.");
