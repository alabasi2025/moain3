using System.Data;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Features.Employees;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Features.Attendance;

[ApiController]
[Route("api/v1/punches")]
public sealed class PunchesController(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("manual")]
    [HasPermission("attendance.punches.create")]
    public async Task<ApiResponse<object>> Manual([FromBody] ManualPunchInput input)
    {
        if (input.EmployeeId <= 0 || input.PunchTime == default ||
            input.Direction is not ("in" or "out" or "unknown") ||
            string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 300)
            throw new ArgumentException("الموظف ووقت البصمة والاتجاه والسبب الصحيح إلزامية");

        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var employee = await FeatureDb.Require(db, tx, currentUser, "Employees", input.EmployeeId);
        var id = await FeatureDb.Insert(db, tx, currentUser, "PunchLogs", FeatureDb.Values(new
        {
            input.EmployeeId,
            FingerprintNo = employee["FingerprintNo"],
            input.PunchTime,
            LocalTime = DateTime.SpecifyKind(input.PunchTime.DateTime, DateTimeKind.Unspecified),
            input.Direction,
            Reason = input.Reason.Trim(),
            Source = "manual",
            Status = "accepted",
            ReviewedBy = currentUser.UserId,
            ReviewedAt = DateTimeOffset.UtcNow
        }));
        tx.Commit();
        return new(new { id });
    }

    [HttpGet]
    [HasPermission("attendance.punches.read")]
    public async Task<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>> List(
        long? employeeId = null, DateOnly? from = null, DateOnly? to = null)
    {
        if (employeeId is <= 0 || (from.HasValue && to.HasValue && from > to))
            throw new ArgumentException("مرشحات البحث غير صالحة");

        using var db = FeatureDb.Open(connectionFactory);
        var rows = await FeatureDb.Query(db, """
            SELECT p.*
            FROM hr.PunchLogs p
            WHERE p.CompanyId=@CompanyId AND p.IsDeleted=0
              AND (@EmployeeId IS NULL OR p.EmployeeId=@EmployeeId)
              AND (@FromDate IS NULL OR p.LocalTime>=@FromDate)
              AND (@ToDate IS NULL OR CONVERT(date,p.LocalTime)<=@ToDate)
              AND (p.EmployeeId IS NULL OR EXISTS (
                  SELECT 1 FROM hr.Employees e
                  WHERE e.CompanyId=@CompanyId AND e.IsDeleted=0
                    AND e.Id=p.EmployeeId))
            ORDER BY p.LocalTime,p.Id
            """, FeatureDb.Parameters(currentUser, new
        {
            EmployeeId = employeeId,
            FromDate = from,
            ToDate = to
        }));
        return new(rows.Select(FeatureDb.JsonRow).ToArray());
    }
}

public sealed record ManualPunchInput(
    long EmployeeId,
    DateTimeOffset PunchTime,
    string Direction,
    string Reason);
