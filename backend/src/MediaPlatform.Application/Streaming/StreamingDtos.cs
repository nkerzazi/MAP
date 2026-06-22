namespace MediaPlatform.Application.Streaming;

/// <summary>Objet HLS streamé depuis le stockage (manifeste ou segment).</summary>
public record HlsObject(Stream Content, string ContentType);
