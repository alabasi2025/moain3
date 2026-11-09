/* =====================================================================
   06_fixes.sql — Corrective script for HRMS pack (run AFTER 01 and 02)
   - Idempotent: safe to run more than once.
   - Verified on SQL Server 2022 (16.0.4305.5) with: sqlcmd -I (QUOTED_IDENTIFIER ON)
   - Each block references the finding ID in docs/03_التدقيق_التقني.md
   ===================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   -- F-01: filtered indexes require it (01_schema fails without -I)
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

/* ---------------------------------------------------------------------
   F-02  Four x-permission codes used by OpenAPI but missing from seed
   --------------------------------------------------------------------- */
INSERT INTO hr.Permissions (PermCode, Module, DescriptionAr, DescriptionEn)
SELECT v.PermCode, N'finance', v.Ar, v.En
FROM (VALUES
  (N'finance.payroll_runs.delete',      N'حذف كشوف الرواتب (مسودة فقط)',      N'delete payroll_runs (draft only)'),
  (N'finance.social_insurance.update',  N'تعديل كشف التأمينات (مسودة فقط)',   N'update social_insurance (draft only)'),
  (N'finance.social_insurance.delete',  N'حذف كشف التأمينات (مسودة فقط)',     N'delete social_insurance (draft only)'),
  (N'finance.end_of_service.delete',    N'حذف سند نهاية الخدمة (مسودة فقط)',  N'delete end_of_service (draft only)')
) v(PermCode, Ar, En)
WHERE NOT EXISTS (SELECT 1 FROM hr.Permissions p WHERE p.PermCode = v.PermCode);
GO
/* Decision D-03: grant to sysadmin + payroll (owners of these documents). */
INSERT INTO hr.RolePermissions (CompanyId, RoleId, PermissionId)
SELECT r.CompanyId, r.Id, p.Id
FROM hr.Roles r
JOIN hr.Permissions p ON p.PermCode IN (N'finance.payroll_runs.delete', N'finance.social_insurance.update',
                                         N'finance.social_insurance.delete', N'finance.end_of_service.delete')
WHERE r.IsDeleted = 0 AND r.NameEn IN (N'sysadmin', N'payroll')
  AND NOT EXISTS (SELECT 1 FROM hr.RolePermissions rp
                  WHERE rp.CompanyId = r.CompanyId AND rp.RoleId = r.Id AND rp.PermissionId = p.Id AND rp.IsDeleted = 0);
GO

/* ---------------------------------------------------------------------
   F-07  Missing business CHECK constraints (dates / ranges / amounts)
   --------------------------------------------------------------------- */
DECLARE @c TABLE (Name sysname, Tbl sysname, Expr nvarchar(400));
INSERT @c VALUES
 (N'CK_FiscalYears_Dates',          N'FiscalYears',         N'[StartDate] <= [EndDate]'),
 (N'CK_Leaves_Dates',               N'Leaves',              N'[FromDate] <= [ToDate] AND [Days] > 0'),
 (N'CK_Delegations_Dates',          N'Delegations',         N'[FromDate] <= [ToDate] AND [FromUserId] <> [ToUserId]'),
 (N'CK_EmployeeShifts_Dates',       N'EmployeeShifts',      N'[EffectiveTo] IS NULL OR [EffectiveFrom] <= [EffectiveTo]'),
 (N'CK_Employees_Contract',         N'Employees',           N'[ContractEnd] IS NULL OR [ContractStart] IS NULL OR [ContractStart] <= [ContractEnd]'),
 (N'CK_Employees_SocialPct',        N'Employees',           N'[SocialCompanyPct] BETWEEN 0 AND 100 AND [SocialEmployeePct] BETWEEN 0 AND 100'),
 (N'CK_Advances_Amounts',           N'Advances',            N'[Amount] > 0 AND [InstallmentsCount] >= 1 AND [InstallmentAmount] > 0 AND [RemainingAmount] >= 0 AND [FirstDeductMonth] BETWEEN 1 AND 12'),
 (N'CK_AdvanceInstallments_Month',  N'AdvanceInstallments', N'[PeriodMonth] BETWEEN 1 AND 12 AND [Amount] >= 0 AND [PaidAmount] >= 0'),
 (N'CK_VariableItems_Month',        N'VariableItems',       N'[PeriodMonth] BETWEEN 1 AND 12'),
 (N'CK_PayrollRuns_Month',          N'PayrollRuns',         N'[PeriodMonth] BETWEEN 1 AND 12'),
 (N'CK_SocialInsuranceRuns_Month',  N'SocialInsuranceRuns', N'[PeriodMonth] BETWEEN 1 AND 12'),
 (N'CK_MonthlyAttendances_Period',  N'MonthlyAttendances',  N'[PeriodMonth] BETWEEN 1 AND 12 AND [PeriodFrom] <= [PeriodTo]'),
 (N'CK_ShiftDays_DayOfWeek',        N'ShiftDays',           N'[DayOfWeek] BETWEEN 0 AND 6'),
 (N'CK_Shifts_Minutes',             N'Shifts',              N'[WorkMinutes] > 0 AND [WorkMinutes] <= 1440 AND [BreakMinutes] >= 0'),
 (N'CK_DailyAttendance_Minutes',    N'DailyAttendance',     N'[RequiredMinutes] >= 0 AND [WorkMinutes] >= 0 AND [LateMin] >= 0 AND [EarlyLeaveMin] >= 0 AND [AbsentMin] >= 0 AND [OvertimeMin] >= 0 AND [ExcusedMin] >= 0'),
 (N'CK_Custodies_Dates',            N'Custodies',           N'[ReturnedOn] IS NULL OR [DeliveredOn] <= [ReturnedOn]');
