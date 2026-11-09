<!-- agent D_arch model gpt-5.2 -->
فيما يلي مواصفة بديلة **[مستنتج]** اعتماداً حصراً على: (المخطط: جداول/أعمدة وقيود CHECK والفهارس)، و(مسارات الـAPI)، و(ملخص الدليل p-xx). كل ما لا يسنده ذلك موسوم [افتراض].

---

## 1) هيكل الحل (Clean Architecture) — ASP.NET Core 8 + EF Core + SQL Server **[مستنتج]**

### 1.1 تقسيم طبقات/مشاريع الحل (Solution) **[مستنتج]**
- **Hrms.Api** (طبقة العرض/الواجهة Web API)
  - مسؤوليات: تعريف Controllers/Endpoints لكل المسارات المذكورة في الـAPI (مثل: `/auth/login`, `/attendance/process`, `/payroll-runs/{id}/calculate`) (مسارات API).
  - يطبق: المصادقة/التفويض والتحقق من `x-permission` (مسارات API + جداول Permissions/RolePermissions/UserRoles/UserGroupRoles).
  - يعتمد على: Hrms.Application فقط [افتراض].
- **Hrms.Application** (حالات الاستخدام Use Cases)
  - مسؤوليات: Services/Handlers لتنفيذ عمليات مثل: إغلاق سنة مالية `POST /fiscal-years/{id}/close`، احتساب حضور `POST /attendance/process`، إقفال شهر `POST /attendance/monthly/{id}/close`، احتساب كشف راتب `POST /payroll-runs/{id}/calculate` (مسارات API).
  - يحتوي: عقود Interfaces للمستودعات ووحدات خارجية (IUnitOfWork/Repositories, IClock, IEncryption) [افتراض].
  - يعتمد على: Hrms.Domain فقط [افتراض].
- **Hrms.Domain** (النواة)
  - مسؤوليات: الكيانات وقواعد العمل النقية (Entities/ValueObjects/Domain Services).
  - يتضمن محرك الحضور اليومي كدالة نقية (انظر القسم 2) [مستنتج من وجود `/shifts/simulate` و`DailyAttendance` و`Shifts` و`PunchLogs`].
  - لا يعتمد على أي طبقة أخرى [افتراض].
- **Hrms.Infrastructure** (البنية التحتية)
  - مسؤوليات: EF Core DbContext، الترحيلات، تنفيذ المستودعات، تكامل SQL Server، تشفير الحقول المذكورة في README (README: “حقول ... تُخزَّن مشفّرة من طبقة التطبيق”).
  - يعتمد على: Hrms.Application وHrms.Domain [افتراض].
  - يضم: تنفيذ رفع المرفقات `Attachments.StoragePath` (جدول Attachments/عمود StoragePath) [افتراض].
- **Hrms.Contracts** (DTOs/Requests/Responses أو OpenAPI-generated models) [افتراض]
  - الهدف: تثبيت عقود الإدخال/الإخراج المتوافقة مع `03_openapi.yaml` (README).
- **Hrms.Tests** (اختبارات)
  - تشمل: اختبارات أمثلة عددية للحضور والرواتب (README: “سبب وجود اختبارات الأمثلة العددية”) + وجود `/shifts/simulate` (مسار API).

> ملاحظة: أسماء المشاريع أعلاه [افتراض]؛ أما **الحدود الطبقية والتبعيات** فهي مستنتجة من “Clean Architecture” المطلوب ومن طبيعة المسارات/الجداول (مسارات API + جداول/أعمدة).

