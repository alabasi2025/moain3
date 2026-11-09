<!-- agent C_tech model gpt-5.3-codex -->
فيما يلي التدقيق التقني الصارم بناءً على المادة المتاحة فقط، مع تمييز ما هو [افتراض].

---

## 1) قائمة العيوب المصنّفة مع الدليل

## عيوب حرجة

1) **تعذّر فحص OpenAPI آليًا (فقدان الملف)**
- الدليل: `FileNotFoundError ... '03_openapi.yaml'`.
- الأثر: لا يمكن التحقق الآلي من اتساق `security`, `operationId`, و`scopes` مع الصلاحيات الفعلية.  
- المرجع: (نتيجة الفحص الآلي).

2) **مخاطر تسرب متعدد الشركات بسبب غياب أدلة FK المركّبة مع CompanyId**
- الدليل: جميع الجداول تقريبًا تحتوي `CompanyId` (مثل `Users`, `Employees`, `RolePermissions`) لكن قسم القيود المعروض يحوي `CHECK` وفهارس فقط دون إظهار `FOREIGN KEY` يربط (`CompanyId`,`ParentId`) بين الجداول.  
- الأثر: إمكانية ربط سجل تابع لشركة A بأب من شركة B إذا لم تُفرض FK مركّبة.  
- المرجع: (المخطط: أعمدة CompanyId في الجداول)، (القيود/الفهارس: لا توجد أوامر FK معروضة).

3) **نقص فهارس استعلامات ثقيلة (حضور يومي/بصمات/رواتب)**
- الدليل:
  - الحضور اليومي: الفهرس الحالي فريد على `(CompanyId, EmployeeId, WorkDate, ShiftId)` فقط.  
    المرجع: (القيود/الفهارس: `UX_DailyAttendance_EmployeeId_WorkDate_ShiftId`)، (مسار API: `GET /attendance/daily`).
  - البصمات: لا يوجد فهرس زمني مباشر على `(CompanyId, PunchTime)` أو `(CompanyId, EmployeeId, PunchTime)`؛ الموجود فهرس تكرار جهاز/بصمة/وقت.  
    المرجع: (القيود/الفهارس: `UX_PunchLogs_DeviceId_FingerprintNo_PunchTime`)، (مسار API: `GET /punches`).
  - الرواتب: لا يظهر فهرس فترة على `VariableItems(PeriodYear,PeriodMonth,EmployeeId)` رغم استعمالها في حساب التشغيل الشهري.  
    المرجع: (المخطط: `VariableItems.PeriodYear/PeriodMonth/EmployeeId`)، (مسار API: `POST /payroll-runs/{id}/calculate`).

4) **تعارض محتمل بين استخدام IsDeleted في الفهارس وعدم ظهوره في تعريف الأعمدة**
- الدليل: جميع الفهارس المفلترة تقريبًا تعتمد `WHERE [IsDeleted] = 0` بينما تعريف الأعمدة المعروض لا يتضمن `IsDeleted`.  
- الأثر: إذا لم يكن العمود موجودًا فعليًا فالفهارس/المنطق غير قابلين للتنفيذ.  
- المرجع: (القيود/الفهارس: جميع `UX_* ... WHERE [IsDeleted]=0`)، (المخطط: تعريف الأعمدة المعروض).

---

## عيوب مهمة

1) **التطبيع: تخزين حقول اسم مُشتقة مع مكونات الاسم**
- الدليل: `Employees` يحتوي `FirstName*`, `MiddleName*`, `LastName*` بالإضافة إلى `FullNameAr/FullNameEn`.  
- الأثر: ازدواجية وتعارض تحديثات (Update anomalies).  
- المرجع: (المخطط: `Employees.FirstNameAr...FullNameAr...FullNameEn`).

2) **قيود عمل مفقودة على تواريخ/مدى**
- أمثلة:
  - لا يظهر قيد `FromDate <= ToDate` في `Leaves`.  
    المرجع: (المخطط: `Leaves.FromDate`, `Leaves.ToDate`)، (القيود: لا يوجد CK مقابل ذلك).
  - لا يظهر قيد `StartDate <= EndDate` في `FiscalYears`.  
    المرجع: (المخطط: `FiscalYears.StartDate`, `FiscalYears.EndDate`).
  - لا يظهر تحقق تماسك `Shifts.WorkMinutes` مع الوقت والراحة.  
    المرجع: (المخطط: `Shifts.StartTime/EndTime/BreakMinutes/WorkMinutes`).

