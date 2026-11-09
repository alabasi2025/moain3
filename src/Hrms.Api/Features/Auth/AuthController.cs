using System.Data;
using System.Data.Common;
using Dapper;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hrms.Api.Features.Auth;

public sealed record LoginRequest(string CompanyCode, string Username, string Password, int? FiscalYear);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UserInfo(
    long Id,
    string Username,
    string DisplayName,
    bool IsAdmin,
    long CompanyId,
    IReadOnlyList<string> Permissions);

public sealed record LoginData(string AccessToken, string RefreshToken, int ExpiresIn, UserInfo User);

internal sealed class AuthUserRow
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

internal sealed class AuthTokenRow
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public long? ReplacedByTokenId { get; set; }
}

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private const string InvalidCredentials = "بيانات الدخول غير صحيحة";
    private const string InvalidRefresh = "رمز التحديث غير صالح أو منتهي الصلاحية";

    private const string UserSelect = @"
SELECT u.Id, u.CompanyId, u.Username, u.PasswordHash, u.DisplayName, u.IsAdmin, u.FailedLogins, u.LockedUntil
FROM hr.Users u
JOIN hr.Companies c ON c.Id = u.CompanyId AND c.IsDeleted = 0 AND c.IsActive = 1
WHERE u.Id = @UserId AND u.CompanyId = @CompanyId AND u.IsActive = 1 AND u.IsDeleted = 0;";

    private readonly IDbConnectionFactory _db;
    private readonly TokenService _tokens;
    private readonly JwtOptions _jwt;
    private readonly ICurrentUser _currentUser;

    public AuthController(IDbConnectionFactory db, TokenService tokens, IOptions<JwtOptions> jwt, ICurrentUser currentUser)
    {
        _db = db;
        _tokens = tokens;
        _jwt = jwt.Value;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginData>>> Login([FromBody] LoginRequest req)
    {
        if (req is null
            || string.IsNullOrWhiteSpace(req.CompanyCode)
            || string.IsNullOrWhiteSpace(req.Username)
            || string.IsNullOrEmpty(req.Password))
        {
            throw new ArgumentException("رمز الشركة واسم المستخدم وكلمة المرور مطلوبة");
        }

        await using var conn = await OpenAsync();

        var companyId = await conn.ExecuteScalarAsync<long?>(
            "SELECT TOP 1 Id FROM hr.Companies WHERE Code = @Code AND IsDeleted = 0 AND IsActive = 1;",
            new { Code = req.CompanyCode.Trim() });

        if (companyId is null)
        {
            return Unauthorized(new ProblemDetails { Title = InvalidCredentials });
        }

        var user = await conn.QueryFirstOrDefaultAsync<AuthUserRow>(@"
SELECT Id, CompanyId, Username, PasswordHash, DisplayName, IsAdmin, FailedLogins, LockedUntil
FROM hr.Users
WHERE CompanyId = @CompanyId AND Username = @Username AND IsActive = 1 AND IsDeleted = 0;",
            new { CompanyId = companyId.Value, Username = req.Username.Trim() });

        if (user is null)
        {
            return Unauthorized(new ProblemDetails { Title = InvalidCredentials });
        }

        var now = DateTimeOffset.UtcNow;

        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "الحساب مقفل مؤقتاً",
                detail: $"تم قفل الحساب بسبب محاولات دخول فاشلة متكررة حتى {lockedUntil:yyyy-MM-dd HH:mm} (UTC)");
        }

        if (!PasswordHasher.Verify(req.Password, user.PasswordHash))
        {
            await conn.ExecuteAsync(@"
UPDATE hr.Users
SET FailedLogins = FailedLogins + 1,
    LockedUntil = CASE WHEN FailedLogins + 1 >= @MaxFailed THEN @LockUntil ELSE LockedUntil END
WHERE Id = @Id AND CompanyId = @CompanyId;",
                new { MaxFailed = MaxFailedLogins, LockUntil = now.Add(LockDuration), user.Id, user.CompanyId });

            return Unauthorized(new ProblemDetails { Title = InvalidCredentials });
        }

        if (req.FiscalYear is int fiscalYear)
        {
            var fyExists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM hr.FiscalYears WHERE CompanyId = @CompanyId AND FiscalYear = @FiscalYear AND IsDeleted = 0;",
                new { user.CompanyId, FiscalYear = fiscalYear });
            if (fyExists == 0)
            {
                throw new ArgumentException("السنة المالية المحددة غير معرّفة لهذه الشركة");
            }
        }

        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync(@"
UPDATE hr.Users
SET FailedLogins = 0, LockedUntil = NULL, LastLoginAt = @Now
WHERE Id = @Id AND CompanyId = @CompanyId;",
            new { Now = now, user.Id, user.CompanyId }, transaction: tx);

        var permissions = await LoadPermissionsAsync(conn, tx, user.Id, user.CompanyId);
        var (data, _) = await IssueTokensAsync(conn, tx, user, permissions);

        await tx.CommitAsync();

        return new ApiResponse<LoginData>(data);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginData>>> Refresh([FromBody] RefreshRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            return Unauthorized(new ProblemDetails { Title = InvalidRefresh });
        }

        var hash = TokenService.HashToken(req.RefreshToken);
        var now = DateTimeOffset.UtcNow;

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var token = await conn.QueryFirstOrDefaultAsync<AuthTokenRow>(@"
SELECT Id, CompanyId, UserId, ExpiresAt, RevokedAt, ReplacedByTokenId
FROM hr.RefreshTokens WITH (UPDLOCK, ROWLOCK)
WHERE TokenHash = @Hash;",
            new { Hash = hash }, transaction: tx);

        if (token is null)
        {
            await tx.RollbackAsync();
            return Unauthorized(new ProblemDetails { Title = InvalidRefresh });
        }

        if (token.RevokedAt is not null)
        {
            if (token.ReplacedByTokenId is not null)
            {
                await conn.ExecuteAsync(@"
UPDATE hr.RefreshTokens
SET RevokedAt = @Now
WHERE UserId = @UserId AND CompanyId = @CompanyId AND RevokedAt IS NULL;",
                    new { Now = now, token.UserId, token.CompanyId }, transaction: tx);
                await tx.CommitAsync();
            }
            else
            {
                await tx.RollbackAsync();
            }

            return Unauthorized(new ProblemDetails { Title = InvalidRefresh });
        }

        if (token.ExpiresAt <= now)
        {
            await tx.RollbackAsync();
            return Unauthorized(new ProblemDetails { Title = InvalidRefresh });
        }

        var user = await conn.QueryFirstOrDefaultAsync<AuthUserRow>(
            UserSelect, new { token.UserId, token.CompanyId }, transaction: tx);

        if (user is null || (user.LockedUntil is { } lu && lu > now))
        {
            await tx.RollbackAsync();
            return Unauthorized(new ProblemDetails { Title = InvalidRefresh });
        }

        var permissions = await LoadPermissionsAsync(conn, tx, user.Id, user.CompanyId);
        var (data, newTokenId) = await IssueTokensAsync(conn, tx, user, permissions);

        await conn.ExecuteAsync(@"
UPDATE hr.RefreshTokens
SET RevokedAt = @Now, ReplacedByTokenId = @NewId
WHERE Id = @Id AND CompanyId = @CompanyId;",
            new { Now = now, NewId = newTokenId, UserId = user.Id, token.Id, token.CompanyId }, transaction: tx);

        await tx.CommitAsync();

        return new ApiResponse<LoginData>(data);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ApiResponse<bool>> Logout([FromBody] RefreshRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            throw new ArgumentException("رمز التحديث مطلوب");
        }

        var hash = TokenService.HashToken(req.RefreshToken);

        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(@"
UPDATE hr.RefreshTokens
SET RevokedAt = @Now
WHERE TokenHash = @Hash AND UserId = @UserId AND CompanyId = @CompanyId AND RevokedAt IS NULL;",
            new { Now = DateTimeOffset.UtcNow, Hash = hash, _currentUser.UserId, _currentUser.CompanyId });

        return new ApiResponse<bool>(true);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ApiResponse<UserInfo>> Me()
    {
        await using var conn = await OpenAsync();

        var user = await conn.QueryFirstOrDefaultAsync<AuthUserRow>(
            UserSelect, new { _currentUser.UserId, _currentUser.CompanyId })
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        var permissions = await LoadPermissionsAsync(conn, null, user.Id, user.CompanyId);
        return new ApiResponse<UserInfo>(ToInfo(user, permissions));
    }

    private async Task<DbConnection> OpenAsync()
    {
        var conn = _db.Create() as DbConnection
                   ?? throw new InvalidOperationException("مصنع الاتصال لا يرجع DbConnection");
        await conn.OpenAsync();
        return conn;
    }

    private static async Task<IReadOnlyList<string>> LoadPermissionsAsync(IDbConnection conn, IDbTransaction? tx, long userId, long companyId)
    {
        var perms = await conn.QueryAsync<string>(@"
SELECT DISTINCT p.PermCode
FROM hr.UserRoles ur
JOIN hr.Roles r ON r.Id = ur.RoleId AND r.CompanyId = ur.CompanyId AND r.IsDeleted = 0 AND r.IsActive = 1
JOIN hr.RolePermissions rp ON rp.RoleId = ur.RoleId AND rp.CompanyId = ur.CompanyId AND rp.IsDeleted = 0
JOIN hr.Permissions p ON p.Id = rp.PermissionId AND p.IsDeleted = 0
WHERE ur.UserId = @UserId AND ur.CompanyId = @CompanyId AND ur.IsDeleted = 0;",
            new { UserId = userId, CompanyId = companyId }, transaction: tx);

        return perms.OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    private async Task<(LoginData Data, long TokenId)> IssueTokensAsync(
        IDbConnection conn, IDbTransaction tx, AuthUserRow user, IReadOnlyList<string> permissions)
    {
        var accessToken = _tokens.CreateAccessToken(user.Id, user.CompanyId, user.Username, permissions, user.IsAdmin);
        var (refreshToken, refreshHash) = _tokens.CreateRefreshToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshDays);

        var tokenId = await conn.ExecuteScalarAsync<long>(@"
INSERT hr.RefreshTokens(CompanyId, UserId, TokenHash, ExpiresAt, IpAddress)
OUTPUT INSERTED.Id
VALUES(@CompanyId, @UserId, @TokenHash, @ExpiresAt, @IpAddress);",
            new
            {
                user.CompanyId,
                UserId = user.Id,
                TokenHash = refreshHash,
                ExpiresAt = expiresAt,
                IpAddress = Truncate(HttpContext.Connection.RemoteIpAddress?.ToString(), 60)
            }, transaction: tx);

        var expiresIn = (int)TimeSpan.FromMinutes(_jwt.AccessMinutes).TotalSeconds;
        var data = new LoginData(accessToken, refreshToken, expiresIn, ToInfo(user, permissions));
        return (data, tokenId);
    }

    private static UserInfo ToInfo(AuthUserRow user, IReadOnlyList<string> permissions) =>
        new(user.Id, user.Username, user.DisplayName, user.IsAdmin, user.CompanyId, permissions);

    private static string? Truncate(string? value, int max) =>
        value is null ? null : (value.Length <= max ? value : value[..max]);
}