### 1.2 حدود الوحدات (Modules) داخل Domain/Application **[مستنتج]**
استناداً إلى تجميع المسارات والموارد:
1) **Admin**: شركات/سنوات مالية/مستخدمين/مجموعات/أدوار/إعدادات/سجلات تدقيق/نسخ احتياطي/ترويسات تقارير  
(مسارات: `/companies`, `/fiscal-years`, `/users`, `/user-groups`, `/roles`, `/settings`, `/audit-logs`, `/backups`, `/report-headers`).
2) **Definitions (Defs)**: الهيكل، الدرجات، الوظائف، مراكز التكلفة، …  
(مسارات: `/org-nodes`, `/job-grades`, `/jobs`, `/cost-centers`, `/employee-groups`, `/projects`, `/banks`, `/countries`, `/insurance-companies`, `/document-types`, `/sponsors`, `/nationalities`, `/work-natures`, `/lookups`, `/geo-locations`).
3) **Employees**: ملف الموظف وتوابعه (الوثائق/الأرشيف/المرافقين/المؤهلات/الخبرات/بنود الراتب/حساب البوابة)  
(مسارات: `/employees/...` + جداول Employees وEmployeeDocuments وEmployeeArchive…).
4) **Attendance**: الشفتات، الربط، الأجهزة، البصمات، العطل، الاستئذان، اليومي/الشهري  
(مسارات: `/shifts`, `/shift-assignments`, `/biometric-devices`, `/punches`, `/holidays`, `/excuses`, `/attendance/*` + جداول Shifts/ShiftDays/EmployeeShifts/PunchLogs/OfficialHolidays/Excuses/DailyAttendance/MonthlyAttendances…).
5) **Leaves**: أنواع الإجازات، الأرصدة، الإجازات، الدفتر  
(مسارات: `/leave-types`, `/leave-balances`, `/leaves` + جداول LeaveTypes/LeaveBalances/LeaveLedger/Leaves).
6) **Requests/Approvals**: التدفقات والطلبات والخطوات والتعليقات والتفويض  
(مسارات: `/requests`, `/approval-flows`, `/delegations` + جداول Requests/ApprovalFlows/RequestSteps…).
7) **Finance/Payroll**: البنود، المتغيرات، السلف/الأقساط، العهد، كشوف الرواتب، التأمينات، نهاية الخدمة  
(مسارات: `/payroll-items`, `/variable-items`, `/advances`, `/custodies`, `/payroll-runs`, `/social-insurance-runs`, `/end-of-service` + جداول PayrollRuns/PayrollRunLines/PayrollRunLineItems/Advances/AdvanceInstallments…).
8) **Reports**: تشغيل التقارير وعروض التقارير  
(مسارات: `/reports/{key}`, `/report-views` + جداول ReportHeaders/ReportViews).
9) **Mobile/Me**: الحضور عبر الجوال، ملفي، طلباتي…  
(مسارات: `/mobile/*`, `/devices/register`, `/me/*` + جداول UserDevices/EmployeePortalAccounts/PunchLogs).
10) **Tasks/Notifications/Dashboard**  
(مسارات: `/tasks`, `/notifications`, `/dashboard/*` + جداول Tasks/Notifications).

### 1.3 التبعيات بين الوحدات (Module Dependencies) **[مستنتج]**
- Attendance يعتمد على:
  - Employees (لتحديد `EmployeeId` وخصائص مثل `OvertimeEnabled`, `DayRate`, `HourRate`) (Employees جدول/أعمدة).
  - Leaves (لتحديد `Leaves` المؤثرة على `DailyAttendance.Status='leave'` و`LeaveId`) (DailyAttendance أعمدة LeaveId/Status + جدول Leaves).
- Payroll يعتمد على:
  - Employees (الأجور وخصائص `MonthDays`, `DayRate`, `HourRate`, `SocialCompanyPct`, `SocialEmployeePct`) (Employees أعمدة).
  - Attendance (مصدر `WorkDays/AbsentDays/LateMin...` بعد إقفال الشهر “feeds payroll”) (مسار: `POST /attendance/monthly/{id}/close` وصفه + جداول MonthlyAttendanceLines).
  - Advances/Installments (الاعتماد/العكس في اعتماد كشف الراتب) (مسارات: `/payroll-runs/{id}/approve`, `/payroll-runs/{id}/unapprove` + جداول AdvanceInstallments/PayrollRunLineItems).
- Requests/Approvals تتقاطع مع:
  - Leaves/Excuses/Advances/Custodies… لأن `Requests.RequestType` يشمل (`leave`,`advance`,`custody`,`excuse`,`punch_fix`,`other`) (قيد CK_Requests_RequestType).

---

## 2) محرك احتساب الحضور اليومي كدالة نقية **[مستنتج]**

### 2.1 هدف الدالة **[مستنتج]**
إنتاج سجل/نتيجة تتطابق مع أعمدة جدول `DailyAttendance` (جدول DailyAttendance/الأعمدة) لكل (EmployeeId, WorkDate, ShiftId) مع الالتزام بحالات `Status` المقيدة (قيد CK_DailyAttendance_Status).

