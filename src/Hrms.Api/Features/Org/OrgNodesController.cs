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
