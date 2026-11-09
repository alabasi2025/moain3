<!-- agent C_tech model gpt-5.3-codex -->
فيما يلي التدقيق التقني الصارم وفق المعطيات فقط.

## 1) قائمة العيوب المصنفة

## حرج

1) **عدم اتساق صلاحيات API مع بذرة الصلاحيات (4 صلاحيات مفقودة)**  
- الدليل: الفحص الآلي أظهر `x-permission not in seed` لأربع صلاحيات:  
  `finance.end_of_service.delete`, `finance.payroll_runs.delete`, `finance.social_insurance.delete`, `finance.social_insurance.update` (دليل: نتائج الفحص الآلي).  
- الأثر: عمليات موجودة في API قد تصبح غير قابلة للتفويض عبر RBAC الافتراضي (مسار API: `DELETE /end-of-service/{id}`, `DELETE /payroll-runs/{id}`, `PUT /social-insurance-runs/{id}`, `DELETE /social-insurance-runs/{id}`).

2) **مخاطر تسرب متعدد الشركات بسبب FK أحادي المفتاح وعدم تقييد CompanyId بين الأب/الابن**  
- الدليل: الجداول الفرعية تحمل `CompanyId`، لكن مفاتيح FK معرفّة على `Id` الأب فقط (مثلًا: `DailyAttendance.EmployeeId -> Employees`, `PayrollRunLines.RunId -> PayrollRuns`) دون FK مركب `(CompanyId, ParentId)` (FK list + جدول/عمود).  
- الأثر: يمكن إدخال مرجع صحيح تقنيًا لكن لشركة مختلفة إذا أخطأت طبقة التطبيق [افتراض].

3) **مسارات حساسة بلا x-permission**  
- الدليل: 11 عملية بلا `x-permission` (نتائج الفحص الآلي)، منها مسارات تشغيلية:  
  `POST /punches/batch`, `GET /mobile/config`, `POST /devices/register` (مسار API).  
- الأثر: الحوكمة غير مكتملة بين Security Scheme وRBAC؛ خصوصًا إدخال البصمات الدفعي [افتراض].

---

## مهم

4) **24 صلاحية مزروعة وغير مستخدمة في API (انجراف تصميم RBAC)**  
- الدليل: `seed perms unused by API: 24` مع القائمة (دليل: نتائج الفحص الآلي).  
- الأثر: تعقيد تشغيلي وتدقيق صلاحيات غير فعّال.

5) **أعمدة مرجعية بلا FK (14 عمودًا)**  
- الدليل: `Id-columns without FK: 14` (نتائج الفحص الآلي)، وأبرزها:  
  `Employees.PhotoAttachmentId`, `EmployeeDocuments.AttachmentId`, `Leaves.AttachmentId`, `Custodies.AttachmentId`, `RefreshTokens.ReplacedByTokenId` (جدول/عمود).  
- الأثر: ضعف السلامة المرجعية.  
- ملاحظة: بعض الأعمدة قد تكون مقصودة كمرجع متعدد الأنواع (مثل `Attachments.OwnerId`, `Requests.ResultEntityId`) [افتراض].

6) **فهارس ناقصة لأحمال ثقيلة (الحضور اليومي/البصمات/الرواتب)**  
- الدليل: الموجود فريد وموجه للنزاهة أكثر من القراءة التحليلية:  
  - `DailyAttendance`: فهرس فريد `(CompanyId,EmployeeId,WorkDate,ShiftId)` فقط (جدول/فهرس).  
  - `PunchLogs`: فهارس فريدة على `ClientId` و`(DeviceId,FingerprintNo,PunchTime)` فقط (جدول/فهرس).  
  - `PayrollRuns`: فهرس فريد على `DocNo` فقط (جدول/فهرس).  
- الأثر: استعلامات القوائم حسب تاريخ/فترة/حالة ستتعرض لمسح أوسع [افتراض].

7) **قيود أعمال مفقودة (CHECK) لسلامة التاريخ/القيم**  
- الدليل: لا يظهر CHECK لـ:  
  - `FiscalYears.StartDate <= EndDate`  
  - `Leaves.FromDate <= ToDate`  
  - `Employees.ContractStart <= ContractEnd` عند وجودهما  
  - قيم موجبة لـ `Amount/Days` في جداول معاملات محددة  
  (قائمة القيود المعروضة لا تحتويها).  
