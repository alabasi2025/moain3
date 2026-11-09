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
