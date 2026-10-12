using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Portfolio.Api.Features.Editor;

public sealed class LocalEditorWriteFilter(ILocalEditorAccess access) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!access.CanWrite()) context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}
