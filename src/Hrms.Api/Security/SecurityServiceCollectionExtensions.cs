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