3) **التزامن: غياب RowVersion/ETag**
- الدليل: لا يظهر أي عمود `rowversion/timestamp` في الجداول ذات تحديث كثيف (`DailyAttendance`, `PayrollRuns`, `Requests`...).  
- الأثر: Lost updates في عمليات UI/API المتزامنة.  
- المرجع: (المخطط: الجداول المذكورة بلا rowversion)، (مسارات API: `PUT/PATCH` متعددة).

4) **التشفير/إخفاء البيانات الحساسة غير مثبت على مستوى القاعدة**
- الدليل: أعمدة حساسة نصية صريحة (`Employees.IdentityNo`, `PassportNo`, `BankAccount`, `Users.TwoFactorSecret`).  
- الأثر: خطر كشف بيانات عند وصول مباشر لقاعدة البيانات ما لم يوجد تشفير طبقي خارجي [افتراض].  
- المرجع: (المخطط: الأعمدة المذكورة).

---

## تحسينات

1) **تقييد نطاق القيم الرقمية**
- أمثلة: نسب مثل `SocialCompanyPct`, `SocialEmployeePct` دون CK لنطاق 0..100.  
- المرجع: (المخطط: `Employees.SocialCompanyPct/SocialEmployeePct`)، (القيود: لا CK ظاهر).

2) **تحسين توحيد الصلاحيات مع المسارات**
- ملاحظة: توجد صلاحيات من نمط `export/approve/process` في المصفوفة لا يظهر لها مسارات صريحة في API المعروض.  
- المرجع: (المصفوفة: رموز E/A/P)، (مسارات API: لا توجد مسارات صريحة مقابلة لبعض الموارد).

3) **توثيق صريح لسياسة الحقول الحساسة في OpenAPI**
- الدليل: المصفوفة تنص على إرجاع null للحساسة بدون صلاحية؛ يلزم عكس ذلك في schemas/responses.  
- المرجع: (المصفوفة: ملاحظات التنفيذ الخاصة `employees.government.read` و`employees.financial.read`).

---

## 2) سكربت `06_fixes.sql` (Idempotent)

> **مهم**: الصلاحيات الأربع أدناه محددة كـ[افتراض] لغياب ملف `02_seed.sql`/نتيجة المقارنة التفصيلية. اخترتها لأنها تتوافق مع نمط “تُمنح لـ sysadmin وpayroll” في المصفوفة.

