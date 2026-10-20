using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Cv;

public sealed class CvService(ICvRepository repository) : ICvService
{
    public Task<StoredCv?> GetAsync(string language, CancellationToken cancellationToken)
        => ContentLanguage.IsSupported(language)
            ? repository.OpenReadAsync(language, cancellationToken)
            : Task.FromResult<StoredCv?>(null);
}
