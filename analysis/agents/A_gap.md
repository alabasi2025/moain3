<!-- agent A_gap model gpt-5.2 -->
## 1) جدول تتبّع Traceability (الدليل ← الجدول.العمود ← مسار API ← الحالة)

> ملاحظة منهجية: الدليل يصف شاشات/أزرار كثيرة بدون ذكر API. تم الربط فقط بما له مقابل مباشر في (المخطط) و(قائمة API). أي عنصر لم نجد له API صريحاً و/أو جدولاً مناسباً اعتُبر “مفقود/جزئي” دون اختراع مسارات.  

### 1.1 تسجيل الدخول (دليل ص3)
| عنصر الدليل | المصدر | الجدول.العمود (المخطط) | مسار API | الحالة |
|---|---|---|---|---|
| السنة (السنة المالية) | (دليل ص3) | FiscalYears.FiscalYear + FiscalYears.IsClosed/IsCurrent | GET /auth/fiscal-years | مغطى |
| الشركة (رقم الشركة) | (دليل ص3) | Companies.Code | GET /companies | جزئي (الإدراج في شاشة الدخول غير مذكور صراحة في API؛ الموجود عام) |
| اسم المستخدم | (دليل ص3) | Users.Username | POST /auth/login | مغطى |
| كلمة المرور | (دليل ص3) | Users.PasswordHash | POST /auth/login | مغطى |
| اللغة | (دليل ص3) | Users.Language و/أو Companies.DefaultLanguage | POST /auth/login + GET /auth/me | جزئي (آلية اختيار اللغة عند الدخول غير مذكورة في API) |
| تذكري (Remember me) | (دليل ص3) | RefreshTokens.* | POST /auth/refresh + POST /auth/logout | جزئي (لا يوجد حقل rememberMe مذكور في POST /auth/login) |
| زر دخول | (دليل ص3) | — | POST /auth/login | مغطى |
| زر إغلاق | (دليل ص3) | — | — | مفقود (لا API؛ سلوك UI فقط) |

### 1.2 الشاشة الرئيسية: تنبيهات/طلبات/مخططات (دليل ص4-ص5)
| عنصر الدليل | المصدر | الجدول.العمود | مسار API | الحالة |
|---|---|---|---|---|
| Alerts/التنبيهات (وثائق منتهية/إجازات/عهد..) | (دليل ص4، ص5) | Notifications.* + [قد تعتمد على vw_ExpiringDocuments] | GET /dashboard/alerts | جزئي (مصادر التنبيه التفصيلية غير معرّفة في API) |
| Employees Requests/طلبات الموظفين | (دليل ص4، ص5) | Requests.RequestType/Status | GET /dashboard/requests-summary | مغطى |
| Charts/مخططات | (دليل ص4، ص5) | — | GET /dashboard/charts | مغطى |
| شجرة النظام (التنقل للوحدات) | (دليل ص4، ص5) | Permissions/RolePermissions… | GET /auth/me | جزئي (صلاحيات عرض الشجرة ليست موصوفة ككيان UI) |

### 1.3 الإدارة والتحكم (دليل ص5)
| عنصر الدليل | المصدر | الجدول.العمود | مسار API | الحالة |
|---|---|---|---|---|
| السنة المالية (تحديد بداية/نهاية) | (دليل ص5) | FiscalYears.StartDate/EndDate/IsClosed | CRUD /fiscal-years + POST /fiscal-years/{id}/close | مغطى |
| الإقفالات المالية | (دليل ص5) | FiscalYears.IsClosed | POST /fiscal-years/{id}/close | جزئي (لا يظهر “فتح/إلغاء إقفال” كوظيفة في الدليل؛ وAPI لا يذكر reopen سنة) |
| المستخدمين | (دليل ص5) | Users.* | CRUD /users | مغطى |
| مجموعة المستخدمين | (دليل ص5) | UserGroups.* | CRUD /user-groups | مغطى |
| قوالب الصلاحيات | (دليل ص5) | Roles.* + RolePermissions.* | CRUD /roles | جزئي (لا توجد مسارات صريحة لإدارة RolePermissions) |
| ترويسة التقارير | (دليل ص5) | ReportHeaders.* | GET/PUT /report-headers | مغطى |
| تحديث النظام | (دليل ص5) | — | — | مفقود (غير موجود في API) |
| الإعدادات والضبط (ربط محاسبي/استيراد مراكز تكلفة) | (دليل ص5) | Settings.* | GET/PUT /settings | جزئي (لا يوجد API مخصص للاستيراد/تكامل محاسبي) |
| النسخة الاحتياطية | (دليل ص5) | — | GET/POST /backups + POST /backups/{key}/restore | مغطى |
| تحويلات التاريخ (ميلادي/هجري) | (دليل ص5) | Companies.HijriAdjustDays | GET /calendar/convert | مغطى |