### 2.2 توقيع الدالة (Pure Function Signature) **[مستنتج]**
```text
ComputeDailyAttendance(
  workDate: date,
  employeeId: bigint,
  shift: Shifts row,
  shiftDay: ShiftDays row (matching DayOfWeek) [افتراض آلية المطابقة],
  punches: list of PunchLogs rows for employee within window,
  holidays: list of OfficialHolidays rows applicable to workDate,
  leaves: list of Leaves rows overlapping workDate,
  excuses: list of Excuses rows for workDate (and optionally ExcuseShifts),
  companyTimeZone: Companies.TimeZone,
  rules: derived from shift columns (grace/windows/overtime/punch mode)
) -> DailyAttendanceResult (columns aligned to DailyAttendance)
```
- مصادر الأعمدة:
  - `Shifts` (StartTime, EndTime, WorkMinutes, CrossesMidnight, LateGrace*, EarlyGrace*, BeforeInMin/AfterInMin, BeforeOutMin/AfterOutMin, PunchMode, PresentIfNoOut/PresentIfNoIn, OvertimeEnabled, OvertimeFrom/OvertimeTo) (جدول Shifts/أعمدة).
  - `ShiftDays` (IsWorkDay, StartTime/EndTime override) (جدول ShiftDays/أعمدة).
  - `PunchLogs` (PunchTime, LocalTime, Direction, Status) (جدول PunchLogs/أعمدة + قيود CK_PunchLogs_*).
  - `OfficialHolidays` (SpecificStartDate/DayOfMonth/MonthOfYear/DurationDays/OrgNodeId) (جدول OfficialHolidays/أعمدة).
  - `Leaves` (FromDate/ToDate/IsHalfDay/Status) (جدول Leaves/أعمدة + قيد CK_Leaves_Status).
  - `Excuses` (ExcuseDate/FromTime/ToTime/IsApproved) (جدول Excuses/أعمدة).

> تحديد “نافذة استخراج البصمات” [افتراض]؛ المخطط لا يحددها، لكن وجود BeforeInMin/AfterInMin/BeforeOutMin/AfterOutMin في Shifts يدل على نافذة قبول حول بداية/نهاية الشفت (جدول Shifts/أعمدة).

### 2.3 مخرجات الدالة (مطابقة أعمدة `DailyAttendance`) **[مستنتج]**
ينتج كائن يحتوي حرفياً:
- `CompanyId` [افتراض تمريرها خارجياً لأن الدالة تعمل ضمن شركة].
- `EmployeeId`, `WorkDate`, `ShiftId`
- `InTime`, `OutTime` (datetimeoffset(0))
- `RequiredMinutes` (int)
- `WorkMinutes` (int)
- `LateMin` (int)
- `EarlyLeaveMin` (int)
- `AbsentMin` (int)
- `OvertimeMin` (int)
- `ExcusedMin` (int)
- `Status` (nvarchar(20)) ∈ (`present`,`absent`,`leave`,`holiday`,`holiday_work`,`weekend`,`excused`) (قيد CK_DailyAttendance_Status)
- `LeaveId`, `ExcuseId`, `HolidayId`
- `IsManualOverride`=0 و`OverrideReason`=NULL [افتراض لأن override يتم عبر API منفصل]
- `ComputedAt` [افتراض يُضبط خارج الدالة]
- `MonthlyId` [افتراض يُربط لاحقاً عند إنشاء/إقفال الشهر]

### 2.4 قواعد اشتقاق `RequiredMinutes` ووقت البداية/النهاية **[مستنتج]**
1) إذا كان لـ `ShiftDays.StartTime/EndTime` قيمة، فهي **تتجاوز** `Shifts.StartTime/EndTime` [افتراض] (وجود أعمدة StartTime/EndTime في ShiftDays).
2) `RequiredMinutes`:
   - إذا `ShiftDays.IsWorkDay=0` ⇒ `RequiredMinutes=0` و`Status='weekend'` ما لم يوجد عمل فعلي بالبصمات [افتراض، لكن الحالة weekend موجودة في القيد].
   - إذا `IsWorkDay=1` ⇒ `RequiredMinutes = Shifts.WorkMinutes` (جدول Shifts.WorkMinutes).

### 2.5 جدول الحالات (≥ 20 حالة) بأمثلة رقمية **[مستنتج]**
الإعداد القياسي للأمثلة (مشترك ما لم يُذكر غير ذلك) **[افتراض]**:
- شفت نهاري: `StartTime=08:00`, `EndTime=16:00`, `WorkMinutes=480`, `BreakMinutes=0`.
- السماح: `LateGraceEnabled=1`, `LateGraceMin=10`, `EarlyGraceEnabled=1`, `EarlyGraceMin=10`.
- النوافذ: `BeforeInMin=60`, `AfterInMin=180`, `BeforeOutMin=180`, `AfterOutMin=60` [افتراض].
- `PunchMode='inout'` (قيد CK_Shifts_PunchMode).
- جميع بصمات `PunchLogs.Status='accepted'` (قيد CK_PunchLogs_Status).

