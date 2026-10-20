namespace Portfolio.Application.Cv;

public interface ICvService
{
    /// <summary>The CV of a supported language, or null when that language has none.</summary>
    Task<StoredCv?> GetAsync(string language, CancellationToken cancellationToken);
}