### 1.4 التعريفات العامة (الهيكل الإداري والمرجعيات) (دليل ص8-ص13)
| عنصر الدليل | المصدر | الجدول.العمود | مسار API | الحالة |
|---|---|---|---|---|
| الهيكل الإداري: رقم العقدة/اسم محلي/اسم أجنبي/النوع/إضافة عقدة (+)/حفظ | (دليل ص8-ص9) | OrgNodes.Code/NameAr/NameEn/NodeType/ParentId | CRUD /org-nodes + GET /org-nodes/tree + POST /org-nodes/{id}/move | مغطى |
| الدرجات الوظيفية | (دليل ص11) | JobGrades.* | CRUD /job-grades | مغطى |
| الوظائف | (دليل ص11) | Jobs.* (+ Jobs.JobGradeId) | CRUD /jobs | مغطى |
| مراكز التكلفة | (دليل ص12) | CostCenters.* | CRUD /cost-centers | مغطى |
| مجموعات الموظفين | (دليل ص12) | EmployeeGroups.* | CRUD /employee-groups | مغطى |
| المشاريع | (دليل ص13) | Projects.* | CRUD /projects | مغطى |
| بيانات البنوك | (دليل ص13) | Banks.* | CRUD /banks | مغطى |
| الدول والمدن (دولة ثم مدن تابعة) | (دليل ص13) | Countries.* + Cities.CountryId | CRUD /countries + /countries/{id}/cities | مغطى |
| شركات التأمين | (دليل ص13) | InsuranceCompanies.* | CRUD /insurance-companies | مغطى |
| أنواع الوثائق | (دليل ص13، ص16) | DocumentTypes.* | CRUD /document-types | مغطى |
| بيانات الكفلاء | (دليل ص13) | Sponsors.* | CRUD /sponsors | مغطى |
| الجنسيات | (دليل ص13) | Nationalities.* | CRUD /nationalities | مغطى |
| تعريف طبيعة العمل | (دليل ص13) | WorkNatures.* | CRUD /work-natures | مغطى |