> الحسابات بالـدقائق:  
LateMin = max(0, InTime - StartTime - (LateGraceMin إن كان LateGraceEnabled=1)) [افتراض]  
EarlyLeaveMin = max(0, EndTime - OutTime - (EarlyGraceMin إن كان EarlyGraceEnabled=1)) [افتراض]  
WorkMinutes = max(0, min(OutTime, EndTime) - max(InTime, StartTime)) بالدقائق، مع عدم تجاوز RequiredMinutes [افتراض].  
AbsentMin = RequiredMinutes - WorkMinutes - ExcusedMin، على ألا يقل عن 0 [افتراض].  
الـStatus يُحدد وفق الحالات أدناه مستنداً إلى نطاق القيم وأولوية (إجازة/عطلة/راحة/حضور) [افتراض].

| # | الحالة | مثال (In/Out) | النتائج الأساسية (Required/Work/Late/Early/Absent/Excused/OT) | Status | تعليل الدليل |
|---|---|---|---|---|---|
| 1 | حضور كامل | 08:00 / 16:00 | 480 / 480 /0/0/0/0/0 | present | CK_DailyAttendance_Status + أعمدة DailyAttendance |
| 2 | تأخير ضمن السماح | 08:07 / 16:00 | 480 / 473 /0/0/7/0/0 [افتراض احتساب الغياب الجزئي] | present | وجود LateGrace* (Shifts) |
| 3 | تأخير خارج السماح | 08:20 / 16:00 | 480 / 460 /10/0/20/0/0 [افتراض] | present | LateGraceMin (Shifts) |
| 4 | خروج مبكر ضمن السماح | 08:00 / 15:55 | 480 / 475 /0/0/5/0/0 [افتراض] | present | EarlyGrace* (Shifts) |
| 5 | خروج مبكر خارج السماح | 08:00 / 15:30 | 480 / 450 /0/20/30/0/0 [افتراض] | present | EarlyGraceMin (Shifts) |
| 6 | تأخير + خروج مبكر | 08:25 / 15:20 | 480 / 415 /15/30/65/0/0 [افتراض] | present | أعمدة LateMin/EarlyLeaveMin/AbsentMin |
| 7 | غياب كامل (لا بصمات) | — | 480 / 0 /0/0/480/0/0 | absent | Status=absent (قيد) |
| 8 | بصمة دخول واحدة فقط (PresentIfNoOut=0) | 08:00 / — | 480 /0/0/0/480/0/0 [افتراض] | absent [افتراض] | وجود PresentIfNoOut (Shifts) |
| 9 | بصمة دخول واحدة فقط (PresentIfNoOut=1) | 08:00 / — | 480 /480/0/0/0/0/0 [افتراض] | present | Shifts.PresentIfNoOut |
|10| بصمة خروج واحدة فقط (PresentIfNoIn=0) | — / 16:00 | 480 /0/0/0/480/0/0 [افتراض] | absent [افتراض] | Shifts.PresentIfNoIn |
|11| بصمة خروج واحدة فقط (PresentIfNoIn=1) | — / 16:00 | 480 /480/0/0/0/0/0 [افتراض] | present | Shifts.PresentIfNoIn |
|12| بصمات مكررة (اختيار أول In وآخر Out) | 07:58,08:01,16:00,16:03 | 480/480/0/0/0/0/0 (اعتماد 08:01 و16:00) [افتراض] | present | PunchMode inout + تعدد PunchLogs |
|13| بصمات خارج نافذة القبول (تجاهل) | 03:00 / 23:00 | 480/0/0/0/480/0/0 [افتراض] | absent | وجود Before*/After* (Shifts) يوحي بالترشيح |
|14| شفت ليلي يعبر منتصف الليل | Start 22:00 End 06:00 CrossesMidnight=1 | In=22:05(اليوم) Out=06:00(اليوم التالي) ⇒ Required=480 Work≈475 Late=0(ضمن grace) Absent=5 [افتراض] | present | Shifts.CrossesMidnight |
|15| شفت ليلي وبصمة Out بعد نهاية النافذة | In 22:00 Out 08:00 | Work=480 + OT=120 [افتراض] | present | OvertimeEnabled/OvertimeFrom/OvertimeTo (Shifts) |
|16| عطلة رسمية بدون عمل | — وHoliday ينطبق | Required=0 Work=0 | holiday | Status=holiday (قيد) + جدول OfficialHolidays |
|17| عطلة رسمية مع بصمات (عمل في عطلة) | 08:00/12:00 في يوم Holiday | Required=0 Work=240 OT=240 [افتراض] | holiday_work | Status=holiday_work (قيد) |
|18| راحة أسبوعية بدون عمل (IsWorkDay=0) | — | Required=0 Work=0 | weekend | ShiftDays.IsWorkDay + Status=weekend (قيد) |
|19| راحة أسبوعية مع عمل | 09:00/13:00 وIsWorkDay=0 | Required=0 Work=240 OT=240 [افتراض] | present [افتراض] أو holiday_work غير ممكن هنا | CK_DailyAttendance_Status لا يحوي weekend_work |
|20| إجازة معتمدة تغطي اليوم | Leave.Status='approved' FromDate<=WorkDate<=ToDate | Required=0 Work=0 LeaveId=... | leave | جدول Leaves + Status=leave |
|21| إجازة نصف يوم | Leaves.IsHalfDay=1 | Required=480 Work=240 Absent=240 أو LeaveDays في الشهري [افتراض] | leave [افتراض] أو present؟ | وجود IsHalfDay (Leaves) لكن DailyAttendance لا يحوي نصف يوم صراحة |
|22| استئذان (Excuse) معتمد يغطي 2 ساعة | In 10:00 Out 16:00 وExcuse 08:00-10:00 | Required=480 Work=360 Excused=120 Absent=0 Late=0 | excused | Status=excused (قيد) + جدول Excuses.FromTime/ToTime |
|23| استئذان جزئي داخل الدوام مع تأخير | In 10:15 Out 16:00 وExcuse 10:00-11:00 | Late بعد السماح =؟ (10:15-08:00-10=125) لكن Excused=60 ⇒ LateMin=65 [افتراض فصل التأخير عن الاستئذان] | excused [افتراض] | أعمدة LateMin وExcusedMin موجودة معاً |
|24| PunchMode='inonly' | In 08:00,08:05 فقط | OutTime=NULL وWorkMinutes وفق PresentIfNoOut [افتراض] | present/absent حسب flags | Shifts.PunchMode + PresentIfNoOut |
|25| PunchMode='outonly' | Out 16:00 فقط | InTime=NULL وWorkMinutes وفق PresentIfNoIn [افتراض] | present/absent حسب flags | Shifts.PunchMode + PresentIfNoIn |
|26| PunchMode='any' | 08:00,16:00 بلا Direction | يختار الأقدم In والأحدث Out [افتراض] | present | PunchLogs.Direction يسمح unknown (قيد) + PunchMode any |
|27| PunchLogs.Status!='accepted' | بصمة rejected | تُستبعد من الاحتساب [افتراض] | حسب الباقي | قيد CK_PunchLogs_Status يميز accepted/pending/... |