- الأثر: قبول بيانات غير منطقية إن لم تضبطها الخدمة.

8) **تطبيع دلالي ناقص لمرجعيات Lookups**  
- الدليل: أعمدة مثل `Employees.ReligionId`, `MaritalStatusId`, `ContractTypeId`, `StatusId` تشير إلى `Lookups` فقط دون قيد يفرض `LookupType` الموافق (جدول/عمود + FK list).  
- الأثر: احتمال ربط دين بحالة اجتماعية مثلًا [افتراض].

9) **تزامن API غير موثق على الرغم من وجود RowVersion**  
- الدليل: كل الجداول تقريبًا تحوي `RowVersion` (مخطط عام)، لكن المسارات `PUT/PATCH` لا تذكر `If-Match`/نسخة صف (مسار API).  
- الأثر: احتمال lost update إذا لم تطبق الخدمة فحص نسخة السجل [افتراض].

10) **حماية بيانات حساسة على مستوى التخزين غير مثبتة**  
- الدليل: حقول حساسة نصية واضحة: `Employees.IdentityNo`, `PassportNo`, `Users.TwoFactorSecret`, `Employees.BankAccount` (جدول/عمود).  
- الأثر: في غياب Always Encrypted/TDE/تشفير حقلي موثق توجد فجوة امتثال [افتراض].  
- ملاحظة: يوجد تحكم وصول وظيفي في المصفوفة، لكنه لا يثبت التشفير أثناء التخزين (المصفوفة).

---

## تحسين

11) **Soft delete + UNIQUE المفلتر يسمح بإعادة استخدام أرقام وثائق بعد الحذف المنطقي**  
- الدليل: فهارس مثل `UX_Leaves_DocNo`, `UX_PayrollRuns_DocNo` مفلترة بـ `IsDeleted = 0` (فهرس).  
- الأثر: قد يتعارض مع متطلبات التتبع القانونية [افتراض].

12) **غياب CompanyId في `Permissions` مقصود غالبًا لكنه يستلزم حوكمة نشر مركزية**  
- الدليل: `tables without CompanyId: ['Companies','Permissions']` (نتائج الفحص الآلي).  
- الأثر: صلاحية عالمية على مستوى النظام؛ مناسب غالبًا لكنه يحتاج ضبط دورة حياة مركزي [افتراض].

---

## 2) سكربت تصحيحي `06_fixes.sql` (Idempotent)