```sql
/* 06_fixes.sql
   Post: 01_schema.sql + 02_seed.sql
   هدف: إصلاح صلاحيات مفقودة [افتراض] + إضافة فهارس حرجة ناقصة
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

------------------------------------------------------------
-- A) Permissions missing in seed [افتراض]
------------------------------------------------------------
DECLARE @MissingPerms TABLE
(
    PermCode      NVARCHAR(150) PRIMARY KEY,
    [Module]      NVARCHAR(50)  NOT NULL,
    DescriptionAr NVARCHAR(300) NULL,
    DescriptionEn NVARCHAR(300) NULL
);

/* [افتراض] الصلاحيات الأربع المفقودة */
INSERT INTO @MissingPerms (PermCode, [Module], DescriptionAr, DescriptionEn)
VALUES
(N'finance.payroll_runs.approve',     N'finance', N'اعتماد كشف الرواتب',          N'Approve payroll run'),
(N'finance.payroll_runs.process',     N'finance', N'تنفيذ/ترحيل كشف الرواتب',      N'Process payroll run'),
(N'finance.social_insurance.approve', N'finance', N'اعتماد كشف التأمينات',         N'Approve social insurance run'),
(N'finance.end_of_service.approve',   N'finance', N'اعتماد سند نهاية الخدمة',       N'Approve end of service');

-- Insert into Permissions idempotent
INSERT INTO hr.Permissions (PermCode, [Module], DescriptionAr, DescriptionEn)
SELECT mp.PermCode, mp.[Module], mp.DescriptionAr, mp.DescriptionEn
FROM @MissingPerms mp
WHERE NOT EXISTS
(
    SELECT 1
    FROM hr.Permissions p
    WHERE p.PermCode = mp.PermCode
      AND (COL_LENGTH('hr.Permissions','IsDeleted') IS NULL OR p.IsDeleted = 0)
);

------------------------------------------------------------
-- B) Grant missing perms to sysadmin + payroll (per company)
------------------------------------------------------------
;WITH RoleTarget AS
(
    SELECT r.Id AS RoleId, r.CompanyId, r.NameEn
    FROM hr.Roles r
    WHERE (COL_LENGTH('hr.Roles','IsDeleted') IS NULL OR r.IsDeleted = 0)
      AND r.NameEn IN (N'sysadmin', N'payroll')
),
PermTarget AS
(
    SELECT p.Id AS PermissionId, p.PermCode
    FROM hr.Permissions p
    WHERE p.PermCode IN (SELECT PermCode FROM @MissingPerms)
      AND (COL_LENGTH('hr.Permissions','IsDeleted') IS NULL OR p.IsDeleted = 0)
)
INSERT INTO hr.RolePermissions (CompanyId, RoleId, PermissionId)
SELECT rt.CompanyId, rt.RoleId, pt.PermissionId
FROM RoleTarget rt
CROSS JOIN PermTarget pt
WHERE NOT EXISTS
(
    SELECT 1
    FROM hr.RolePermissions rp
    WHERE rp.CompanyId = rt.CompanyId
      AND rp.RoleId = rt.RoleId
      AND rp.PermissionId = pt.PermissionId
      AND (COL_LENGTH('hr.RolePermissions','IsDeleted') IS NULL OR rp.IsDeleted = 0)
);

------------------------------------------------------------
-- C) Critical missing indexes for heavy queries
------------------------------------------------------------

/* 1) Daily attendance list by date range */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DailyAttendance_Company_WorkDate_Employee'
      AND object_id = OBJECT_ID(N'hr.DailyAttendance')
)
BEGIN
    IF COL_LENGTH('hr.DailyAttendance','IsDeleted') IS NOT NULL
        CREATE INDEX IX_DailyAttendance_Company_WorkDate_Employee
        ON hr.DailyAttendance (CompanyId, WorkDate, EmployeeId)
        INCLUDE (ShiftId, [Status], InTime, OutTime, WorkMinutes, LateMin, EarlyLeaveMin, OvertimeMin, AbsentMin, ExcusedMin, MonthlyId, IsManualOverride)
        WHERE IsDeleted = 0;
    ELSE
        CREATE INDEX IX_DailyAttendance_Company_WorkDate_Employee
        ON hr.DailyAttendance (CompanyId, WorkDate, EmployeeId)
        INCLUDE (ShiftId, [Status], InTime, OutTime, WorkMinutes, LateMin, EarlyLeaveMin, OvertimeMin, AbsentMin, ExcusedMin, MonthlyId, IsManualOverride);
END;

/* 2) Punch logs by employee/time (timeline & reconciliation) */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PunchLogs_Company_Employee_PunchTime'
      AND object_id = OBJECT_ID(N'hr.PunchLogs')
)
BEGIN
    IF COL_LENGTH('hr.PunchLogs','IsDeleted') IS NOT NULL
        CREATE INDEX IX_PunchLogs_Company_Employee_PunchTime
        ON hr.PunchLogs (CompanyId, EmployeeId, PunchTime)
        INCLUDE ([Status], Direction, [Source], DeviceId, FingerprintNo, GeoLocationId, IsOffline, IsMock, DistanceM)
        WHERE IsDeleted = 0 AND EmployeeId IS NOT NULL;
    ELSE
        CREATE INDEX IX_PunchLogs_Company_Employee_PunchTime
        ON hr.PunchLogs (CompanyId, EmployeeId, PunchTime)
        INCLUDE ([Status], Direction, [Source], DeviceId, FingerprintNo, GeoLocationId, IsOffline, IsMock, DistanceM);
END;

/* 3) Punch logs by company/time (global review screens) */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PunchLogs_Company_PunchTime'
      AND object_id = OBJECT_ID(N'hr.PunchLogs')
)
BEGIN
    IF COL_LENGTH('hr.PunchLogs','IsDeleted') IS NOT NULL
        CREATE INDEX IX_PunchLogs_Company_PunchTime
        ON hr.PunchLogs (CompanyId, PunchTime)
        INCLUDE (EmployeeId, DeviceId, FingerprintNo, [Status], Direction, [Source])
        WHERE IsDeleted = 0;
    ELSE
        CREATE INDEX IX_PunchLogs_Company_PunchTime
        ON hr.PunchLogs (CompanyId, PunchTime)
        INCLUDE (EmployeeId, DeviceId, FingerprintNo, [Status], Direction, [Source]);
END;

/* 4) Variable items by payroll period */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_VariableItems_Company_Period_Employee'
      AND object_id = OBJECT_ID(N'hr.VariableItems')
)
BEGIN
    IF COL_LENGTH('hr.VariableItems','IsDeleted') IS NOT NULL
        CREATE INDEX IX_VariableItems_Company_Period_Employee
        ON hr.VariableItems (CompanyId, PeriodYear, PeriodMonth, EmployeeId)
        INCLUDE (PayrollItemId, Amount, Percentage, IsApplied, PayrollRunId)
        WHERE IsDeleted = 0;
    ELSE
        CREATE INDEX IX_VariableItems_Company_Period_Employee
        ON hr.VariableItems (CompanyId, PeriodYear, PeriodMonth, EmployeeId)
        INCLUDE (PayrollItemId, Amount, Percentage, IsApplied, PayrollRunId);
END;

COMMIT TRAN;
GO
```

