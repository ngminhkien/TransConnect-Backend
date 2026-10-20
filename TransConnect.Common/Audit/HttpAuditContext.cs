using Microsoft.AspNetCore.Http;
using TransConnect.Common.Auth;

namespace TransConnect.Common.Audit;

public sealed class HttpAuditContext(IHttpContextAccessor http, ICurrentUser current) : IAuditContext
{
    public Guid? ActorAccountId => current.UserId;
    public string? RequestId => http.HttpContext?.TraceIdentifier;
}
