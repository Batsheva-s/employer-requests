using EmployerRequests.Application.Abstractions;
using EmployerRequests.Domain;

namespace EmployerRequests.Api.Identity;

/// <summary>
/// The user name recorded in the status history.
/// The exam has no login, so the client sends the name in the <c>X-User-Name</c> header – URL-encoded, because
/// HTTP headers carry ASCII only and names may be in Hebrew. With real authentication, this class would read the
/// name from the signed-in user's token instead; nothing else in the system would change.
/// </summary>
public class HeaderCurrentUser : ICurrentUser
{
    public const string HeaderName = "X-User-Name";
    private const string AnonymousUser = "anonymous";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserName
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
            var name = string.IsNullOrWhiteSpace(header) ? string.Empty : Uri.UnescapeDataString(header).Trim();

            if (name.Length == 0)
            {
                return AnonymousUser;
            }

            // Never longer than the history column.
            return name.Length <= RequestStatusChange.ChangedByMaxLength
                ? name
                : name[..RequestStatusChange.ChangedByMaxLength];
        }
    }
}
