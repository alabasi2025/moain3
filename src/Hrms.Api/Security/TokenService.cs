using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hrms.Api.Security;

public sealed class TokenService(IOptions<JwtOptions> o)
{
    private readonly JwtOptions _options = o.Value;

    public string CreateAccessToken(
        long userId,
        long companyId,
        string username,
        IEnumerable<string> permissions,
        bool isAdmin)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(permissions);

        if (string.IsNullOrWhiteSpace(_options.Key) ||
            Encoding.UTF8.GetByteCount(_options.Key) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must contain at least 32 UTF-8 bytes.");
        }

        if (string.IsNullOrWhiteSpace(_options.Issuer) ||
            string.IsNullOrWhiteSpace(_options.Audience) ||
            _options.AccessMinutes <= 0)
        {
            throw new InvalidOperationException("The JWT configuration is invalid.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
            new("company_id", companyId.ToString(CultureInfo.InvariantCulture)),
            new("name", username),
            new("is_admin", isAdmin ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        foreach (string permission in permissions)
        {
            ArgumentNullException.ThrowIfNull(permission);
            claims.Add(new Claim("perm", permission));
        }

        DateTime now = DateTime.UtcNow;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(
            signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string token, string hash) CreateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);

        try
        {
            string token = Base64UrlEncoder.Encode(randomBytes);
            return (token, HashToken(token));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(randomBytes);
        }
    }

    public static string HashToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        byte[] bytes = Encoding.UTF8.GetBytes(token);

        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
