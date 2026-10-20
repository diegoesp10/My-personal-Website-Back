namespace Portfolio.Application.Cv;

/// <summary>PDF of the CV in one language, with the file name the browser saves it under.</summary>
public sealed record StoredCv(Stream Content, string FileName);
