/* 07_verify.sql — negative tests: each statement MUST fail. Run after 01,02,06 on a test DB. */
SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON;
DECLARE @r TABLE(Test nvarchar(100), Result nvarchar(20));
BEGIN TRY INSERT hr.FiscalYears(CompanyId,FiscalYear,StartDate,EndDate) VALUES(1,2026,'2026-12-31','2026-01-01'); INSERT @r VALUES(N'FiscalYear start>end',N'FAIL-accepted'); END TRY BEGIN CATCH INSERT @r VALUES(N'FiscalYear start>end',N'PASS-rejected'); END CATCH;
BEGIN TRY INSERT hr.PayrollRuns(CompanyId,DocNo,PeriodYear,PeriodMonth,IssueDate) VALUES(1,999,2026,13,'2026-01-01'); INSERT @r VALUES(N'PayrollRun month=13',N'FAIL-accepted'); END TRY BEGIN CATCH INSERT @r VALUES(N'PayrollRun month=13',N'PASS-rejected'); END CATCH;
BEGIN TRY INSERT hr.Shifts(CompanyId,Code,NameAr,StartTime,EndTime,WorkMinutes) VALUES(1,999,N'x','08:00','16:00',0); INSERT @r VALUES(N'Shift WorkMinutes=0',N'FAIL-accepted'); END TRY BEGIN CATCH INSERT @r VALUES(N'Shift WorkMinutes=0',N'PASS-rejected'); END CATCH;
BEGIN TRY INSERT hr.Permissions(PermCode,Module) VALUES(N'admin.companies.read',N'admin'); INSERT @r VALUES(N'Duplicate PermCode',N'FAIL-accepted'); END TRY BEGIN CATCH INSERT @r VALUES(N'Duplicate PermCode',N'PASS-rejected'); END CATCH;
SELECT Test, Result FROM @r;
SELECT N'missing x-permission codes in seed' AS Chk, COUNT(*) AS Missing FROM (VALUES (N'finance.payroll_runs.delete'),(N'finance.social_insurance.update'),(N'finance.social_insurance.delete'),(N'finance.end_of_service.delete')) v(c) WHERE NOT EXISTS (SELECT 1 FROM hr.Permissions p WHERE p.PermCode=v.c);