DECLARE @n sysname, @t sysname, @e nvarchar(400), @sql nvarchar(max);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Name, Tbl, Expr FROM @c;
OPEN cur; FETCH NEXT FROM cur INTO @n, @t, @e;
WHILE @@FETCH_STATUS = 0
BEGIN
  IF OBJECT_ID(N'hr.' + @n, N'C') IS NULL
  BEGIN
    SET @sql = N'ALTER TABLE hr.' + QUOTENAME(@t) + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@n) + N' CHECK (' + @e + N');';
    EXEC sys.sp_executesql @sql;
  END
  FETCH NEXT FROM cur INTO @n, @t, @e;
END
CLOSE cur; DEALLOCATE cur;
GO

/* ---------------------------------------------------------------------
   F-05  Reference columns without FK (safe ones only).
   Polymorphic columns intentionally left without FK:
   AuditLogs.EntityId, Attachments.OwnerId, Requests.ResultEntityId (see D-05)
   --------------------------------------------------------------------- */
DECLARE @f TABLE (Name sysname, Tbl sysname, Col sysname, RefTbl sysname);
INSERT @f VALUES
 (N'FK_RefreshTokens_ReplacedByTokenId', N'RefreshTokens',     N'ReplacedByTokenId', N'RefreshTokens'),
 (N'FK_AuditLogs_UserId',                N'AuditLogs',         N'UserId',            N'Users'),
 (N'FK_Companies_LogoAttachmentId',      N'Companies',         N'LogoAttachmentId',  N'Attachments'),
 (N'FK_ReportHeaders_LogoAttachmentId',  N'ReportHeaders',     N'LogoAttachmentId',  N'Attachments'),
 (N'FK_Employees_PhotoAttachmentId',     N'Employees',         N'PhotoAttachmentId', N'Attachments'),
 (N'FK_EmployeeDocuments_AttachmentId',  N'EmployeeDocuments', N'AttachmentId',      N'Attachments'),
 (N'FK_EmployeeArchive_AttachmentId',    N'EmployeeArchive',   N'AttachmentId',      N'Attachments'),
 (N'FK_Leaves_AttachmentId',             N'Leaves',            N'AttachmentId',      N'Attachments'),
 (N'FK_Custodies_AttachmentId',          N'Custodies',         N'AttachmentId',      N'Attachments');
