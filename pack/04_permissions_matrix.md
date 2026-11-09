# مصفوفة الصلاحيات

الرموز: R عرض، C إضافة، U تعديل، D حذف، E تصدير، A اعتماد، P تنفيذ/إقفال/ترحيل. «—» تعني لا صلاحية.
رمز الصلاحية الفعلي = `module.resource.action` (مثل `attendance.punches.process`). المصفوفة هي ما يزرعه `02_seed.sql` للأدوار الافتراضية.
نطاق البيانات (Data Scope) يُطبَّق فوق الصلاحية: المسؤول المباشر يرى فقط عقد الهيكل المسندة إليه.

| الوحدة | المورد | الاسم | sysadmin | hr_manager | payroll | attendance_officer | line_manager | employee | auditor |
|---|---|---|---|---|---|---|---|---|---|
| admin | companies | الشركات | RCUD | — | — | — | — | — | R |
| admin | fiscal_years | السنوات المالية | RCUDP | R | — | — | — | — | R |
| admin | users | المستخدمون | RCUD | R | — | — | — | — | R |
| admin | user_groups | مجموعات المستخدمين | RCUD | — | — | — | — | — | R |
| admin | roles | الأدوار وقوالب الصلاحيات | RCUD | — | — | — | — | — | R |
| admin | settings | الإعدادات | RU | — | — | — | — | — | R |
| admin | report_headers | ترويسة التقارير | RU | — | — | — | — | — | R |
| admin | backups | النسخ الاحتياطي | RCP | — | — | — | — | — | R |
| admin | audit_logs | سجل التدقيق | RE | R | — | — | — | — | RE |
| defs | org_nodes | الهيكل الإداري | RCUD | RCUD | R | R | — | — | R |
| defs | job_grades | الدرجات الوظيفية | RCUD | RCUD | R | R | — | — | R |
| defs | jobs | الوظائف | RCUD | RCUD | R | R | — | — | R |
| defs | cost_centers | مراكز التكلفة | RCUD | RCUD | R | R | — | — | R |
| defs | employee_groups | مجموعات الموظفين | RCUD | RCUD | R | R | — | — | R |
| defs | projects | المشاريع | RCUD | RCUD | R | R | — | — | R |
| defs | banks | البنوك | RCUD | RCUD | R | R | — | — | R |
| defs | countries | الدول والمدن | RCUD | RCUD | R | R | — | — | R |
| defs | insurance_companies | شركات التأمين | RCUD | RCUD | R | R | — | — | R |
| defs | document_types | أنواع الوثائق | RCUD | RCUD | R | R | — | — | R |
| defs | sponsors | الكفلاء | RCUD | RCUD | R | R | — | — | R |
| defs | nationalities | الجنسيات | RCUD | RCUD | R | R | — | — | R |
| defs | work_natures | طبيعة العمل | RCUD | RCUD | R | R | — | — | R |
| defs | lookups | القوائم الثابتة | RCUD | RCUD | R | R | — | — | R |
| defs | geo_locations | المواقع الجغرافية | RCUD | RCUD | R | R | — | — | R |
| employees | employees | ملف الموظف | RCUDEP | RCUDEP | R | R | R | — | RE |
| employees | financial | البيانات المالية للموظف | RCUD | RCUD | RU | R | — | — | R |
| employees | government | البيانات الحكومية | RU | RU | RU | R | — | — | R |
| employees | documents | وثائق الموظف | RCUD | RCUD | R | R | R | — | R |
| employees | archive | أرشيف الموظف | RCUD | RCUD | R | R | — | — | R |
| employees | companions | المرافقون | RCUD | RCUD | R | R | — | — | R |
| employees | qualifications | المؤهلات | RCUD | RCUD | R | R | — | — | R |
| employees | experiences | الخبرات | RCUD | RCUD | R | R | — | — | R |
| employees | history | حركة الموظف | R | R | R | R | R | — | R |
| employees | portal_account | البوابة الإلكترونية | RU | RU | R | R | — | — | R |
| attendance | shifts | فترات الدوام | RCUD | RCUD | — | RCUD | — | — | R |
| attendance | shift_assignments | ربط الموظفين بالدوام | RCD | RCD | — | RCD | — | — | R |
| attendance | devices | أجهزة البصمة | RCUDP | RCUDP | — | RCUDP | — | — | R |
| attendance | punches | سجل التحضير | RCDEP | RCDEP | — | RCDEP | R | — | RE |
| attendance | holidays | الإجازات الرسمية | RCUD | RCUD | — | RCUD | — | — | R |
| attendance | excuse_types | أنواع الاستئذان | RCUD | RCUD | — | RCUD | — | — | R |
| attendance | excuses | تسجيل الاستئذان | RCUDA | RCUDA | — | RCUDA | RA | — | R |
| attendance | daily | حافظ الدوام اليومي | RUEP | RUEP | RE | RUEP | R | — | RE |
| attendance | monthly | الملخص الشهري | RCEP | RCEP | RE | RCEP | R | — | RE |
| leaves | leave_types | أنواع الإجازات | RCUD | RCUD | R | R | — | — | R |
| leaves | balances | أرصدة الإجازات | RCUA | RCUA | R | R | — | — | R |
| leaves | leaves | إجازات الموظفين | RCUDEA | RCUDEA | R | R | RCA | — | RE |
| requests | requests | الطلبات | RCA | RCA | — | — | RCA | — | R |
| requests | approval_flows | مسارات الموافقات | RCUD | RCUD | — | — | — | — | R |
| requests | delegations | التفويض | RCUD | RCUD | — | — | RCU | — | R |
| finance | payroll_items | بنود الرواتب | RCUD | R | RCUD | — | — | — | R |
| finance | variable_items | البدلات والاستقطاعات المتغيرة | RCUDA | RCUDA | RCUDA | — | — | — | R |
| finance | advances | السلف | RCUDEP | RCUDEP | RCUDEP | — | — | — | RE |
| finance | custodies | العهد | RCUDP | RCUDP | RCUDP | — | — | — | R |
| finance | payroll_runs | كشوف الرواتب | RCUEAP | RE | RCUEAP | — | — | — | RE |
| finance | payslips | قسائم الرواتب | RE | RE | RE | — | — | — | RE |
| finance | social_insurance | كشف التأمينات | RCEA | RE | RCEA | — | — | — | RE |
| finance | end_of_service | سند نهاية الخدمة | RCUEA | RE | RCUEA | — | — | — | RE |
| reports | reports | التقارير | RE | RE | RE | RE | RE | — | RE |
| reports | report_views | العروض المحفوظة | RCUD | RCUD | RCUD | RCUD | R | — | R |
| dashboard | dashboard | لوحة المعلومات | R | R | R | R | R | — | R |
| tasks | tasks | المهام | RCUD | RCUD | RCUD | RCUD | RCUD | — | R |
| tasks | notifications | الإشعارات | RU | RU | RU | RU | RU | — | R |
| me | profile | ملفي | R | — | — | — | — | R | — |
| me | attendance | حضوري | R | — | — | — | — | R | — |
| me | payslips | قسائمي | R | — | — | — | — | R | — |
| me | leaves | إجازاتي | RC | — | — | — | — | RC | — |
| me | balances | أرصدتي | R | — | — | — | — | R | — |
| me | requests | طلباتي | RC | — | — | — | — | RC | — |
| me | documents | وثائقي | R | — | — | — | — | R | — |
| me | punch | بصمتي من الجوال | C | — | — | — | — | C | — |

## الأدوار

- `sysadmin`: مدير النظام
- `hr_manager`: مدير موارد بشرية
- `payroll`: مسؤول رواتب
- `attendance_officer`: مسؤول حضور
- `line_manager`: مسؤول مباشر
- `employee`: موظف
- `auditor`: مدقق

## ملاحظات تنفيذ

- الحقول الحساسة (الهوية، الجواز، حساب البنك، الراتب) تتطلب `employees.government.read` أو `employees.financial.read` وإلا تُرجَع فارغة (null) ويُسجَّل أي وصول ناجح في AuditLogs.
- دور `employee` يعمل فقط عبر `/me/*` ولا يرى بيانات غيره.
- دور `line_manager` محدود بنطاق الهيكل الإداري ويعتمد طلبات مرؤوسيه فقط (المسار في ApprovalFlows).
- إجمالي الصلاحيات: 248. إجمالي الروابط الافتراضية بين الأدوار والصلاحيات: 721.