> ملاحظة ضبط الأولويات (Leave vs Holiday vs Weekend vs Excuse vs Present) **[افتراض]** لأن المخطط لا يصرّح بالأولوية، لكنه يتيح ربط `LeaveId/ExcuseId/HolidayId` في نفس السجل (DailyAttendance أعمدة).

---

## 3) دورة حالة كشف الراتب ومعادلات + مثال YER **[مستنتج]**

### 3.1 دورة الحالة (PayrollRuns.Status) وربطها بالـAPI **[مستنتج]**
- الحالات المسموحة: `('draft','review','approved','paid')` (قيد CK_PayrollRuns_Status).
- الانتقالات المستنتجة من المسارات:
  1) **draft → review** عند `POST /payroll-runs/{id}/calculate` (مسار API: “Calculate (draft only)”).
  2) **review → approved** عند `POST /payroll-runs/{id}/approve` (مسار API).
  3) **approved → review أو draft** عند `POST /payroll-runs/{id}/unapprove` (مسار API: “Reverse approval”) [افتراض الحالة المستهدفة review].
  4) **approved → paid** عند `POST /payroll-runs/{id}/pay` (مسار API).
- “posted/reversed” المذكورة في السؤال تُقابل عملياً:
  - posted = أثر اعتماد الكشف على أقساط السلف: “posts advance installments” (مسار API `/payroll-runs/{id}/approve` + جدول AdvanceInstallments.Status).
  - reversed = عكس ذلك: “reverses installments” (مسار API `/payroll-runs/{id}/unapprove`)  
  (لا يوجد عمود Posted/Journal في المخطط، لذا التسمية المحاسبية [افتراض]).