---

## 3) تعديلات OpenAPI مقترحة لربط صلاحيات غير مستخدمة (24)

> [افتراض] القائمة مبنية على مقارنة **المصفوفة** مع **مسارات API المعروضة نصيًا** (وليس `03_openapi.yaml` غير المتاح).

1. `GET /employees/export` → `employees.employees.export`  
2. `GET /employees/{id}/government` → `employees.government.read`  
3. `PUT /employees/{id}/government` → `employees.government.update`  
4. `GET /attendance/daily/export` → `attendance.daily.export`  
5. `GET /attendance/monthly/export` → `attendance.monthly.export`  
6. `POST /attendance/excuses/{id}/approve` → `attendance.excuses.approve`  
7. `POST /attendance/punches/process` → `attendance.punches.process`  
8. `GET /leaves/export` → `leaves.leaves.export`  
9. `POST /leaves/{id}/approve` → `leaves.leaves.approve`  
10. `GET /advances/export` → `finance.advances.export`  
11. `GET /payroll-runs/export` → `finance.payroll_runs.export`  
12. `PUT /payroll-runs/{id}` → `finance.payroll_runs.update`  
13. `POST /social-insurance-runs/{id}/approve` → `finance.social_insurance.approve`  
14. `GET /social-insurance-runs/export` → `finance.social_insurance.export`  
15. `POST /end-of-service/{id}/approve` → `finance.end_of_service.approve`  
16. `GET /end-of-service/export` → `finance.end_of_service.export`  
17. `GET /audit-logs/export` → `admin.audit_logs.export`  
18. `GET /reports/{key}/export` أو ربط `format!=json` بـ `reports.reports.export`  
19. `POST /variable-items/{id}/approve` → `finance.variable_items.approve`  
20. `GET /fiscal-years/export` → `admin.fiscal_years.export` [إن كانت مزروعة]  
21. `GET /users/export` → `admin.users.export` [إن كانت مزروعة]  
22. `GET /punches/export` → `attendance.punches.export`  
23. `GET /requests/export` → `requests.requests.export` [إن كانت مزروعة]  
24. `GET /employees/{id}/salary-items/export` → `employees.financial.export` [إن كانت مزروعة]

---

إذا رغبت، أقدّم لك نسخة **Diff جاهزة** لـ OpenAPI (`paths + security + x-permission`) بدل القائمة الوصفية.