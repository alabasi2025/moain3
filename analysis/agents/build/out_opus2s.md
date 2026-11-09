=== FILE: src/Hrms.Api/Features/Auth/AuthController.cs ===
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
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
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
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
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
WHERE TokenHash = @Hash AND IsDeleted = 0;",
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
SET RevokedAt = @Now, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
WHERE UserId = @UserId AND CompanyId = @CompanyId AND RevokedAt IS NULL AND IsDeleted = 0;",
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
SET RevokedAt = @Now, ReplacedByTokenId = @NewId, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
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
SET RevokedAt = @Now, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
WHERE TokenHash = @Hash AND UserId = @UserId AND CompanyId = @CompanyId AND RevokedAt IS NULL AND IsDeleted = 0;",
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
INSERT hr.RefreshTokens(CompanyId, UserId, TokenHash, ExpiresAt, IpAddress, CreatedBy)
OUTPUT INSERTED.Id
VALUES(@CompanyId, @UserId, @TokenHash, @ExpiresAt, @IpAddress, @UserId);",
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
=== END FILE ===
=== FILE: src/Hrms.Api/Features/Definitions/DefinitionsController.cs ===
using System.Data;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Hrms.Api.Features.Definitions;

public sealed class DefinitionDto
{
    public long Id { get; set; }
    public int Code { get; set; }
    public string NameAr { get; set; } = "";
    public string? NameEn { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public sealed class DefinitionInput
{
    public int? Code { get; init; }
    public string NameAr { get; init; } = "";
    public string? NameEn { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class LookupDto
{
    public long Id { get; set; }
    public int Code { get; set; }
    public string NameAr { get; set; } = "";
    public string? NameEn { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public string LookupType { get; set; } = "";
}

internal static class AuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Task WriteAsync(
        IDbConnection conn,
        IDbTransaction? tx,
        ICurrentUser user,
        string entityName,
        long entityId,
        string actionType,
        object? newValues)
    {
        return conn.ExecuteAsync(@"
INSERT hr.AuditLogs(CompanyId, UserId, EntityName, EntityId, ActionType, NewValues, IpAddress)
VALUES(@CompanyId, @UserId, @EntityName, @EntityId, @ActionType, @NewValues, @IpAddress);",
            new
            {
                user.CompanyId,
                user.UserId,
                EntityName = entityName,
                EntityId = entityId,
                ActionType = actionType,
                NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
                IpAddress = user.Ip
            }, transaction: tx);
    }
}

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class DefinitionsController : ControllerBase
{
    private sealed record DefinitionKind(string Table, string Permission);

    private static readonly IReadOnlyDictionary<string, DefinitionKind> Kinds =
        new Dictionary<string, DefinitionKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["job-grades"] = new("JobGrades", "defs.job_grades"),
            ["jobs"] = new("Jobs", "defs.jobs"),
            ["cost-centers"] = new("CostCenters", "defs.cost_centers"),
            ["employee-groups"] = new("EmployeeGroups", "defs.employee_groups"),
            ["projects"] = new("Projects", "defs.projects"),
            ["banks"] = new("Banks", "defs.banks"),
            ["nationalities"] = new("Nationalities", "defs.nationalities"),
            ["work-natures"] = new("WorkNatures", "defs.work_natures"),
        };

    private readonly IDbConnectionFactory _db;
    private readonly ICurrentUser _user;

    public DefinitionsController(IDbConnectionFactory db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet("defs/{kind}")]
    public async Task<ApiResponse<IReadOnlyList<DefinitionDto>>> List(
        string kind,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var k = Resolve(kind, "read");
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var where = "WHERE CompanyId = @CompanyId AND IsDeleted = 0";
        if (term is not null)
        {
            where += " AND (NameAr LIKE @Like OR NameEn LIKE @Like OR CAST(Code AS nvarchar(20)) = @Term)";
        }

        var sql = $@"
SELECT COUNT(1) FROM hr.{k.Table} {where};
SELECT Id, Code, NameAr, NameEn, Notes, IsActive
FROM hr.{k.Table} {where}
ORDER BY Code
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

        await using var conn = await OpenAsync();
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            _user.CompanyId,
            Term = term,
            Like = term is null ? null : "%" + EscapeLike(term) + "%",
            Skip = (page - 1) * pageSize,
            Take = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<DefinitionDto>()).ToList();

        return new ApiResponse<IReadOnlyList<DefinitionDto>>(items, new PageMeta(page, pageSize, total));
    }

    [HttpGet("defs/{kind}/{id:long}")]
    public async Task<ApiResponse<DefinitionDto>> Get(string kind, long id)
    {
        var k = Resolve(kind, "read");
        await using var conn = await OpenAsync();
        var dto = await GetAsync(conn, null, k.Table, id) ?? throw new KeyNotFoundException("السجل غير موجود");
        return new ApiResponse<DefinitionDto>(dto);
    }

    [HttpPost("defs/{kind}")]
    public async Task<ApiResponse<DefinitionDto>> Create(string kind, [FromBody] DefinitionInput input)
    {
        var k = Resolve(kind, "create");
        Validate(input);

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        int code;
        if (input.Code is int requested)
        {
            await EnsureCodeUniqueAsync(conn, tx, k.Table, requested, 0);
            code = requested;
        }
        else
        {
            code = await conn.ExecuteScalarAsync<int>(
                $"SELECT ISNULL(MAX(Code), 0) + 1 FROM hr.{k.Table} WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId;",
                new { _user.CompanyId }, transaction: tx);
        }

        var values = new
        {
            Code = code,
            NameAr = input.NameAr.Trim(),
            NameEn = Normalize(input.NameEn),
            Notes = Normalize(input.Notes),
            input.IsActive
        };

        long id;
        try
        {
            id = await conn.ExecuteScalarAsync<long>($@"
INSERT hr.{k.Table}(CompanyId, Code, NameAr, NameEn, Notes, IsActive, CreatedBy)
OUTPUT INSERTED.Id
VALUES(@CompanyId, @Code, @NameAr, @NameEn, @Notes, @IsActive, @UserId);",
                new
                {
                    _user.CompanyId,
                    values.Code,
                    values.NameAr,
                    values.NameEn,
                    values.Notes,
                    values.IsActive,
                    _user.UserId
                }, transaction: tx);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException("الكود مستخدم مسبقاً، الرجاء اختيار كود آخر");
        }

        await AuditWriter.WriteAsync(conn, tx, _user, k.Table, id, "create", values);

        var dto = await GetAsync(conn, tx, k.Table, id) ?? throw new KeyNotFoundException("السجل غير موجود");
        await tx.CommitAsync();

        return new ApiResponse<DefinitionDto>(dto);
    }

    [HttpPut("defs/{kind}/{id:long}")]
    public async Task<ApiResponse<DefinitionDto>> Update(string kind, long id, [FromBody] DefinitionInput input)
    {
        var k = Resolve(kind, "update");
        Validate(input);

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var existingCode = await conn.ExecuteScalarAsync<int?>(
            $"SELECT Code FROM hr.{k.Table} WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, _user.CompanyId }, transaction: tx)
            ?? throw new KeyNotFoundException("السجل غير موجود");

        var code = input.Code ?? existingCode;
        if (code != existingCode)
        {
            await EnsureCodeUniqueAsync(conn, tx, k.Table, code, id);
        }

        var values = new
        {
            Id = id,
            Code = code,
            NameAr = input.NameAr.Trim(),
            NameEn = Normalize(input.NameEn),
            Notes = Normalize(input.Notes),
            input.IsActive
        };

        int affected;
        try
        {
            affected = await conn.ExecuteAsync($@"
UPDATE hr.{k.Table}
SET Code = @Code, NameAr = @NameAr, NameEn = @NameEn, Notes = @Notes, IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
                new
                {
                    values.Id,
                    values.Code,
                    values.NameAr,
                    values.NameEn,
                    values.Notes,
                    values.IsActive,
                    _user.UserId,
                    _user.CompanyId
                }, transaction: tx);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException("الكود مستخدم مسبقاً، الرجاء اختيار كود آخر");
        }

        if (affected == 0)
        {
            throw new KeyNotFoundException("السجل غير موجود");
        }

        await AuditWriter.WriteAsync(conn, tx, _user, k.Table, id, "update", values);

        var dto = await GetAsync(conn, tx, k.Table, id) ?? throw new KeyNotFoundException("السجل غير موجود");
        await tx.CommitAsync();

        return new ApiResponse<DefinitionDto>(dto);
    }

    [HttpDelete("defs/{kind}/{id:long}")]
    public async Task<ApiResponse<bool>> Delete(string kind, long id)
    {
        var k = Resolve(kind, "delete");

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var affected = await conn.ExecuteAsync($@"
UPDATE hr.{k.Table}
SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, _user.UserId, _user.CompanyId }, transaction: tx);

        if (affected == 0)
        {
            throw new KeyNotFoundException("السجل غير موجود");
        }

        await AuditWriter.WriteAsync(conn, tx, _user, k.Table, id, "delete", new { Id = id, IsDeleted = true });
        await tx.CommitAsync();

        return new ApiResponse<bool>(true);
    }

    [HttpGet("lookups")]
    public async Task<ApiResponse<IReadOnlyList<LookupDto>>> Lookups([FromQuery] string? lookupType)
    {
        var type = string.IsNullOrWhiteSpace(lookupType) ? null : lookupType.Trim();

        await using var conn = await OpenAsync();
        var items = (await conn.QueryAsync<LookupDto>(@"
SELECT Id, Code, NameAr, NameEn, Notes, IsActive, LookupType
FROM hr.Lookups
WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND IsActive = 1
  AND (@LookupType IS NULL OR LookupType = @LookupType)
ORDER BY LookupType, Code;",
            new { _user.CompanyId, LookupType = type })).ToList();

        return new ApiResponse<IReadOnlyList<LookupDto>>(items);
    }

    private DefinitionKind Resolve(string kind, string action)
    {
        if (string.IsNullOrWhiteSpace(kind) || !Kinds.TryGetValue(kind, out var k))
        {
            throw new KeyNotFoundException($"نوع التعريف '{kind}' غير معروف");
        }

        if (!_user.IsAdmin && !_user.Has(k.Permission + "." + action))
        {
            throw new UnauthorizedAccessException();
        }

        return k;
    }

    private async Task<DbConnection> OpenAsync()
    {
        var conn = _db.Create() as DbConnection
                   ?? throw new InvalidOperationException("مصنع الاتصال لا يرجع DbConnection");
        await conn.OpenAsync();
        return conn;
    }

    private Task<DefinitionDto?> GetAsync(IDbConnection conn, IDbTransaction? tx, string table, long id) =>
        conn.QueryFirstOrDefaultAsync<DefinitionDto?>(
            $"SELECT Id, Code, NameAr, NameEn, Notes, IsActive FROM hr.{table} WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, _user.CompanyId }, transaction: tx);

    private async Task EnsureCodeUniqueAsync(IDbConnection conn, IDbTransaction tx, string table, int code, long excludeId)
    {
        var count = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM hr.{table} WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId AND Code = @Code AND IsDeleted = 0 AND Id <> @ExcludeId;",
            new { _user.CompanyId, Code = code, ExcludeId = excludeId }, transaction: tx);

        if (count > 0)
        {
            throw new InvalidOperationException($"الكود {code} مستخدم مسبقاً، الرجاء اختيار كود آخر");
        }
    }

    private static void Validate(DefinitionInput? input)
    {
        if (input is null) throw new ArgumentException("البيانات مطلوبة");
        if (string.IsNullOrWhiteSpace(input.NameAr)) throw new ArgumentException("الاسم العربي مطلوب");
        if (input.NameAr.Trim().Length > 200) throw new ArgumentException("الاسم العربي يجب ألا يتجاوز 200 حرف");
        if (input.NameEn is not null && input.NameEn.Trim().Length > 200) throw new ArgumentException("الاسم الإنجليزي يجب ألا يتجاوز 200 حرف");
        if (input.Notes is not null && input.Notes.Trim().Length > 1000) throw new ArgumentException("الملاحظات يجب ألا تتجاوز 1000 حرف");
        if (input.Code is <= 0) throw new ArgumentException("الكود يجب أن يكون رقماً موجباً");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
=== END FILE ===
=== FILE: src/Hrms.Api/Features/Org/OrgNodesController.cs ===
using System.Data;
using System.Data.Common;
using Dapper;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Features.Definitions;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Hrms.Api.Features.Org;

public sealed class OrgNodeDto
{
    public long Id { get; set; }
    public int Code { get; set; }
    public string NameAr { get; set; } = "";
    public string? NameEn { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public long? ParentId { get; set; }
    public string NodeType { get; set; } = "";
    public long? ManagerEmployeeId { get; set; }
    public string? NodePath { get; set; }
    public int NodeLevel { get; set; }
    public int SortOrder { get; set; }
    public List<OrgNodeDto> Children { get; set; } = new();
}

public sealed class OrgNodeInput
{
    public int? Code { get; init; }
    public string NameAr { get; init; } = "";
    public string? NameEn { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
    public long? ParentId { get; init; }
    public string NodeType { get; init; } = "";
    public long? ManagerEmployeeId { get; init; }
    public int SortOrder { get; init; }
}

internal sealed class OrgPathRow
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string? NodePath { get; set; }
    public int NodeLevel { get; set; }
}

[ApiController]
[Authorize]
[Route("api/v1/org-nodes")]
public sealed class OrgNodesController : ControllerBase
{
    private const string EntityName = "OrgNodes";

    private const string NodeSelect = @"
SELECT Id, Code, NameAr, NameEn, Notes, IsActive, ParentId, NodeType, ManagerEmployeeId, NodePath, NodeLevel, SortOrder
FROM hr.OrgNodes";

    private readonly IDbConnectionFactory _db;
    private readonly ICurrentUser _user;

    public OrgNodesController(IDbConnectionFactory db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet("tree")]
    [HasPermission("defs.org_nodes.read")]
    public async Task<ApiResponse<IReadOnlyList<OrgNodeDto>>> Tree()
    {
        await using var conn = await OpenAsync();
        var nodes = (await conn.QueryAsync<OrgNodeDto>(
            NodeSelect + " WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY SortOrder, Code;",
            new { _user.CompanyId })).ToList();

        var byId = nodes.ToDictionary(n => n.Id);

        bool IsInCycle(OrgNodeDto node)
        {
            var seen = new HashSet<long> { node.Id };
            var current = node.ParentId;
            while (current is long c && byId.TryGetValue(c, out var parent))
            {
                if (!seen.Add(c))
                {
                    return c == node.Id;
                }
                current = parent.ParentId;
            }
            return false;
        }

        var roots = new List<OrgNodeDto>();
        foreach (var node in nodes)
        {
            if (node.ParentId is long pid
                && pid != node.Id
                && byId.TryGetValue(pid, out var parent)
                && !IsInCycle(node))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return new ApiResponse<IReadOnlyList<OrgNodeDto>>(roots);
    }

    [HttpPost]
    [HasPermission("defs.org_nodes.create")]
    public async Task<ApiResponse<OrgNodeDto>> Create([FromBody] OrgNodeInput input)
    {
        Validate(input);

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await EnsureNodeTypeAsync(conn, tx, input.NodeType);

        if (input.ParentId is long parentId)
        {
            await EnsureParentExistsAsync(conn, tx, parentId);
        }

        int code;
        if (input.Code is int requested)
        {
            await EnsureCodeUniqueAsync(conn, tx, requested, 0);
            code = requested;
        }
        else
        {
            code = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(MAX(Code), 0) + 1 FROM hr.OrgNodes WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId;",
                new { _user.CompanyId }, transaction: tx);
        }

        var values = new
        {
            Code = code,
            NameAr = input.NameAr.Trim(),
            NameEn = Normalize(input.NameEn),
            Notes = Normalize(input.Notes),
            input.IsActive,
            input.ParentId,
            NodeType = input.NodeType.Trim(),
            input.ManagerEmployeeId,
            input.SortOrder
        };

        long id;
        try
        {
            id = await conn.ExecuteScalarAsync<long>(@"
INSERT hr.OrgNodes(CompanyId, Code, NameAr, NameEn, Notes, IsActive, ParentId, NodeType, ManagerEmployeeId, NodeLevel, SortOrder, CreatedBy)
OUTPUT INSERTED.Id
VALUES(@CompanyId, @Code, @NameAr, @NameEn, @Notes, @IsActive, @ParentId, @NodeType, @ManagerEmployeeId, 0, @SortOrder, @UserId);",
                new
                {
                    _user.CompanyId,
                    values.Code,
                    values.NameAr,
                    values.NameEn,
                    values.Notes,
                    values.IsActive,
                    values.ParentId,
                    values.NodeType,
                    values.ManagerEmployeeId,
                    values.SortOrder,
                    _user.UserId
                }, transaction: tx);
        }
        catch (SqlException ex)
        {
            throw MapSqlException(ex);
        }

        await RebuildPathsAsync(conn, tx);
        await AuditWriter.WriteAsync(conn, tx, _user, EntityName, id, "create", values);

        var dto = await GetAsync(conn, tx, id) ?? throw new KeyNotFoundException("العقدة غير موجودة");
        await tx.CommitAsync();

        return new ApiResponse<OrgNodeDto>(dto);
    }

    [HttpPut("{id:long}")]
    [HasPermission("defs.org_nodes.update")]
    public async Task<ApiResponse<OrgNodeDto>> Update(long id, [FromBody] OrgNodeInput input)
    {
        Validate(input);

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var existingCode = await conn.ExecuteScalarAsync<int?>(
            "SELECT Code FROM hr.OrgNodes WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, _user.CompanyId }, transaction: tx)
            ?? throw new KeyNotFoundException("العقدة غير موجودة");

        await EnsureNodeTypeAsync(conn, tx, input.NodeType);

        if (input.ParentId is long parentId)
        {
            if (parentId == id)
            {
                throw new ArgumentException("لا يمكن جعل العقدة أباً لنفسها");
            }

            await EnsureParentExistsAsync(conn, tx, parentId);

            var links = (await conn.QueryAsync<OrgPathRow>(
                "SELECT Id, ParentId FROM hr.OrgNodes WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId AND IsDeleted = 0;",
                new { _user.CompanyId }, transaction: tx)).ToDictionary(r => r.Id);

            var visited = new HashSet<long>();
            long? current = parentId;
            while (current is long c)
            {
                if (c == id)
                {
                    throw new ArgumentException("لا يمكن جعل العقدة تابعة لإحدى العقد المتفرعة منها (يؤدي ذلك إلى دورة)");
                }
                if (!visited.Add(c) || !links.TryGetValue(c, out var row))
                {
                    break;
                }
                current = row.ParentId;
            }
        }

        var code = input.Code ?? existingCode;
        if (code != existingCode)
        {
            await EnsureCodeUniqueAsync(conn, tx, code, id);
        }

        var values = new
        {
            Id = id,
            Code = code,
            NameAr = input.NameAr.Trim(),
            NameEn = Normalize(input.NameEn),
            Notes = Normalize(input.Notes),
            input.IsActive,
            input.ParentId,
            NodeType = input.NodeType.Trim(),
            input.ManagerEmployeeId,
            input.SortOrder
        };

        int affected;
        try
        {
            affected = await conn.ExecuteAsync(@"
UPDATE hr.OrgNodes
SET Code = @Code, NameAr = @NameAr, NameEn = @NameEn, Notes = @Notes, IsActive = @IsActive,
    ParentId = @ParentId, NodeType = @NodeType, ManagerEmployeeId = @ManagerEmployeeId, SortOrder = @SortOrder,
    UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
                new
                {
                    values.Id,
                    values.Code,
                    values.NameAr,
                    values.NameEn,
                    values.Notes,
                    values.IsActive,
                    values.ParentId,
                    values.NodeType,
                    values.ManagerEmployeeId,
                    values.SortOrder,
                    _user.UserId,
                    _user.CompanyId
                }, transaction: tx);
        }
        catch (SqlException ex)
        {
            throw MapSqlException(ex);
        }

        if (affected == 0)
        {
            throw new KeyNotFoundException("العقدة غير موجودة");
        }

        await RebuildPathsAsync(conn, tx);
        await AuditWriter.WriteAsync(conn, tx, _user, EntityName, id, "update", values);

        var dto = await GetAsync(conn, tx, id) ?? throw new KeyNotFoundException("العقدة غير موجودة");
        await tx.CommitAsync();

        return new ApiResponse<OrgNodeDto>(dto);
    }

    [HttpDelete("{id:long}")]
    [HasPermission("defs.org_nodes.delete")]
    public async Task<ApiResponse<bool>> Delete(long id)
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM hr.OrgNodes WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, _user.CompanyId }, transaction: tx);
        if (exists == 0)
        {
            throw new KeyNotFoundException("العقدة غير موجودة");
        }

        var children = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM hr.OrgNodes WITH (HOLDLOCK) WHERE CompanyId = @CompanyId AND ParentId = @Id AND IsDeleted = 0;",
            new { Id = id, _user.CompanyId }, transaction: tx);

        var employees = await conn.ExecuteScalarAsync<int>(@"
DECLARE @emp int = 0;
IF COL_LENGTH(N'hr.Employees', N'OrgNodeId') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(1) FROM hr.Employees WHERE CompanyId = @cid AND OrgNodeId = @nid AND IsDeleted = 0;',
        N'@cid bigint, @nid bigint, @c int OUTPUT',
        @cid = @CompanyId, @nid = @Id, @c = @emp OUTPUT;
SELECT @emp;",
            new { Id = id, _user.CompanyId }, transaction: tx);

        if (children > 0 || employees > 0)
        {
            throw new InvalidOperationException("لا يمكن حذف عقدة لها أبناء أو موظفون");
        }

        await conn.ExecuteAsync(@"
UPDATE hr.OrgNodes
SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, _user.UserId, _user.CompanyId }, transaction: tx);

        await AuditWriter.WriteAsync(conn, tx, _user, EntityName, id, "delete", new { Id = id, IsDeleted = true });
        await tx.CommitAsync();

        return new ApiResponse<bool>(true);
    }

    private async Task<DbConnection> OpenAsync()
    {
        var conn = _db.Create() as DbConnection
                   ?? throw new InvalidOperationException("مصنع الاتصال لا يرجع DbConnection");
        await conn.OpenAsync();
        return conn;
    }

    private Task<OrgNodeDto?> GetAsync(IDbConnection conn, IDbTransaction? tx, long id) =>
        conn.QueryFirstOrDefaultAsync<OrgNodeDto?>(
            NodeSelect + " WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, _user.CompanyId }, transaction: tx);

    private async Task EnsureParentExistsAsync(IDbConnection conn, IDbTransaction tx, long parentId)
    {
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM hr.OrgNodes WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = parentId, _user.CompanyId }, transaction: tx);
        if (count == 0)
        {
            throw new ArgumentException("العقدة الأب غير موجودة أو لا تنتمي لهذه الشركة");
        }
    }