```sql
/* 06_fixes.sql
   يُنفّذ بعد 01_schema.sql و 02_seed.sql
*/
SET NOCOUNT ON;

------------------------------------------------------------
-- A) إصلاح الصلاحيات الأربع المفقودة في البذرة
------------------------------------------------------------

IF OBJECT_ID('hr.Permissions', 'U') IS NOT NULL
BEGIN
    -- 1) Upsert Permissions
    ;WITH MissingPerms AS (
        SELECT N'finance.end_of_service.delete' AS PermCode, N'finance' AS Module, N'حذف سند نهاية الخدمة' AS DescriptionAr, N'Delete end of service' AS DescriptionEn
        UNION ALL SELECT N'finance.payroll_runs.delete',   N'finance', N'حذف كشف رواتب', N'Delete payroll run'
        UNION ALL SELECT N'finance.social_insurance.delete',N'finance', N'حذف كشف التأمينات', N'Delete social insurance run'
        UNION ALL SELECT N'finance.social_insurance.update',N'finance', N'تعديل كشف التأمينات', N'Update social insurance run'
    )
    -- إعادة تفعيل إن كانت محذوفة منطقيًا
    UPDATE p
       SET p.IsDeleted = 0,
           p.Module = mp.Module,
           p.DescriptionAr = mp.DescriptionAr,
           p.DescriptionEn = mp.DescriptionEn,
           p.UpdatedAt = SYSDATETIMEOFFSET()
    FROM hr.Permissions p
    JOIN MissingPerms mp ON mp.PermCode = p.PermCode
    WHERE p.IsDeleted = 1;

    -- إدراج إن لم توجد
    INSERT INTO hr.Permissions (PermCode, Module, DescriptionAr, DescriptionEn)
    SELECT mp.PermCode, mp.Module, mp.DescriptionAr, mp.DescriptionEn
    FROM MissingPerms mp
    WHERE NOT EXISTS (
        SELECT 1
        FROM hr.Permissions p
        WHERE p.PermCode = mp.PermCode
          AND p.IsDeleted = 0
    );
END;

------------------------------------------------------------
-- B) منح الصلاحيات إلى أدوار sysadmin و payroll
-- [افتراض]: التعرف على الدورين عبر Roles.NameEn بالقيم sysadmin/payroll
------------------------------------------------------------

IF OBJECT_ID('hr.RolePermissions', 'U') IS NOT NULL
   AND OBJECT_ID('hr.Roles', 'U') IS NOT NULL
   AND OBJECT_ID('hr.Permissions', 'U') IS NOT NULL
BEGIN
    ;WITH TargetPerms AS (
        SELECT p.Id AS PermissionId, p.PermCode
        FROM hr.Permissions p
        WHERE p.IsDeleted = 0
          AND p.PermCode IN (
              N'finance.end_of_service.delete',
              N'finance.payroll_runs.delete',
              N'finance.social_insurance.delete',
              N'finance.social_insurance.update'
          )
    ),
    TargetRoles AS (
        SELECT r.Id AS RoleId, r.CompanyId
        FROM hr.Roles r
        WHERE r.IsDeleted = 0
          AND LOWER(ISNULL(r.NameEn, N'')) IN (N'sysadmin', N'payroll')
    )
    INSERT INTO hr.RolePermissions (CompanyId, RoleId, PermissionId)
    SELECT tr.CompanyId, tr.RoleId, tp.PermissionId
    FROM TargetRoles tr
    CROSS JOIN TargetPerms tp
    WHERE NOT EXISTS (
        SELECT 1
        FROM hr.RolePermissions rp
        WHERE rp.CompanyId = tr.CompanyId
          AND rp.RoleId = tr.RoleId
          AND rp.PermissionId = tp.PermissionId
          AND rp.IsDeleted = 0
    );
END;

------------------------------------------------------------
-- C) فهارس أداء أساسية (الحضور اليومي، البصمات، الرواتب)
------------------------------------------------------------

-- DailyAttendance: الاستعلام حسب التاريخ/الشركة
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_DailyAttendance_Company_WorkDate_Employee'
      AND object_id = OBJECT_ID('hr.DailyAttendance')
)
BEGIN
    CREATE INDEX IX_DailyAttendance_Company_WorkDate_Employee
    ON hr.DailyAttendance (CompanyId, WorkDate, EmployeeId)
    INCLUDE (ShiftId, Status, WorkMinutes, LateMin, EarlyLeaveMin, AbsentMin, OvertimeMin, ExcusedMin, MonthlyId)
    WHERE IsDeleted = 0;
END;

-- PunchLogs: الاستعلام الزمني العام
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PunchLogs_Company_PunchTime'
      AND object_id = OBJECT_ID('hr.PunchLogs')
)
BEGIN
    CREATE INDEX IX_PunchLogs_Company_PunchTime
    ON hr.PunchLogs (CompanyId, PunchTime)
    INCLUDE (EmployeeId, DeviceId, Direction, Status, Source, LocalTime)
    WHERE IsDeleted = 0;
END;

-- PunchLogs: سجل موظف عبر فترة
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PunchLogs_Company_Employee_PunchTime'
      AND object_id = OBJECT_ID('hr.PunchLogs')
)
BEGIN
    CREATE INDEX IX_PunchLogs_Company_Employee_PunchTime
    ON hr.PunchLogs (CompanyId, EmployeeId, PunchTime)
    INCLUDE (DeviceId, Direction, Status, Source, LocalTime)
    WHERE IsDeleted = 0 AND EmployeeId IS NOT NULL;
END;

-- PayrollRuns: التصفية حسب الفترة/الحالة
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PayrollRuns_Company_Period_Status'
      AND object_id = OBJECT_ID('hr.PayrollRuns')
)
BEGIN
    CREATE INDEX IX_PayrollRuns_Company_Period_Status
    ON hr.PayrollRuns (CompanyId, PeriodYear, PeriodMonth, Status)
    INCLUDE (RunType, IssueDate, ApprovedAt, PaidAt)
    WHERE IsDeleted = 0;
END;

-- PayrollRunLines: استعلام قسائم موظف عبر تشغيلات
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PayrollRunLines_Company_Employee_RunId'
      AND object_id = OBJECT_ID('hr.PayrollRunLines')
)
BEGIN
    CREATE INDEX IX_PayrollRunLines_Company_Employee_RunId
    ON hr.PayrollRunLines (CompanyId, EmployeeId, RunId)
    INCLUDE (NetPay, TotalEarnings, TotalDeductions, BasicSalary, WorkDays)
    WHERE IsDeleted = 0;
END;

-- VariableItems: فلاتر الفترة
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_VariableItems_Company_Period_Employee'
      AND object_id = OBJECT_ID('hr.VariableItems')
)
BEGIN
    CREATE INDEX IX_VariableItems_Company_Period_Employee
    ON hr.VariableItems (CompanyId, PeriodYear, PeriodMonth, EmployeeId)
    INCLUDE (PayrollItemId, Amount, Percentage, IsApplied, PayrollRunId)
    WHERE IsDeleted = 0;
END;
```

