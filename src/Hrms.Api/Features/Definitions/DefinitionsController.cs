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
