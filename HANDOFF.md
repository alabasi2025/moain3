# HANDOFF — للمتابعة في بيئة/وكيل جديد

> اقرأ هذا الملف أولاً. الكتاب الشامل: `docs/BOOK_كتاب_المشروع.md`. آخر تحديث: 2026-11-09 08:45. الفرع الرئيسي `main` يحوي كل شيء (طلب الدمج #1 مدموج).

## 1. ما هو المشروع
إعادة بناء «نظام شؤون الموظفين» (أنظمة الجزيرة — دليل 27 صفحة في `source/`) على حزمة البناء `pack/` (SQL Server + OpenAPI + صلاحيات)، متوافق مع **قانون العمل اليمني 5/1995** وقانون التأمينات 26/1991.
التقنية: **ASP.NET Core 8 + Dapper + SQL Server 2022** + واجهة ويب عربية RTL (ملف واحد) + اختبارات xUnit و E2E بايثون.

## 2. الحالة الآن (مُتحقَّق منها)
| البند | الحالة |
|---|---|
| البناء | ✅ 0 أخطاء، 0 تحذيرات |
| اختبارات الوحدة | ✅ 77/77 (`dotnet test`) |
| E2E عام (دخول، هيكل، تعريفات، موظف، فترات، بصمات، احتساب حضور) | ✅ 36/36 — `tools/e2e_smoke.py` |
| E2E رواتب (احتساب، بنود، اعتماد، عكس، صرف، تدقيق) | ✅ 35/35 — `tools/e2e_payroll.py` |
| الواجهة | ✅ دخول، الرئيسية، الهيكل، التعريفات، الموظفون، الحضور |

## 3. حسابات التجربة (DEMO فقط — `fixes/08_demo_users.sql`)
| الشركة | المستخدم | كلمة المرور | الدور |
|---|---|---|---|
| `C1` | `admin` | `1` | مدير نظام (كل الصلاحيات 252) |
| `C1` | `user` | `1` | مدير موارد بشرية `hr_manager` (199 صلاحية) |
⚠️ قبل الإنتاج: احذف 08 وغيّر كلمات المرور، وفعّل سياسة كلمات مرور.

## 4. إعادة بناء البيئة من الصفر (أمر واحد)
```bash
git clone https://github.com/alabasi2025/moain3.git && cd moain3
bash tools/setup_env.sh          # SQL Server 2022 + قاعدة HRMS + بيانات + .NET 8 + build + test
source /tmp/.hrms_env && ASPNETCORE_URLS=http://0.0.0.0:5001 setsid -f dotnet src/Hrms.Api/bin/Debug/net8.0/Hrms.Api.dll > /tmp/api.log 2>&1 < /dev/null
HRMS_PASSWORD=1 python3 tools/e2e_smoke.py
HRMS_PASSWORD=1 HRMS_SQL_PASSWORD="$(cat /tmp/.sapw)" python3 tools/e2e_payroll.py
```
**مزالق معروفة (كلها معالجة في السكربت):**
1. `01_schema.sql` يجب تشغيله بـ `sqlcmd -I` وإلا يُنشئ 2 فهرساً من 188 بصمت (Msg 1934).
2. Debian 13 يحتاج `libldap-2.5` من Ubuntu لتشغيل `sqlservr`.
3. لا systemd في الساندبوكس: `sqlservr` يُشغَّل يدوياً بـ `setsid -f`.
4. `pkill -f <نمط>` قد يقتل صدفة الأوامر نفسها — استخدم نمطاً مرتكزاً مثل `pkill -f '^dotnet bin/Debug'`.
5. الأسرار (JWT/AES/اتصال) **متغيرات بيئة فقط**؛ `appsettings.json` فارغ عمداً.

## 5. الوكلاء (مفتاح البروكسي)
- القائمة الكاملة المختبرة: `analysis/proxy_models/MODELS.md` (81 نموذجاً، 75 تعمل). الأسماء الصحيحة: **`claude-opus-5-5`، `claude-fable-5-1`، `gpt-6-astra`**.
- البروكسي: `~/.genspark_llm.yaml` أو `OPENAI_API_KEY`/`OPENAI_BASE_URL` (متوافق OpenAI).
- **استخدم البث دائماً** (`stream=True`): الطلبات الطويلة بدونه تنقطع بخطأ 524. المشغّل الجاهز: `tools/run_build_agent.py <key> <model>` يقرأ `/tmp/gen/p_<key>.txt` و`/tmp/gen/common.txt` ويكتب الملفات المستخرجة إلى `/tmp/gen/code2/`.
- البرومبتات المستخدمة ومخرجاتها الخام: `analysis/agents/build/` (أعد استخدامها كقالب: `common.txt` = قواعد الإخراج `=== FILE: path ===`).
- **قاعدة ذهبية:** لا تدمج كود وكيل قبل `dotnet build` + اختبار حي. مثال خطأ اكتُشف: opus افترض أعمدة `CreatedBy/IsDeleted` في `RefreshTokens` وهي غير موجودة (`AuditLogs` و`RefreshTokens` فقط بلا الأعمدة القياسية).

## 6. خريطة الكود
```
src/Hrms.Domain/Attendance/   AttendanceEngine.Compute (دالة نقية)            ← opus
src/Hrms.Domain/Payroll/      PayrollCalculator.Calculate                       ← fable
src/Hrms.Domain/Labor/        YemenLaborRules (م28،38،43-45،48،64،71،79،80،84،93،94،120) ← fable
src/Hrms.Api/Security/        JWT، perm:* policies، PBKDF2، AES-GCM، ICurrentUser ← astra
src/Hrms.Api/Common/          ApiResponse، RFC7807 middleware                   ← astra
src/Hrms.Api/Data/            IDbConnectionFactory
src/Hrms.Api/Features/Auth|Definitions|Org|Admin   ← opus
src/Hrms.Api/Features/Employees|Attendance          ← astra (FeatureDb helper داخل EmployeesController.cs)
src/Hrms.Api/Features/Payroll                       ← opus
src/Hrms.Api/wwwroot/index.html                     ← fable
tests/Hrms.Tests/                                   اختبارات الوحدة
tools/                                              e2e، فحص الحزمة، تشغيل الوكلاء، setup_env.sh
docs/                                               التقرير الشامل، مواصفة المحرك، حالة التنفيذ
fixes/                                              06 إصلاحات+قانون يمني، 07 تحقق، 08 حسابات تجريبية
```
المسارات المنفّذة: `POST auth/login|refresh|logout`, `GET auth/me`, `defs/{kind}` CRUD (8 أنواع), `lookups`, `org-nodes/tree` + CRUD, `employees` CRUD + `start-work` + `history`, `shifts` CRUD + `simulate`, `shift-assignments/apply`, `punches/manual` + list, `attendance/process`, `attendance/daily`, `payroll-runs` CRUD + `calculate|approve|unapprove|pay|lines|lines/{id}/items`.

## 7. الباقي — بالأولوية (قائمة عمل للوكيل التالي)
**P0 — ليكتمل دورة الراتب من الواجهة دون SQL يدوي**
1. API بنود راتب الموظف `employees/{id}/salary-items` (EmployeeSalaryItems) — حالياً e2e يُدخلها بـ SQL.
2. API السلف والأقساط `advances` (توليد الأقساط تلقائياً) + العهد `custodies` + المتغيرات `variable-items`.
3. شاشة الرواتب في الواجهة (إنشاء كشف، احتساب، عرض الأسطر والبنود، اعتماد/عكس/صرف).
4. قسيمة الراتب PDF + كشف «مع التوقيع» (م66 — إلزامي قانوناً).

**P1 — الوحدات التشغيلية**
5. الإجازات: أنواع، أرصدة، طلب/اعتماد، خصم الرصيد، الأجر المرضي المتدرج عبر `YemenLaborRules.SickLeavePay`.
6. الطلبات ومسارات الموافقة والتفويض (`Requests`, `ApprovalFlows`, `Delegations`).
7. الاستئذان + الإقفال الشهري `attendance/monthly` (+ منع الاحتساب بعد الإقفال — موجود في المعالج).
8. العطل الرسمية API، التأمينات `social-insurance-runs`، نهاية الخدمة `end-of-service` (م120).
9. إدارة الصلاحيات (فجوة F-04): `roles/{id}/permissions`، `users/{id}/roles` + شاشة المستخدمين.
10. التقارير `reports/{key}` + تصدير، ونطاق البيانات للمسؤول المباشر (UserDataScopes).

**P2 — التكامل والنشر**
11. خدمة سحب البصمات ZKTeco + `punches/batch` بمفتاح الجهاز.
12. تطبيق Flutter (المرحلة 6 في `pack/05_phase_prompts.md`).
13. Docker Compose (API + SQL Server) + GitHub Actions (build + test + e2e).
14. جدول الجزاءات (م93-95)، دوام رمضان التلقائي، بدل ليلي/نوبات (م57) — مقترحات في `docs/00` §5.2.

**مفتوح مع صاحب النظام:** «الوثيقة الرئيسية» (الأقسام 3-13) غير مستلمة؛ قواعد المحرك الحالية [مستنتجة] — راجع الأسئلة التسعة في `docs/00_التقرير_الشامل.md` §8.

## 8. النشر
- **Cloudflare Pages/Workers لا يشغّل .NET ولا SQL Server.** الخيارات: (أ) استضافة API على VM/حاوية (Azure App Service، Fly.io، VPS) + Azure SQL/SQL Server، والواجهة الثابتة على Cloudflare Pages تشير إلى رابط API (يتطلب CORS)؛ (ب) نقل الخلفية إلى Workers + D1 (إعادة كتابة كاملة — غير موصى به).
- **المنشور الآن (Cloudflare Worker):** https://hrms-moain3.water-juniper-2b8.workers.dev — الإعداد في `deploy/cloudflare/` (`worker.js` يخدم الواجهة ويمرّر `/api/*` و`/health` إلى `API_ORIGIN`).
- ⚠️ **لا يفتح في المتصفح:** حساب `--temporary` يعرض صفحة تحقق Cloudflare (403). البديل الفوري: رابط الساندبوكس المباشر. النشر المُدار `gsk hosted deploy` (ملف `wrangler.jsonc` في الجذر) ينتظر موافقة المالك.
- ⚠️ نُشر بـ `wrangler deploy --temporary` (حساب Cloudflare مؤقت — **ينتهي خلال أيام**)، و`API_ORIGIN` يشير إلى خادم الساندبوكس المؤقت. عند موت الساندبوكس تعمل الواجهة ويتوقف الدخول.
- **لجعله دائماً:** (1) استضف API (.NET + SQL Server) على خادم دائم؛ (2) غيّر `API_ORIGIN` في `deploy/cloudflare/wrangler.toml`؛ (3) `cd deploy/cloudflare && wrangler login && wrangler deploy` على حساب Cloudflare الخاص بك (أو مهارة `cf-byok-deploy`). انسخ `src/Hrms.Api/wwwroot/*` إلى `deploy/cloudflare/public/` عند تحديث الواجهة.