---

## 3) تعديلات OpenAPI مقترحة لربط الصلاحيات الـ24 غير المستخدمة

> الهدف: إزالة drift بين `02_seed.sql` و`x-permission`.

1. `admin.audit_logs.export` → `GET /audit-logs/export` **أو** `GET /audit-logs?format=csv|xlsx` (مسار API: `/audit-logs`).  
2. `attendance.daily.export` → `GET /attendance/daily/export` **أو** `GET /attendance/daily?format=`.  
3. `attendance.excuses.approve` → `POST /excuses/{id}/approve` و`POST /excuses/{id}/reject`.  
4. `attendance.monthly.export` → `GET /attendance/monthly/export` **أو** `GET /attendance/monthly?format=`.  
5. `attendance.punches.export` → `GET /punches/export` **أو** `GET /punches?format=`.  
6. `attendance.punches.process` → `POST /punches/process` (معالجة/اعتماد دفعات).  
7. `employees.employees.export` → `POST /employees/query/export` **أو** `POST /employees/query?format=`.  
8. `employees.government.read` → `GET /employees/{id}/government`.  
9. `employees.government.update` → `PUT /employees/{id}/government`.  
10. `employees.portal_account.read` → `GET /employees/{id}/portal-account`.  
11. `finance.advances.export` → `GET /advances/export` **أو** `GET /advances?format=`.  
12. `finance.end_of_service.approve` → `POST /end-of-service/{id}/approve` (وممكن `unapprove`).  
13. `finance.end_of_service.export` → `GET /end-of-service/export` **أو** `GET /end-of-service?format=`.  
14. `finance.payroll_runs.export` → `GET /payroll-runs/export` **أو** `GET /payroll-runs?format=`.  
15. `finance.payroll_runs.update` → `PUT /payroll-runs/{id}` (تحديث بيانات تشغيل draft).  
16. `finance.payslips.export` → `GET /payroll-runs/{id}/payslips/export` [افتراض].  
17. `finance.social_insurance.approve` → `POST /social-insurance-runs/{id}/approve`.  
18. `finance.social_insurance.export` → `GET /social-insurance-runs/export` **أو** `GET /social-insurance-runs?format=`.  
19. `finance.variable_items.approve` → `POST /variable-items/{id}/approve` (أو bulk approve).  
20. `leaves.balances.update` → `PUT /leave-balances/{id}` أو `PATCH /leave-balances/{id}`.  
21. `leaves.leaves.approve` → `POST /leaves/{id}/approve` و`POST /leaves/{id}/reject` [افتراض].  
22. `leaves.leaves.export` → `GET /leaves/export` **أو** `GET /leaves?format=`.  
23. `me.leaves.create` → `POST /me/leaves` (بدل تمريرها فقط عبر `/me/requests`).  
24. `reports.reports.export` → فصل صلاحية التصدير في `GET /reports/{key}` عندما `format!=json` (مسار API: `/reports/{key}`).

إذا رغبت، أقدّم نسخة ثانية من `06_fixes.sql` تضيف أيضًا **قيود CHECK الأساسية** (تواريخ/قيم موجبة) و**Fks الآمنة الممكنة** (مثل `RefreshTokens.ReplacedByTokenId` self-FK) دون كسر البيانات الحالية.