### 1.5 ملف الموظف (بيانات أساسية/حكومية/مالية/بوابة/وثائق/أرشيف/مرافقين/مؤهلات/خبرات/حركة) (دليل ص10، ص14-ص18)
| عنصر الدليل | المصدر | الجدول.العمود | مسار API | الحالة |
|---|---|---|---|---|
| إنشاء/تعديل/حذف/بحث موظف | (دليل ص10، ص14) | Employees.* | CRUD /employees + POST /employees/query | مغطى |
| الرقم الآلي/رقم البصمة/الباركود | (دليل ص10، ص14، ص22) | Employees.EmployeeNo/FingerprintNo/Barcode | /employees | مغطى |
| الاسم العربي/الأجنبي وتفاصيله | (دليل ص10، ص14) | Employees.FirstNameAr…FullNameEn | /employees | مغطى |
| الجنس | (دليل ص10، ص14) | Employees.Gender + CK_Employees_Gender | /employees | مغطى |
| الجنسية/الديانة/الحالة الاجتماعية/اللغة | (دليل ص10، ص14) | Employees.NationalityId/ReligionId/MaritalStatusId + Lookups | /employees + /lookups | جزئي (نوع LookupType المطلوب غير مذكور) |
| الميلاد: دولة/مدينة/تاريخ (م/هـ) | (دليل ص14، ص15) | Employees.BirthCountryId/BirthCityId/BirthDate + /calendar/convert | /employees + GET /calendar/convert | جزئي (تخزين هجري غير موجود كحقول؛ يعتمد على التحويل) |
| بيانات العمل: موقع إداري/هيكل إداري | (دليل ص10، ص14، ص19) | Employees.OrgNodeId + OrgNodes | /employees + /org-nodes | مغطى |
| درجة الموظف/الوظيفة/المجموعة/مركز التكلفة/المشروع | (دليل ص10، ص14) | Employees.JobGradeId/JobId/EmployeeGroupId/CostCenterId/ProjectId | /employees + تعريفات (/job-grades…)| مغطى |
| طبيعة العمل | (دليل ص10، ص14) | Employees.WorkNatureId | /employees + /work-natures | مغطى |
| حساب بنك | (دليل ص10، ص14، ص20) | Employees.BankId/BankAccount | /employees + /banks | مغطى |
| حالة الموظف | (دليل ص10، ص14، ص19) | Employees.StatusId + Lookups | /employees + /lookups | جزئي (LookupType للحالة غير مذكور) |
| “يمكن احتساب الإضافي للموظف” | (دليل ص10، ص14) | Employees.OvertimeEnabled | /employees | مغطى |
| بيانات التعاقد: نوع التعاقد/بداية/نهاية/عقد مفتوح | (دليل ص10، ص14، ص20) | Employees.ContractTypeId/ContractStart/ContractEnd/IsOpenContract | /employees + /lookups | جزئي (LookupType لأنواع التعاقد غير مذكور) |
| “مباشرة العمل” + نافذة تاريخ المباشرة | (دليل ص14، ص22) | EmployeeWorkStarts.StartDate + Employees.WorkStartDate/LastWorkStartDate | POST /employees/{id}/start-work | مغطى |
| عرض سجل عمل الموظف/حركة الموظف | (دليل ص14، ص18) | AuditLogs.* + vw_EmployeeHistory | GET /employees/{id}/history + GET /audit-logs | جزئي (الفرق بين “حركة الموظف” وAuditLogs يحتاج توضيح) |
| البيانات الحكومية: هوية/جواز/تأمين/تأمينات | (دليل ص15، ص20) | Employees.Identity* / Passport* / Insurance* / Social* | PUT /employees/{id} | مغطى |
| البيانات المالية: بنود المستحقات/الاستقطاعات + إجمالي الراتب | (دليل ص15، ص20) | EmployeeSalaryItems.* + Employees.* (لا يوجد TotalSalary) | /employees/{id}/salary-items | جزئي (حقل “إجمالي الراتب” غير ممثل كعمود صريح) |
| البوابة الإلكترونية/صلاحيات تطبيق | (دليل ص16) | EmployeePortalAccounts.* + Users.UserType | PUT /employees/{id}/portal-account | مغطى |
| صورة الموظف | (دليل ص10، ص14) | Employees.PhotoAttachmentId + Attachments | PUT /employees/{id}/photo | مغطى |
| الوثائق (نوع/رقم/تواريخ م/هـ) | (دليل ص16، ص20) | EmployeeDocuments.* + DocumentTypes | /employees/{id}/documents + /document-types | جزئي (الهجري غير مخزن كأعمدة؛ يعتمد على التحويل) |
| الأرشيف (عنوان المرفق/ملف) | (دليل ص17) | EmployeeArchive.Title/AttachmentId + Attachments | /employees/{id}/archive | مغطى |
| المرافقون (صلة/رقم إقامة/تواريخ م/هـ) | (دليل ص17) | EmployeeCompanions.RelationId/ResidenceNo/IssueDate/ExpiryDate | /employees/{id}/companions + /lookups | جزئي (LookupType لصلات القرابة غير مذكور + الهجري تحويل) |
| المؤهلات | (دليل ص18) | EmployeeQualifications.* + [QualificationId lookup] | /employees/{id}/qualifications | جزئي (لا يوجد جدول تعريف Qualifications؛ يتطلب Lookups/جدول جديد) |
| الخبرات | (دليل ص18) | EmployeeExperiences.* | /employees/{id}/experiences | مغطى |