### 3.2 معادلات الرواتب (اعتماداً على أعمدة الجداول) **[مستنتج جزئياً]**
المخطط يوفّر:
- في `Employees`: `MonthDays`, `DayRate`, `HourRate`, `DailyHours`, `SocialCompanyPct`, `SocialEmployeePct` (Employees/أعمدة).
- في `PayrollRunLines`: `WorkDays`, `BasicSalary`, `TotalEarnings`, `TotalDeductions`, `NetPay` (PayrollRunLines/أعمدة).
- في `PayrollItems`: `ItemType`, `CalcMethod ('fixed','pct_basic','pct_gross')`, `IsBasic`, `SubjectToInsurance` (PayrollItems/أعمدة + قيود CK_PayrollItems_*).
- في `PayrollRunLineItems`: (PayrollItemId, ItemType, Amount, Source, SourceRef) (PayrollRunLineItems/أعمدة).

بالتالي المعادلات التالية **[مستنتج]** مع أجزاء [افتراض] حيث لا يوجد نص صريح:
1) **الأجر اليومي**:
   - إذا `Employees.DayRate > 0` ⇒ `DayWage = DayRate` (Employees.DayRate).
   - وإلا إذا `BasicSalary` متاح ⇒ `DayWage = BasicSalary / Employees.MonthDays` [افتراض] (Employees.MonthDays).
2) **الأجر بالساعة**:
   - إذا `Employees.HourRate > 0` ⇒ `HourWage = HourRate` (Employees.HourRate).
   - وإلا `HourWage = DayWage / Employees.DailyHours` [افتراض] (Employees.DailyHours).
3) **خصم الغياب**:
   - `AbsentDeduction = AbsentDays * DayWage` (MonthlyAttendanceLines.AbsentDays + Employees.DayRate/MonthDays) [افتراض مصدر AbsentDays من الشهر].
4) **خصم التأخير/الخروج المبكر بالدقائق**:
   - `LateDeduction = (LateMin / 60) * HourWage`
   - `EarlyDeduction = (EarlyLeaveMin / 60) * HourWage`
   (MonthlyAttendanceLines.LateMin/EarlyLeaveMin + HourWage) [افتراض].
5) **الإضافي** (إذا `Employees.OvertimeEnabled=1` و/أو `Shifts.OvertimeEnabled=1`) (Employees.OvertimeEnabled + Shifts.OvertimeEnabled):
   - `OvertimePay = (OvertimeMin / 60) * HourWage * OvertimeFactor` [افتراض معامل].
6) **الأقساط (Installments)**:
   - تؤخذ من `AdvanceInstallments` للـ(PeriodYear/PeriodMonth) وتُسجل كبند خصم في `PayrollRunLineItems` مع `Source='advance_installment'` [افتراض]، وتُحدّث `AdvanceInstallments.Status` إلى `deducted` عند اعتماد الكشف (مسار `/payroll-runs/{id}/approve` + قيد CK_AdvanceInstallments_Status).
7) **التأمينات الاجتماعية (حصة الموظف)**:
   - `EmployeeSocial = InsurableWage * (Employees.SocialEmployeePct/100)` (Employees.SocialEmployeePct + SocialInsuranceRunLines.EmployeeShare) [افتراض ربط الآلية].
   - حصة الشركة مماثلة باستخدام `SocialCompanyPct` (Employees.SocialCompanyPct).
8) **الصافي**:
   - `NetPay = TotalEarnings - TotalDeductions` (PayrollRunLines أعمدة NetPay/TotalEarnings/TotalDeductions) [مستنتج مباشر].

### 3.3 مثال رقمي كامل (YER) **[مستنتج مع افتراضات معلنة]**
المعطيات:
- `Companies.CurrencyCode='YER'` (Companies.CurrencyCode).
- موظف:
  - `MonthDays=30` (Employees.MonthDays)
  - `DailyHours=8` (Employees.DailyHours)
  - `DayRate=10,000` YER (Employees.DayRate)  ⇒ `HourWage=1,250` YER [افتراض: DayRate/DailyHours]
  - `SocialEmployeePct=5%` (Employees.SocialEmployeePct) [افتراض قيمة]