    private async Task EnsureCodeUniqueAsync(IDbConnection conn, IDbTransaction tx, int code, long excludeId)
    {
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM hr.OrgNodes WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId AND Code = @Code AND IsDeleted = 0 AND Id <> @ExcludeId;",
            new { _user.CompanyId, Code = code, ExcludeId = excludeId }, transaction: tx);
        if (count > 0)
        {
            throw new InvalidOperationException($"الكود {code} مستخدم مسبقاً، الرجاء اختيار كود آخر");
        }
    }

    private static async Task EnsureNodeTypeAsync(IDbConnection conn, IDbTransaction tx, string nodeType)
    {
        var value = nodeType.Trim();
        var definition = await conn.ExecuteScalarAsync<string?>(
            "SELECT TOP 1 definition FROM sys.check_constraints WHERE name = N'CK_OrgNodes_NodeType';",
            transaction: tx);

        if (definition is not null && !definition.Contains("'" + value + "'", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"نوع العقدة '{value}' غير مسموح به");
        }
    }

    private async Task RebuildPathsAsync(IDbConnection conn, IDbTransaction tx)
    {
        var rows = (await conn.QueryAsync<OrgPathRow>(
            "SELECT Id, ParentId, NodePath, NodeLevel FROM hr.OrgNodes WHERE CompanyId = @CompanyId AND IsDeleted = 0;",
            new { _user.CompanyId }, transaction: tx)).ToList();

        var byId = rows.ToDictionary(r => r.Id);
        var computed = new Dictionary<long, (string Path, int Level)>();

        (string Path, int Level) Compute(OrgPathRow row, HashSet<long> stack)
        {
            if (computed.TryGetValue(row.Id, out var known))
            {
                return known;
            }

            if (!stack.Add(row.Id))
            {
                return ("/" + row.Id + "/", 0);
            }

            (string Path, int Level) result;
            if (row.ParentId is long pid && pid != row.Id && byId.TryGetValue(pid, out var parent))
            {
                var p = Compute(parent, stack);
                result = (p.Path + row.Id + "/", p.Level + 1);
            }
            else
            {
                result = ("/" + row.Id + "/", 0);
            }

            stack.Remove(row.Id);
            computed[row.Id] = result;
            return result;
        }

        var changes = new List<object>();
        foreach (var row in rows)
        {
            var (path, level) = Compute(row, new HashSet<long>());
            if (!string.Equals(row.NodePath, path, StringComparison.Ordinal) || row.NodeLevel != level)
            {
                changes.Add(new { row.Id, Path = path, Level = level, _user.CompanyId });
            }
        }

        if (changes.Count > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE hr.OrgNodes SET NodePath = @Path, NodeLevel = @Level WHERE Id = @Id AND CompanyId = @CompanyId;",
                changes, transaction: tx);
        }
    }

    private static Exception MapSqlException(SqlException ex) => ex.Number switch
    {
        2627 or 2601 => new InvalidOperationException("الكود مستخدم مسبقاً، الرجاء اختيار كود آخر"),
        547 => new ArgumentException("قيمة غير صالحة: نوع العقدة أو أحد المراجع غير موجود"),
        _ => ex
    };

    private static void Validate(OrgNodeInput? input)
    {
        if (input is null) throw new ArgumentException("البيانات مطلوبة");
        if (string.IsNullOrWhiteSpace(input.NameAr)) throw new ArgumentException("الاسم العربي مطلوب");
        if (input.NameAr.Trim().Length > 200) throw new ArgumentException("الاسم العربي يجب ألا يتجاوز 200 حرف");
        if (input.NameEn is not null && input.NameEn.Trim().Length > 200) throw new ArgumentException("الاسم الإنجليزي يجب ألا يتجاوز 200 حرف");
        if (input.Notes is not null && input.Notes.Trim().Length > 1000) throw new ArgumentException("الملاحظات يجب ألا تتجاوز 1000 حرف");
        if (input.Code is <= 0) throw new ArgumentException("الكود يجب أن يكون رقماً موجباً");
        if (string.IsNullOrWhiteSpace(input.NodeType)) throw new ArgumentException("نوع العقدة مطلوب");

        var nodeType = input.NodeType.Trim();
        if (nodeType.Length > 20 || !nodeType.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_' || ch == '-'))
        {
            throw new ArgumentException("نوع العقدة غير صالح");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
=== END FILE ===
=== FILE: src/Hrms.Api/Features/Admin/DevBootstrap.cs ===
using System.Data.Common;
using Dapper;
using Hrms.Api.Data;
using Hrms.Api.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hrms.Api.Features.Admin;

public static class DevBootstrap
{
    public static async Task EnsureAdminAsync(IServiceProvider sp, IConfiguration cfg)
    {
        var password = cfg["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var companyCode = cfg["Bootstrap:CompanyCode"];
        if (string.IsNullOrWhiteSpace(companyCode))
        {
            companyCode = "C1";
        }

        var adminUser = cfg["Bootstrap:AdminUser"];
        if (string.IsNullOrWhiteSpace(adminUser))
        {
            adminUser = "admin";
        }

        await using var scope = sp.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        await using var conn = factory.Create() as DbConnection
                               ?? throw new InvalidOperationException("مصنع الاتصال لا يرجع DbConnection");
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var companyId = await conn.ExecuteScalarAsync<long?>(
            "SELECT TOP 1 Id FROM hr.Companies WHERE Code = @Code AND IsDeleted = 0;",
            new { Code = companyCode.Trim() }, transaction: tx);

        if (companyId is null)
        {
            await tx.RollbackAsync();
            return;
        }

        var userCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM hr.Users WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId AND IsDeleted = 0;",
            new { CompanyId = companyId.Value }, transaction: tx);

        if (userCount > 0)
        {
            await tx.RollbackAsync();
            return;
        }

        var userId = await conn.ExecuteScalarAsync<long>(@"
INSERT hr.Users(CompanyId, Username, PasswordHash, DisplayName, IsAdmin, IsActive)
OUTPUT INSERTED.Id
VALUES(@CompanyId, @Username, @PasswordHash, @DisplayName, 1, 1);",
            new
            {
                CompanyId = companyId.Value,
                Username = adminUser.Trim(),
                PasswordHash = PasswordHasher.Hash(password),
                DisplayName = "مدير النظام"
            }, transaction: tx);

        var roleId = await conn.ExecuteScalarAsync<long?>(
            "SELECT TOP 1 Id FROM hr.Roles WHERE CompanyId = @CompanyId AND NameEn = N'sysadmin' AND IsDeleted = 0;",
            new { CompanyId = companyId.Value }, transaction: tx);

        if (roleId is null)
        {
            var roleCode = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(MAX(Code), 0) + 1 FROM hr.Roles WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId;",
                new { CompanyId = companyId.Value }, transaction: tx);

            roleId = await conn.ExecuteScalarAsync<long>(@"
INSERT hr.Roles(CompanyId, Code, NameAr, NameEn, IsActive, IsSystem, CreatedBy)
OUTPUT INSERTED.Id
VALUES(@CompanyId, @Code, @NameAr, N'sysadmin', 1, 1, @UserId);",
                new { CompanyId = companyId.Value, Code = roleCode, NameAr = "مدير النظام", UserId = userId }, transaction: tx);

            await conn.ExecuteAsync(@"
INSERT hr.RolePermissions(CompanyId, RoleId, PermissionId, CreatedBy)
SELECT @CompanyId, @RoleId, p.Id, @UserId
FROM hr.Permissions p
WHERE p.IsDeleted = 0;",
                new { CompanyId = companyId.Value, RoleId = roleId.Value, UserId = userId }, transaction: tx);
        }

        await conn.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM hr.UserRoles WHERE CompanyId = @CompanyId AND UserId = @UserId AND RoleId = @RoleId AND IsDeleted = 0)
    INSERT hr.UserRoles(CompanyId, UserId, RoleId, CreatedBy)
    VALUES(@CompanyId, @UserId, @RoleId, @UserId);",
            new { CompanyId = companyId.Value, UserId = userId, RoleId = roleId.Value }, transaction: tx);

        await tx.CommitAsync();
    }
}
=== END FILE ===