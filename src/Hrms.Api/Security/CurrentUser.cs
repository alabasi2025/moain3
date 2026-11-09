using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Hrms.Api.Security;

public interface ICurrentUser
{
    long UserId { get; }
    long CompanyId { get; }
    bool IsAdmin { get; }
    bool Has(string perm);
    string? Ip { get; }
}

public sealed class CurrentUser(IHttpContextAccessor a) : ICurrentUser
{
    private ClaimsPrincipal? Principal => a.HttpContext?.User;

    public long UserId => ReadLongClaim("sub");

    public long CompanyId => ReadLongClaim("company_id");

    public bool IsAdmin => Principal?.Claims.Any(claim =>
        string.Equals(claim.Type, "is_admin", StringComparison.Ordinal) &&
        string.Equals(claim.Value, "true", StringComparison.Ordinal)) == true;

    public string? Ip => a.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool Has(string perm)
    {
        ArgumentNullException.ThrowIfNull(perm);

        return IsAdmin || Principal?.Claims.Any(claim =>
            string.Equals(claim.Type, "perm", StringComparison.Ordinal) &&
            string.Equals(claim.Value, perm, StringComparison.Ordinal)) == true;
    }

    private long ReadLongClaim(string claimType)
    {
        string? value = Principal?.Claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, claimType, StringComparison.Ordinal))?.Value;

        return long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long result)
            ? result
            : 0;
    }
}