### 1.6 فترات الدوام/الربط (دليل ص21-ص23)
| عنصر الدليل | المصدر | الجدول.العمود | مسار API | الحالة |
|---|---|---|---|---|
| فترات الدوام (وقت حضور/انصراف/استراحة/سماح/أنماط بصمة/أسبوعي/نوبات/إيقاف) | (دليل ص21) | Shifts.* + ShiftDays.* + ShiftRotations.* | CRUD /shifts | جزئي (بعض خيارات UI لا تقابل أعمدة مباشرة مثل “لا يحتسب ساعات العمل” أو النصوص غير المقروءة) |
| ربط موظفين بفترة (تصفية حسب موظف/بصمة/موقع/درجة/وظيفة/مجموعة/مركز تكلفة + إنزال + حفظ) | (دليل ص23) | EmployeeShifts.* | POST /shift-assignments/preview + POST /shift-assignments/apply + GET /shift-assignments | مغطى |
| شرط “ربط الدوام بعد مباشرة العمل” | (دليل ص22) | Employees.WorkStartDate + منطق في POST /employees/{id}/start-work | POST /employees/{id}/start-work + POST /shift-assignments/apply | جزئي (قيد المنع/السماح غير موصوف في API نصاً) |

### 1.7 المرتبات: إعداد كشف الراتب وتقارير (دليل ص24-ص27)
| عنصر الدليل | المصدر | الجدول.العمود | مسار API | الحالة |
|---|---|---|---|---|
| إعداد كشف الراتب: نمطي/مخصص + شهر/سنة + بيان + معاينة + اعتماد | (دليل ص24-ص25) | PayrollRuns.RunType/PeriodYear/PeriodMonth/IssueDate/Description/Status + PayrollRunLines | /payroll-runs + /payroll-runs/{id}/calculate + /payroll-runs/{id}/approve | جزئي (لا يوجد API “معاينة” مطابق زر “معاينة كشف الراتب”؛ قد تُستبدل بـ calculate/عرض lines) |
| عرض سطور الموظفين في الكشف | (دليل ص25) | PayrollRunLines.* | GET /payroll-runs/{id}/lines | مغطى |
| البنود لكل موظف داخل الكشف | (دليل ص25-ص26) | PayrollRunLineItems.* | — | مفقود (لا API مذكور لاسترجاع line-items) |
| “سجل الاستنادات” | (دليل ص25) | [غير محدد في المخطط] | — | مفقود |
| تقرير ملف الموظف + عرض متقدم/تحديد أعمدة | (دليل ص19-ص20) | Employees + ReportViews.LayoutJson | GET /reports/{key} + CRUD /report-views | جزئي (مفاتيح التقارير ReportKey غير مذكورة؛ وتطابق شاشة “عرض متقدم” يحتاج تعريف) |
| تقرير كشف الراتب (إجمالي/تفصيلي/توقيع/نقدي/قسيمة) | (دليل ص26-ص27) | PayrollRuns/Lines/Items + ReportViews | GET /reports/{key} | جزئي (ReportKey غير معرّف + عناصر تفصيل البنود تحتاج line-items API) |

---

## 2) قائمة المفقودات مرتبة بالأولوية + اقتراحات دقيقة (جدول/عمود/مسار)

> الأولوية مبنية على أثرها التشغيلي على وظائف الدليل الأساسية (ملف الموظف/الدوام/المرتبات) وعلى وجود عناصر “مذكورة في الدليل” بلا مقابل واضح في API/المخطط.

