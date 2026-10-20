namespace Portfolio.Application.Cv;

public interface ICvRepository
{
    Task<StoredCv?> OpenReadAsync(string language, CancellationToken cancellationToken);
}