- ملخص شهر من `MonthlyAttendanceLines` [افتراض كيف يُنقل للرواتب]:
  - `WorkDays=28` (PayrollRunLines.WorkDays موجود لكن مصدره [افتراض])
  - `AbsentDays=1` (MonthlyAttendanceLines.AbsentDays)
  - `LateMin=60` (MonthlyAttendanceLines.LateMin)
  - `OvertimeMin=120` (MonthlyAttendanceLines.OvertimeMin)
- بنود:
  - BasicSalary (بند أساسي) = `DayRate * MonthDays = 300,000` YER [افتراض اشتقاق]
  - OvertimeFactor = 1.5 [افتراض]
  - قسط سلفة هذا الشهر = 20,000 YER (AdvanceInstallments.Amount) [افتراض قيمة]
  - InsurableWage = BasicSalary (SocialInsuranceRunLines.InsurableWage) [افتراض]

الحساب:
- **BasicSalary** = 300,000
- **AbsentDeduction** = 1 * 10,000 = 10,000
- **LateDeduction** = (60/60) * 1,250 = 1,250
- **OvertimePay** = (120/60) * 1,250 * 1.5 = 5,625
- **EmployeeSocial** = 300,000 * 5% = 15,000
- **InstallmentDeduction** = 20,000

إجماليات:
- `TotalEarnings = BasicSalary + OvertimePay = 305,625`
- `TotalDeductions = AbsentDeduction + LateDeduction + EmployeeSocial + InstallmentDeduction
  = 10,000 + 1,250 + 15,000 + 20,000 = 46,250`
- `NetPay = 305,625 - 46,250 = 259,375` YER  
(مطابقة حقول PayrollRunLines: TotalEarnings/TotalDeductions/NetPay).

> كل قيم المعاملات غير الموجودة في المخطط (OvertimeFactor، InsurableWage definition، طريقة اشتقاق BasicSalary) موسومة [افتراض]؛ أما أماكن التخزين فهي مثبتة من الجداول (PayrollRunLines/PayrollRunLineItems/SocialInsuranceRunLines/AdvanceInstallments).

---

## 4) خطة مراحل التنفيذ ومعايير قبول قابلة للاختبار **[مستنتج]**

### المرحلة 1: الأساسيات (Auth/Admin/Defs) **[مستنتج]**
**نطاق**
- مصادقة: `/auth/login`, `/auth/refresh`, `/auth/me`, `/auth/logout` (مسارات API) + جداول Users/RefreshTokens/Permissions/RolePermissions/UserRoles/UserGroupRoles/UserDataScopes.
- إدارة الشركات/السنة المالية/المستخدمين/الأدوار/الإعدادات (مسارات Admin).
- التعريفات العامة (Defs) (مسارات Defs).

**معايير قبول (اختبارات)**
1) إنشاء Company ثم استرجاعها وتعديلها وحذفها منطقياً عبر `/companies` (مسارات API + فهرس UX_Companies_Code المشروط على IsDeleted=0).
2) إنشاء FiscalYear ومن ثم إغلاقها عبر `/fiscal-years/{id}/close` وتصبح “locks transactions” (مسار API)؛ آلية المنع [افتراض] لكن يجب تسجيل أثر في `FiscalYears.IsClosed` (FiscalYears.IsClosed).
3) عند `/auth/me` تُعاد “permissions, data scope” (مسار API) ويطابق ذلك الجداول RolePermissions/UserDataScopes (جداول/أعمدة).

### المرحلة 2: الموظفون (Employees) **[مستنتج]**
**نطاق**
- CRUD للموظف وتوابعه: SalaryItems/Documents/Archive/Companions/Qualifications/Experiences + PortalAccount + Start-work (مسارات `/employees/...` + الجداول المقابلة).

**معايير قبول**
1) لا يمكن إدخال `Employees.Gender` خارج ('M','F') (قيد CK_Employees_Gender).
2) `/employees/{id}/start-work` ينشئ أرصدة إجازات (مسار API: “creates leave balances”) ويُتحقق بوجود صفوف `LeaveBalances` للموظف (جدول LeaveBalances).
3) `/employees/query` يسمح اختيار أعمدة (مسار API) ويجب أن تُرجع فقط أعمدة موجودة في `Employees` [افتراض اختبار].

### المرحلة 3: الحضور (Shifts/Punches/Daily/Monthly) **[مستنتج]**
**نطاق**
- Shifts/ShiftDays/EmployeeShifts/Devices/PunchLogs/Holidays/Excuses.
- محرك اليومي + simulate + process + monthly preview/close/reopen (مسارات Attendance).

