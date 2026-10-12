namespace Portfolio.Application.Images;

public sealed record ImageFormat(string Extension, string ContentType);
public sealed record UploadedImage(string FileName);
public sealed record StoredImage(Stream Content, string ContentType);
