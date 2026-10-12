namespace Portfolio.Api.Features.Editor;

public interface ILocalEditorAccess
{
    bool IsAvailable();
    bool CanWrite();
}
