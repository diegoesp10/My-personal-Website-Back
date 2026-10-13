using Microsoft.AspNetCore.Mvc;

namespace Portfolio.Api.Features.Editor;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalEditorOnlyAttribute() : ServiceFilterAttribute(typeof(LocalEditorAccessFilter));
