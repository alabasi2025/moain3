=== FILE: src/Hrms.Api/Security/PasswordHasher.cs ===
using System.Globalization;
using System.Security.Cryptography;

namespace Hrms.Api.Security;

public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        try
        {
            return string.Concat(
                "PBKDF2$",
                Iterations.ToString(CultureInfo.InvariantCulture),
                "$",
                Convert.ToBase64String(salt),
                "$",
                Convert.ToBase64String(derivedKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public static bool Verify(string password, string hash)
    {
        if (password is null || hash is null || hash.Length > 128)
        {
            return false;
        }

        string[] parts = hash.Split('$');

        if (parts.Length != 4 ||
            !string.Equals(parts[0], "PBKDF2", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "100000", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != SaltSize || expectedHash.Length != HashSize)
        {
            CryptographicOperations.ZeroMemory(expectedHash);
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        try
        {
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
            CryptographicOperations.ZeroMemory(expectedHash);
        }
    }
}
=== END FILE ===
=== FILE: src/Hrms.Api/Security/JwtOptions.cs ===
namespace Hrms.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "hrms";
    public string Audience { get; set; } = "hrms";
    public string Key { get; set; } = "";
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 14;
}
=== END FILE ===
=== FILE: src/Hrms.Api/Security/TokenService.cs ===
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
=== END FILE ===
=== FILE: src/Hrms.Api/Security/PermissionAuthorization.cs ===
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Hrms.Api.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } =
        permission ?? throw new ArgumentNullException(nameof(permission));
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        bool isAdmin = context.User.Claims.Any(claim =>
            string.Equals(claim.Type, "is_admin", StringComparison.Ordinal) &&
            string.Equals(claim.Value, "true", StringComparison.Ordinal));

        bool hasPermission = context.User.Claims.Any(claim =>
            string.Equals(claim.Type, "perm", StringComparison.Ordinal) &&
            string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal));

        if (isAdmin || hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string Prefix = "perm:";
    private readonly DefaultAuthorizationPolicyProvider _defaultProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _defaultProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return _defaultProvider.GetPolicyAsync(policyName);
        }

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _defaultProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _defaultProvider.GetFallbackPolicyAsync();
    }
}

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute("perm:" + permission)
{
}
=== END FILE ===
=== FILE: src/Hrms.Api/Security/CurrentUser.cs ===
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
=== END FILE ===
=== FILE: src/Hrms.Api/Security/FieldEncryptor.cs ===
using System.Security.Cryptography;
using System.Text;

namespace Hrms.Api.Security;

public sealed class FieldEncryptor(string base64Key32)
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key = ParseKey(base64Key32);

    public string? Encrypt(string? plain)
    {
        if (plain is null)
        {
            return null;
        }

        byte[] plaintext = Encoding.UTF8.GetBytes(plain);

        try
        {
            byte[] payload = new byte[checked(NonceSize + TagSize + plaintext.Length)];
            Span<byte> nonce = payload.AsSpan(0, NonceSize);
            Span<byte> tag = payload.AsSpan(NonceSize, TagSize);
            Span<byte> ciphertext = payload.AsSpan(NonceSize + TagSize);

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string? Decrypt(string? cipher)
    {
        if (cipher is null)
        {
            return null;
        }

        byte[] payload = Convert.FromBase64String(cipher);

        if (payload.Length < NonceSize + TagSize)
        {
            throw new FormatException("The encrypted field payload is invalid.");
        }

        ReadOnlySpan<byte> nonce = payload.AsSpan(0, NonceSize);
        ReadOnlySpan<byte> tag = payload.AsSpan(NonceSize, TagSize);
        ReadOnlySpan<byte> ciphertext = payload.AsSpan(NonceSize + TagSize);
        byte[] plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] ParseKey(string base64Key32)
    {
        ArgumentNullException.ThrowIfNull(base64Key32);

        byte[] key;

        try
        {
            key = Convert.FromBase64String(base64Key32);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "The encryption key must be a Base64-encoded 32-byte key.",
                nameof(base64Key32),
                exception);
        }

        if (key.Length != KeySize)
        {
            CryptographicOperations.ZeroMemory(key);

            throw new ArgumentException(
                "The encryption key must contain exactly 32 bytes.",
                nameof(base64Key32));
        }

        return key;
    }
}
=== END FILE ===
=== FILE: src/Hrms.Api/Common/ApiResponse.cs ===
namespace Hrms.Api.Common;

public sealed record ApiResponse<T>(T Data, PageMeta? Meta = null);

public sealed record PageMeta(int Page, int PageSize, int Total);
=== END FILE ===
=== FILE: src/Hrms.Api/Common/ProblemExceptionMiddleware.cs ===
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hrms.Api.Common;

public sealed class ProblemExceptionMiddleware(
    RequestDelegate next,
    ILogger<ProblemExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            int statusCode = GetStatusCode(exception);

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    exception,
                    "Unhandled request exception. Trace identifier: {TraceIdentifier}",
                    context.TraceIdentifier);
            }

            var problem = new ProblemDetails
            {
                Type = "about:blank",
                Title = GetTitle(statusCode),
                Status = statusCode,
                Detail = statusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : exception.Message,
                Instance = context.Request.PathBase.Add(context.Request.Path).Value
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                SerializerOptions,
                context.RequestAborted);
        }
    }

    private static int GetStatusCode(Exception exception)
    {
        if (IsConcurrencyException(exception))
        {
            return StatusCodes.Status409Conflict;
        }

        return exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static bool IsConcurrencyException(Exception exception)
    {
        for (Type? type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (string.Equals(
                type.Name,
                "DbUpdateConcurrencyException",
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
            _ => "Internal Server Error"
        };
    }
}

public static class ProblemExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseProblemExceptions(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ProblemExceptionMiddleware>();
    }
}
=== END FILE ===
=== FILE: src/Hrms.Api/Security/SecurityServiceCollectionExtensions.cs ===
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hrms.Api.Security;

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddHrmsSecurity(
        this IServiceCollection s,
        IConfiguration cfg)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(cfg);

        string encryptionKey = cfg["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key is required.");

        var fieldEncryptor = new FieldEncryptor(encryptionKey);

        s.AddOptions<JwtOptions>()
            .Bind(cfg.GetSection("Jwt"))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer),
                "Jwt:Issuer is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Audience is required.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.Key) &&
                    Encoding.UTF8.GetByteCount(options.Key) >= 32,
                "Jwt:Key must contain at least 32 UTF-8 bytes.")
            .Validate(
                options => options.AccessMinutes > 0,
                "Jwt:AccessMinutes must be greater than zero.")
            .Validate(
                options => options.RefreshDays > 0,
                "Jwt:RefreshDays must be greater than zero.")
            .ValidateOnStart();

        s.AddSingleton<TokenService>();
        s.AddSingleton(fieldEncryptor);

        s.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();

        s.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, configuredOptions) =>
            {
                JwtOptions jwt = configuredOptions.Value;

                bearer.MapInboundClaims = false;
                bearer.IncludeErrorDetails = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name"
                };
            });

        s.AddAuthorization();

        s.Replace(ServiceDescriptor.Singleton<
            IAuthorizationPolicyProvider,
            PermissionPolicyProvider>());

        s.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAuthorizationHandler,
            PermissionHandler>());

        s.AddHttpContextAccessor();
        s.AddScoped<ICurrentUser, CurrentUser>();

        return s;
    }
}
=== END FILE ===