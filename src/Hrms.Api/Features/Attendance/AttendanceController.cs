using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Features.Employees;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Features.Attendance;

[ApiController]
[Route("api/v1/attendance")]
public sealed class AttendanceController(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("process")]
    [HasPermission("attendance.daily.process")]
    public async Task<ApiResponse<AttendanceProcessResult>> Process(
        [FromBody] AttendanceProcessInput input)
    {
        var processor = new AttendanceProcessor(connectionFactory, currentUser);
        return new(await processor.ProcessAsync(input));
    }

    [HttpGet("daily")]
    [HasPermission("attendance.daily.read")]
    public async Task<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>> Daily(
        long? employeeId = null, DateOnly? from = null, DateOnly? to = null)
    {
        if (employeeId is <= 0 || (from.HasValue && to.HasValue && from > to))
            throw new ArgumentException("مرشحات البحث غير صالحة");

        using var db = FeatureDb.Open(connectionFactory);
        var rows = await FeatureDb.Query(db, """
            SELECT d.*
            FROM hr.DailyAttendance d
            INNER JOIN hr.Employees e ON e.Id=d.EmployeeId AND e.CompanyId=d.CompanyId
            WHERE d.CompanyId=@CompanyId AND d.IsDeleted=0
              AND e.CompanyId=@CompanyId AND e.IsDeleted=0
              AND (@EmployeeId IS NULL OR d.EmployeeId=@EmployeeId)
              AND (@FromDate IS NULL OR d.WorkDate>=@FromDate)
              AND (@ToDate IS NULL OR d.WorkDate<=@ToDate)
            ORDER BY d.WorkDate,d.EmployeeId,d.Id
            """, FeatureDb.Parameters(currentUser, new
        {
            EmployeeId = employeeId,
            FromDate = from,
            ToDate = to
        }));
        return new(rows.Select(FeatureDb.JsonRow).ToArray());
    }
}