### P1 — واجهات/بيانات كشف الراتب التفصيلية
1) **استرجاع بنود كشف الراتب لكل موظف (PayrollRunLineItems)**
- الدليل: “إظهار البنود الخاصة بالموظف” (دليل ص25).
- المخطط: PayrollRunLineItems(LineId, PayrollItemId, Amount, …).
- API: لا يوجد مسار لقراءة البنود.
- **الاقتراح:**  
  - GET `/payroll-runs/{id}/lines/{lineId}/items`  ← يعيد PayrollRunLineItems حسب LineId.  
  - أو GET `/payroll-runs/{id}/items?employeeId=` [افتراض].
- الحالة: مفقود (API).

2) **“سجل الاستنادات” المرتبط بكشف الراتب**
- الدليل: “يعرض سجل الاستنادات للموظفين” (دليل ص25).
- المخطط: لا يوجد كيان واضح باسم الاستنادات.
- **الاقتراح:** تحديد المقصود: هل هو (AuditLogs) أم مرفقات (Attachments) أم قيود محاسبية [افتراض].  
  - إن كان مطلوباً ككيان: جدول `PayrollRunReferences(RunId, RefType, RefNo, RefDate, Notes, AttachmentId)` [افتراض] + API `/payroll-runs/{id}/references`.
- الحالة: مفقود (مخطط+API).

3) **معاينة كشف الراتب قبل الاعتماد**
- الدليل: زر “معاينة كشف الراتب” (دليل ص25).
- API الموجود: calculate/approve/lines.
- **الاقتراح:** اعتماد calculate كمعاينة مع مسار صريح:
  - POST `/payroll-runs/{id}/preview` يعيد خطوط محسوبة دون حفظ/قفل [افتراض]، أو توثيق أن `/calculate` يقوم بالمعاينة.
- الحالة: جزئي (تطابق دلالي غير موثق).

### P1 — قوائم Lookup المطلوبة لملف الموظف
4) **قوائم (الديانة/الحالة الاجتماعية/حالة الموظف/نوع التعاقد/صلة القرابة/المؤهلات)**
- الدليل: حقول إلزامية مثل “الديانة/الحالة الاجتماعية” (دليل ص10) + صلة القرابة (دليل ص17) + المؤهل (دليل ص18).
- المخطط: Lookups(LookupType, Code, NameAr, …) موجود، لكن لا توجد أنواع LookupType معرفة.
- API: /lookups موجود بدون تقييد/تعريف الأنواع.
- **الاقتراح:** توثيق LookupType القياسية وإلزامها:
  - `religions`, `marital_status`, `employee_status`, `contract_types`, `companion_relations`, `qualifications` [افتراض]  
  وإتاحة فلترة: GET `/lookups?type={LookupType}` [افتراض إن لم تكن موجودة].
- الحالة: جزئي (النظام قادر لكن غير محدد/غير مضمون).

### P2 — “قوالب الصلاحيات” وربطها
5) **إدارة ربط الصلاحيات بالأدوار/المستخدمين**
- الدليل: “قوالب الصلاحيات” (دليل ص5).
- المخطط: RolePermissions, UserRoles, UserGroupRoles موجودة.
- API: لا يوجد CRUD لهذه الجداول (فقط /roles و/users و/user-groups).
- **الاقتراح:**  
  - GET/PUT `/roles/{id}/permissions` (RolePermissions)  
  - GET/PUT `/users/{id}/roles` (UserRoles)  
  - GET/PUT `/user-groups/{id}/roles` (UserGroupRoles)
- الحالة: مفقود (API).

### P2 — فجوات جزئية في فترات الدوام
6) **خيارات فترات الدوام غير ممثلة صراحة**
- الدليل: “لا يحتسب ساعات العمل”، وحالات “بصمة خروج بدون انصراف…” (دليل ص21).
- المخطط: Shifts يحتوي PresentIfNoOut/PresentIfNoIn وRoundWorkTime وPunchMode… لكن لا يوجد عمود واضح لـ “لا يحتسب ساعات العمل”.
- **الاقتراح:** إن كانت مطلوبة وظيفياً: إضافة `Shifts.CountWorkMinutes:bit` [افتراض] أو توضيح أن “لا يحتسب ساعات العمل” تقابل RoundWorkTime/WorkMinutes [افتراض].
- الحالة: جزئي (مخطط).

