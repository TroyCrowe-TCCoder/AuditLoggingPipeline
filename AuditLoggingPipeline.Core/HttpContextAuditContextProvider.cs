using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AuditLoggingPipeline;

/// <summary>
/// Default <see cref="IAuditContextProvider"/>: reads the actor, IP address and correlation id from
/// the current HTTP request. Register your own provider when the actor or display name must come
/// from elsewhere, for example an active-user cache.
/// </summary>
public sealed class HttpContextAuditContextProvider : IAuditContextProvider
{
    private const string ObjectIdentifierClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>Creates the provider. A null accessor yields <see cref="AuditContext.Empty"/>.</summary>
    public HttpContextAuditContextProvider(IHttpContextAccessor? httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public AuditContext GetContext() =>
        _httpContextAccessor?.HttpContext is { } httpContext ? Build(httpContext) : AuditContext.Empty;

    private static AuditContext Build(HttpContext httpContext)
    {
        var user = httpContext.User;

        return new AuditContext
        {
            ActorId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(ObjectIdentifierClaim)?.Value,
            ActorDisplayName = user.Identity?.Name,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = httpContext.TraceIdentifier,
        };
    }
}