**معايير قبول**
1) `/shifts` يدعم `PunchMode` ضمن القيم المقيدة (قيد CK_Shifts_PunchMode).
2) `/punches/batch` يحقق idempotency على `PunchLogs.ClientId` (فهرس UX_PunchLogs_ClientId).
3) `/attendance/process` ينتج صفوف `DailyAttendance` ملتزمة بقيد الحالة (CK_DailyAttendance_Status) وبفهرس التفرد `(EmployeeId,WorkDate,ShiftId)` (UX_DailyAttendance_EmployeeId_WorkDate_ShiftId).
4) `/attendance/monthly/{id}/close` يجعل `MonthlyAttendances.IsClosed=1` ويمنع `PATCH /attendance/daily/{id}` “blocked after month close” (مسار API + MonthlyAttendances.IsClosed).

### المرحلة 4: الإجازات + الطلبات/الاعتمادات **[مستنتج]**
**نطاق**
- LeaveTypes/Leaves/LeaveBalances/Ledger.
- Requests/ApprovalFlows/Steps/Delegations.

**معايير قبول**
1) حالات `Leaves.Status` لا تخرج عن (draft,pending,approved,rejected,cancelled) (قيد CK_Leaves_Status).
2) إنشاء Request من `/requests` أو `/me/requests` بقيمة `RequestType` ضمن القيم المقيدة (قيد CK_Requests_RequestType).
3) `/requests/{id}/approve` يغيّر `RequestSteps.Decision` ضمن القيم المقيدة (قيد CK_RequestSteps_Decision) [افتراض تفاصيل الانتقال].

### المرحلة 5: الرواتب والمالية (Payroll/Advances/Insurance) **[مستنتج]**
**نطاق**
- PayrollItems/VariableItems/Advances/Installments/Custodies/PayrollRuns/SocialInsuranceRuns/EndOfServices.

**معايير قبول**
1) `/payroll-runs/{id}/calculate` لا يعمل إلا إذا `PayrollRuns.Status='draft'` (مسار API: “draft only” + قيد CK_PayrollRuns_Status).
2) `/payroll-runs/{id}/approve` يغيّر الحالة إلى `approved` ويحدّث أقساط السلف (مسار API) ويُتحقق بتغيير `AdvanceInstallments.Status` إلى `deducted` [افتراض قيمة التحديث لكن ضمن القيد CK_AdvanceInstallments_Status].
3) `/payroll-runs/{id}/unapprove` يعكس أثر الأقساط (مسار API) ويُتحقق بعودة `AdvanceInstallments.Status` إلى `pending` [افتراض].
4) `NetPay` في `PayrollRunLines` يساوي `TotalEarnings-TotalDeductions` (أعمدة PayrollRunLines) باختبار حسابي.

### المرحلة 6: التقارير/لوحة القيادة/المهام/الإشعارات/الجوال **[مستنتج]**
**نطاق**
- `/reports/{key}`, `/report-views`, `/dashboard/*`, `/tasks`, `/notifications`, `/mobile/*`, `/devices/register`, `/me/*`.

**معايير قبول**
1) `/reports/{key}?format=pdf|xlsx|csv` يصدّر حسب المسار (مسار API).
2) `/mobile/punch` “idempotent by clientId” (مسار API) ويُتحقق عبر فهرس UX_PunchLogs_ClientId.
3) `/notifications/{id}/read` يضبط `Notifications.IsRead=1` و`ReadAt` (جدول Notifications/أعمدة).

---

### نقاط معلّقة يلزم تثبيتها من “الوثيقة الرئيسية” (غير متاحة) **[افتراض مطلوب توثيقه لاحقاً]**
- أولوية التصنيف بين `leave/holiday/weekend/excused/present` عند التداخل.
- تعريف نافذة تجميع PunchLogs بدقة عند CrossesMidnight.
- معادلات الرواتب التفصيلية: معامل الإضافي، تعريف InsurableWage، كيفية تحويل دقائق التأخير/الغياب إلى خصومات، وكيفية التعامل مع Leaves.IsPaid.
- سلوك “إعادة فتح الشهر” `POST /attendance/monthly/{id}/reopen` وصلاحياته التفصيلية (مسار API يذكر manager only, audited دون جداول مخصصة لذلك).

إذا زوّدتني بـ`02_seed.sql` أو مقتطفات منه (خصوصاً بنود الرواتب/أنواع الإجازات الافتراضية) يمكنني تقليل مساحة [افتراض] في قسم الرواتب وربط PayrollItems.SystemKey إن وُجدت قيمه هناك (PayrollItems.SystemKey عمود موجود).