### P3 — “تحديث النظام” و“ربط محاسبي/استيراد”
7) **تحديث النظام**  
- الدليل: بند “تحديث النظام” (دليل ص5).  
- لا مقابل في المخطط/API.  
- **الاقتراح:** تعريف المقصود (ترحيل بيانات؟ إعادة فهرسة؟) ثم تصميم API `/admin/system/refresh` [افتراض] أو إلغاء الميزة من النطاق.
- الحالة: مفقود.

8) **الإعدادات والضبط: ربط محاسبي/استيراد مراكز تكلفة**
- الدليل: “ربط النظام مع النظام المحاسبي والاستيراد مراكز التكلفة” (دليل ص5).
- المخطط/API: Settings موجود، لا يوجد تكامل/استيراد.
- **الاقتراح:** API استيراد:
  - POST `/cost-centers/import` (CSV/XLSX) [افتراض].
- الحالة: مفقود (API).

---

## 3) ميزات في الحزمة الجديدة غير موجودة/غير ظاهرة في النظام القائم (تحسينات)

1) **الموافقات وسير العمل للطلبات (ApprovalFlows/Requests/Inbox/Steps/Comments/Delegations)**
- لا يظهر في الدليل إلا “طلبات الموظفين” كقائمة عامة (دليل ص4-ص5) دون ذكر سير موافقات أو تفويض.
- الحزمة: ApprovalFlows + RequestSteps + Delegations + /requests/inbox… (API).
- التحسين: حوكمة كاملة لاعتمادات الإجازات/السلف/العهد/الاستئذان… (API /requests/*).

2) **الحضور عبر الجوال + التحقق بالموقع الجغرافي**
- الدليل يذكر “البوابة الإلكترونية” وربط عبر تطبيق (دليل ص16) دون ذكر بصمة جوال بموقع.
- الحزمة: /mobile/punch + GeoLocations + /mobile/config + Device register (API).
- التحسين: بصمة جوال Idempotent (PunchLogs.ClientId) (المخطط PunchLogs + API).

3) **سحب بصمات من أجهزة وربطها وتشخيص الاتصال**
- الدليل يذكر “تعريف أجهزة البصمة” في الشجرة (دليل ص22) بلا تفاصيل.
- الحزمة: BiometricDevices + /biometric-devices/{id}/test و/pull و/punches/batch (API).
- التحسين: تكامل مُدار مع الأجهزة + استيراد/يدوي/موبايل (CK_PunchLogs_Source).

4) **إقفال شهر الحضور (MonthlyAttendances close/reopen) وتغذية الرواتب**
- الدليل يشرح فترات وربط فقط (دليل ص21-ص23).
- الحزمة: /attendance/monthly/{id}/close و/attendance/process وDailyAttendance override مع سبب (API).
- التحسين: محرك حضور قابل لإعادة المعالجة وقفل.

5) **نسخة احتياطية واستعادة عبر API**
- الدليل يذكر نسخة احتياطية كوظيفة (دليل ص5) دون تفاصيل تقنية.
- الحزمة: /backups + restore (API).

6) **سجل تدقيق شامل AuditLogs واستعلامه**
- الدليل يعرض “حركة الموظف” (دليل ص18) كناتج UI.
- الحزمة: AuditLogs ببيانات Old/New وسبب وIP (المخطط AuditLogs) + GET /audit-logs (API).
- التحسين: تتبع تدقيقي قابل للاستعلام العام.

7) **حفظ تخطيطات التقارير ReportViews (Save/Load XML في الدليل)**
- الدليل يذكر Save/Load XML (دليل ص26) كواجهة.
- الحزمة تقدم ReportViews(LayoutJson) + CRUD /report-views (المخطط+API).
- التحسين: تخطيطات مشتركة/مخصصة بدل ملفات XML محلية.

---

## 4) أسئلة يجب طرحها على العميل

1) **السنة المالية في شاشة الدخول:** هل اختيار السنة يحدد CompanyId+FiscalYear للسياق التشغيلي لكل الطلبات؟ (دليل ص3، API GET /auth/fiscal-years). ما آلية تمرير FiscalYearId لباقي العمليات؟ [افتراض]

2) **معنى “تذكري”**: هل المطلوب جلسة طويلة عبر Refresh Token فقط، أم تذكر بيانات الشركة/السنة/اللغة أيضاً؟ (دليل ص3، API /auth/refresh).

3) **LookupType القياسية المطلوبة**: ما القيم الرسمية لكل من (الديانة، الحالة الاجتماعية، حالة الموظف، نوع التعاقد، صلة القرابة، المؤهلات)؟ (دليل ص10، ص15، ص17، ص18؛ جدول Lookups).

4) **حقل “اللغة” في ملف الموظف** (دليل ص10) هل هو لغة الواجهة للمستخدم أم لغة تواصل الموظف؟ وأين يُخزن؟ لا يوجد عمود لغة ضمن Employees (المخطط Employees).

5) **إجمالي الراتب في تبويب البيانات المالية**: هل هو قيمة مخزنة أم محسوبة من EmployeeSalaryItems؟ (دليل ص15) لأن المخطط لا يحوي عمود TotalSalary في Employees.

6) **الميلادي/الهجري:** هل يُطلب تخزين التاريخين معاً أم يكفي تخزين الميلادي والتحويل عند العرض؟ (دليل ص15-ص17؛ API /calendar/convert).

7) **“سجل الاستنادات” في كشف الراتب:** ما تعريفه الدقيق ومصدر بياناته؟ (دليل ص25) لأنه غير ممثل في المخطط.

8) **المعاينة في كشف الراتب:** هل “معاينة كشف الراتب” تقوم بحساب فعلي وتثبيت نتائج أم مجرد عرض؟ (دليل ص25؛ API /payroll-runs/{id}/calculate و/lines).

9) **كشف راتب مخصص:** هل التخصيص “لموظف واحد فقط” كما ورد (دليل ص25) أم قد يكون لإدارة/فرع/مجموعة؟ وكيف يُمثل النطاق ScopeJson في PayrollRuns؟ (المخطط PayrollRuns.ScopeJson).

10) **قوالب الصلاحيات:** هل العميل يحتاج شاشة لإدارة الصلاحيات تفصيلاً (RolePermissions/UserRoles/UserGroupRoles) أم يكفي أدوار ثابتة؟ (دليل ص5؛ المخطط RolePermissions…).

11) **فترات الدوام:** ما المقصود بخيار “لا يحتسب ساعات العمل” والحالات غير المقروءة المتعلقة ببصمة بدون دخول/انصراف (دليل ص21) لتحديد تطابقها مع Shifts.PresentIfNoIn/PresentIfNoOut أو الحاجة لأعمدة إضافية.

12) **ربط الموظف بالدوام مشروط بمباشرة العمل:** هل المنع إلزامي (لا يسمح بالربط قبل start-work) أم مجرد تنبيه؟ (دليل ص22؛ API /employees/{id}/start-work و/shift-assignments/apply).

13) **تقرير ملف الموظف/عرض متقدم:** ما قائمة التقارير المطلوبة ومفاتيحها ReportKey؟ (دليل ص19-ص20؛ API GET /reports/{key} + جدول ReportHeaders/ReportViews).

إذا رغبتَ، أستطيع إعادة إخراج جدول التتبع بصيغة CSV/Excel-ready مع أعمدة ثابتة (Screen, ControlType, DocRef, Table.Column, API, Coverage, Notes) اعتماداً على نفس المراجع أعلاه.