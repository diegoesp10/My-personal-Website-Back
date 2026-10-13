using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Portfolio.Api.Features.Editor;

public sealed class LocalEditorAccessFilter(ILocalEditorAccess access) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!access.CanAccessProtectedEndpoints()) context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}