DECLARE @fn sysname, @ft sysname, @fc sysname, @fr sysname, @fsql nvarchar(max);
DECLARE fcur CURSOR LOCAL FAST_FORWARD FOR SELECT Name, Tbl, Col, RefTbl FROM @f;
OPEN fcur; FETCH NEXT FROM fcur INTO @fn, @ft, @fc, @fr;
WHILE @@FETCH_STATUS = 0
BEGIN
  IF OBJECT_ID(N'hr.' + @fn, N'F') IS NULL
  BEGIN
    SET @fsql = N'ALTER TABLE hr.' + QUOTENAME(@ft) + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@fn)
              + N' FOREIGN KEY (' + QUOTENAME(@fc) + N') REFERENCES hr.' + QUOTENAME(@fr) + N'([Id]);';
    EXEC sys.sp_executesql @fsql;
  END
  FETCH NEXT FROM fcur INTO @fn, @ft, @fc, @fr;
END
CLOSE fcur; DEALLOCATE fcur;
GO

/* ---------------------------------------------------------------------
   F-06  Missing indexes for heavy read paths
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PunchLogs_Company_PunchTime')
  CREATE INDEX IX_PunchLogs_Company_PunchTime ON hr.PunchLogs (CompanyId, PunchTime)
    INCLUDE (EmployeeId, Direction, Status, Source) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DailyAttendance_Company_WorkDate')
  CREATE INDEX IX_DailyAttendance_Company_WorkDate ON hr.DailyAttendance (CompanyId, WorkDate)
    INCLUDE (EmployeeId, Status, LateMin, EarlyLeaveMin, AbsentMin, OvertimeMin) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollRuns_Company_Period')
  CREATE INDEX IX_PayrollRuns_Company_Period ON hr.PayrollRuns (CompanyId, PeriodYear, PeriodMonth, Status) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AdvanceInstallments_Company_Period')
  CREATE INDEX IX_AdvanceInstallments_Company_Period ON hr.AdvanceInstallments (CompanyId, PeriodYear, PeriodMonth, Status)
    INCLUDE (AdvanceId, Amount, PaidAmount) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VariableItems_Company_Period')
  CREATE INDEX IX_VariableItems_Company_Period ON hr.VariableItems (CompanyId, PeriodYear, PeriodMonth, IsApplied)
    INCLUDE (EmployeeId, PayrollItemId, Amount) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Leaves_Employee_Dates')
  CREATE INDEX IX_Leaves_Employee_Dates ON hr.Leaves (EmployeeId, FromDate, ToDate) INCLUDE (Status, LeaveTypeId, IsHalfDay) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Excuses_Employee_Date')
  CREATE INDEX IX_Excuses_Employee_Date ON hr.Excuses (EmployeeId, ExcuseDate) INCLUDE (FromTime, ToTime, IsApproved) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Requests_Company_Status')
  CREATE INDEX IX_Requests_Company_Status ON hr.Requests (CompanyId, Status, RequestType) INCLUDE (EmployeeId, CurrentStepNo) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_User_Unread')
  CREATE INDEX IX_Notifications_User_Unread ON hr.Notifications (UserId, IsRead) INCLUDE (CreatedAt) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Company_OccurredAt')
  CREATE INDEX IX_AuditLogs_Company_OccurredAt ON hr.AuditLogs (CompanyId, OccurredAt) INCLUDE (EntityName, ActionType, UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeDocuments_Expiry')
  CREATE INDEX IX_EmployeeDocuments_Expiry ON hr.EmployeeDocuments (CompanyId, ExpiryDate) INCLUDE (EmployeeId, DocumentTypeId) WHERE IsDeleted = 0 AND ExpiryDate IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RefreshTokens_TokenHash')
  CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON hr.RefreshTokens (TokenHash);
GO

/* ---------------------------------------------------------------------
   F-10  Yemen timezone: Asia/Aden (same UTC+3 offset as Riyadh, correct IANA zone)
   --------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Companies_TimeZone'
           AND definition LIKE N'%Asia/Riyadh%')
BEGIN
  ALTER TABLE hr.Companies DROP CONSTRAINT DF_Companies_TimeZone;
  ALTER TABLE hr.Companies ADD CONSTRAINT DF_Companies_TimeZone DEFAULT ('Asia/Aden') FOR TimeZone;
END
GO

/* ---------------------------------------------------------------------
   L-xx  Yemeni Labour Law No.5/1995 + Social Insurance Law 26/1991 defaults
   Usage: EXEC hr.usp_ApplyYemenLaborDefaults @CompanyId = <id>;  (after usp_SeedCompanyDefaults)
   Every value cites its article. Values are MINIMUMS from law; company may be more generous (Art.79/5).
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE hr.usp_ApplyYemenLaborDefaults @CompanyId bigint
AS
BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRAN;

  /* 1) Settings (MERGE = idempotent; existing company overrides are NOT overwritten) */
  MERGE hr.Settings AS t
  USING (VALUES
    (N'labor.country',                       N'YE'),
    (N'labor.month_days',                    N'30'),            -- Art.9
    (N'labor.year_days',                     N'365'),           -- Art.9
    (N'work.daily_hours_max',                N'8'),             -- Art.71/1
    (N'work.weekly_hours_max',               N'48'),            -- Art.71/1
    (N'work.ramadan_daily_hours_max',        N'6'),             -- Art.71/2
    (N'work.ramadan_weekly_hours_max',       N'36'),            -- Art.71/2
    (N'work.break_max_minutes',              N'60'),            -- Art.71/4
    (N'work.max_continuous_minutes',         N'300'),           -- Art.71/4
    (N'work.daily_total_with_ot_max_hours',  N'12'),            -- Art.74/2
    (N'work.weekly_rest_day',                N'5'),             -- Art.77 (Friday; .NET DayOfWeek=5)
    (N'work.night_from',                     N'20:00'),         -- Art.73/1
    (N'work.night_to',                       N'05:00'),         -- Art.73/1
    (N'work.night_continuous_max_days',      N'30'),            -- Art.73/1
    (N'overtime.rate_normal',                N'1.5'),           -- Art.56/a
    (N'overtime.rate_night_rest_holiday',    N'2.0'),           -- Art.56/b (+ holiday pay)
    (N'overtime.basis',                      N'basic'),         -- Art.56 «من الأجر الأساسي»
    (N'allowance.night_pct_basic',           N'15'),            -- Art.57/1
    (N'allowance.shift_pct_basic',           N'10'),            -- Art.57/2
    (N'allowance.night_shift_min_days',      N'10'),            -- Art.57/3 (no combining)
    (N'leave.annual.days_min',               N'30'),            -- Art.79/1
    (N'leave.annual.monthly_accrual',        N'2.5'),           -- Art.79/1
    (N'leave.annual.min_chunk_days',         N'2'),             -- Art.79/3
    (N'leave.annual.carryover_pct',          N'50'),            -- Art.79/4
    (N'leave.annual.cash_out_allowed',       N'false'),         -- Art.79/6
    (N'leave.annual.excludes_holidays',      N'true'),          -- Art.79/2
    (N'leave.sick.tiers',                    N'60:100;60:85;60:75;60:50'), -- Art.80/1 (days:pay%)
    (N'leave.sick.cert_required_after_days', N'10'),            -- Art.82/3
    (N'leave.maternity.days',                N'60'),            -- Art.45/1
    (N'leave.maternity.extra_days',          N'20'),            -- Art.45/3 (difficult birth / twins)
    (N'leave.hajj.days',                     N'20'),            -- Art.84
    (N'leave.hajj.min_service_years',        N'4'),             -- Art.84 (once per service)
    (N'leave.casual.max_days_year',          N'10'),            -- Art.85
    (N'leave.widow.paid_days',               N'40'),            -- Art.87
    (N'leave.widow.unpaid_max_days',         N'90'),            -- Art.87
    (N'women.pregnancy_nursing_daily_hours', N'5'),             -- Art.43 (from 6th month / nursing to 6th month)
    (N'women.no_overtime_rule',              N'true'),          -- Art.44
    (N'juvenile.daily_hours_max',            N'7'),             -- Art.48/1
    (N'juvenile.weekly_hours_max',           N'42'),            -- Art.48/1
    (N'contract.probation_max_months',       N'6'),             -- Art.28
    (N'contract.notice_days_monthly',        N'30'),            -- Art.38/3-a
    (N'contract.notice_days_half_monthly',   N'15'),            -- Art.38/3-b
    (N'contract.notice_days_other',          N'7'),             -- Art.38/3-c
    (N'contract.absence_termination_interrupted_days', N'30'),  -- Art.36/d (warning after 15)
    (N'contract.absence_termination_continuous_days',  N'15'),  -- Art.36/d (warning after 7)
    (N'discipline.deduction_max_pct_basic',  N'20'),            -- Art.93/3
    (N'discipline.damage_installment_max_pct_basic', N'25'),    -- Art.64
    (N'discipline.action_deadline_days',     N'15'),            -- Art.94/3-a
    (N'payroll.pay_deadline_day',            N'6'),             -- Art.61/1
    (N'eos.uninsured_months_per_year',       N'1'),             -- Art.120/2 (last month wage)
    (N'insurance.employer_pct',              N'9'),             -- Law 26/1991
    (N'insurance.employee_pct',              N'6')              -- Law 26/1991
  ) AS s(K, V)
  ON t.CompanyId = @CompanyId AND t.SettingKey = s.K AND t.IsDeleted = 0
  WHEN NOT MATCHED THEN INSERT (CompanyId, SettingKey, SettingValue) VALUES (@CompanyId, s.K, s.V);

  /* 2) Leave types — align seed defaults with the law (seed codes 1..7) */
  UPDATE hr.LeaveTypes SET AnnualEntitlementDays = 30, AccrualMode = N'monthly', CarryOverMaxDays = 15,
         CountsHolidays = 0, Notes = N'م79: 30 يوماً، 2.5/شهر، ترحيل النصف، لا تعويض نقدي'
   WHERE CompanyId = @CompanyId AND Code = 1 AND IsDeleted = 0;
  UPDATE hr.LeaveTypes SET MaxDays = 240, RequiresAttachment = 1,
         Notes = N'م80-82: 8 أشهر متدرجة 100/85/75/50%، شهادة طبية، اعتماد إذا تجاوزت 10 أيام'
   WHERE CompanyId = @CompanyId AND Code = 2 AND IsDeleted = 0;
  UPDATE hr.LeaveTypes SET NameAr = N'عارضة', NameEn = N'Casual', MaxDays = 10, DeductsBalance = 0,
         Notes = N'م85: حتى 10 أيام مدفوعة في العام (جوازية)'
   WHERE CompanyId = @CompanyId AND Code = 3 AND IsDeleted = 0;
  UPDATE hr.LeaveTypes SET MaxDays = 60, RequiresAttachment = 1,
         Notes = N'م45: 60 يوماً بأجر كامل + 20 يوماً للولادة المتعسرة أو التوأم'
   WHERE CompanyId = @CompanyId AND Code = 5 AND IsDeleted = 0;
  UPDATE hr.LeaveTypes SET Notes = N'سياسة شركة — غير منصوص في قانون العمل 5/1995'
   WHERE CompanyId = @CompanyId AND Code IN (6, 7) AND IsDeleted = 0;

  INSERT INTO hr.LeaveTypes (CompanyId, Code, NameAr, NameEn, IsPaid, DeductsBalance, RequiresAttachment, MaxDays,
                             AnnualEntitlementDays, AccrualMode, GenderRestriction, Notes)
  SELECT @CompanyId, v.Code, v.Ar, v.En, v.Paid, 0, v.Att, v.MaxD, NULL, N'none', v.G, v.N
  FROM (VALUES
    (8,  N'حج',             N'Hajj',          1, 1, 20,   NULL,  N'م84: 20 يوماً مرة واحدة بعد 4 سنوات خدمة فعلية'),
    (9,  N'عدة وفاة الزوج', N'Widow (Iddah)', 1, 1, 40,   N'F',  N'م87: 40 يوماً مدفوعة + حتى 90 يوماً بدون أجر'),
    (10, N'إصابة عمل',      N'Work injury',   1, 1, NULL, NULL,  N'م83: بأجر كامل حتى البت وفق قانون التأمينات')
  ) v(Code, Ar, En, Paid, Att, MaxD, G, N)
  WHERE NOT EXISTS (SELECT 1 FROM hr.LeaveTypes lt WHERE lt.CompanyId = @CompanyId AND lt.Code = v.Code AND lt.IsDeleted = 0);

  /* 3) Company timezone for Yemen */
  UPDATE hr.Companies SET TimeZone = N'Asia/Aden' WHERE Id = @CompanyId AND TimeZone = N'Asia/Riyadh';

  COMMIT;
END;
GO
