# كتاب مشروع HRMS — نظام شؤون الموظفين (المرجع الكامل للتسليم)

> آخر تحديث: 2026-11-09 09:00 · المستودع: https://github.com/alabasi2025/moain3 (فرع `main`)
> هذا الكتاب يجمع كل شيء في مكان واحد. التفاصيل الدقيقة في الملفات المشار إليها.

---

## الفصل 1 — الفكرة والنطاق
- **الهدف:** إعادة بناء «نظام شؤون الموظفين» القديم (أنظمة الجزيرة) بتقنية حديثة، متوافق مع **قانون العمل اليمني 5/1995** وقانون التأمينات 26/1991.
- **المدخلات:** دليل المستخدم القديم (27 صفحة صور) في `source/original/` ومستخرجه في `source/manual_pages/`، وحزمة البناء `pack/` (81 جدولاً، 173 مساراً، 248 صلاحية، 7 أدوار).
- **التقنية:** ASP.NET Core 8 + Dapper + SQL Server 2022 + واجهة ويب عربية RTL (ملف واحد) + xUnit + اختبارات E2E بايثون.

## الفصل 2 — ما أُنجز (مُتحقَّق منه بالتشغيل)
| البند | النتيجة |
|---|---|
| تحليل الدليل والحزمة وتدقيقها | `docs/00_التقرير_الشامل.md` (11 عيباً موثقاً، مطابقة قانونية، فجوات) |
| تشغيل الحزمة على SQL Server فعلي | اكتُشف أن `01_schema.sql` يفشل بصمت دون `sqlcmd -I` |
| إصلاحات قاعدة البيانات + قيم القانون اليمني | `fixes/06_fixes.sql` (+ `usp_ApplyYemenLaborDefaults` بـ 52 إعداداً) |
| البناء | 0 أخطاء، 0 تحذيرات |
| اختبارات الوحدة | 77/77 |
| E2E عام | 36/36 (`tools/e2e_smoke.py`) |
| E2E رواتب | 35/35 (`tools/e2e_payroll.py`) |

## الفصل 3 — الوصول والتجربة
| الشركة | المستخدم | كلمة المرور | الدور |
|---|---|---|---|
| `C1` | `admin` | `1` | مدير نظام |
| `C1` | `user` | `1` | مدير موارد بشرية |
- ⚠️ تجريبي فقط (`fixes/08_demo_users.sql`). احذفه قبل الإنتاج.

### روابط المعاينة وحالتها (بصدق)
| الرابط | الحالة | السبب |
|---|---|---|
| رابط الساندبوكس المباشر `https://5001-izoiemgz6j8c8qr1g43qk-3c7ff1b5.sandbox.novita.ai` | ✅ يعمل في المتصفح الآن | يموت عند انتهاء الساندبوكس |
| **https://1a16c8af-b624-4e6f-aff8-6a14c2400dfd.vip.gensparksite.com** (Genspark Hosted على Cloudflare) | ✅ **يعمل في المتصفح** — الرابط الرسمي | الواجهة على Cloudflare؛ الـAPI يمر إلى الساندبوكس |
| `https://hrms-moain3.water-juniper-2b8.workers.dev` (حساب Cloudflare مؤقت) | ❌ صفحة تحقق «Just a moment…» (403) للمتصفحات | لا تستخدمه |


### ✅ النشر الذي يعمل (Genspark Hosted على Cloudflare) — https://1a16c8af-b624-4e6f-aff8-6a14c2400dfd.vip.gensparksite.com
- يعمل في المتصفح (تحقق بمتصفح حقيقي: الصفحة تُحمَّل بلا أخطاء، والدخول `admin`/`1` و`user`/`1` ناجح عبره).
- الإعداد: `wrangler.jsonc` في الجذر → `deploy/cloudflare/worker.js` يخدم الواجهة من `deploy/cloudflare/public/` ويمرّر `/api/*` و`/health` إلى `API_ORIGIN`.
- **الدورة:** `git commit` ← `gsk hosted deploy` ← يعيد `pending_approval` ← المالك يوافق (أو `gsk hosted action_approve --id <id>` بعد تأكيده في المحادثة) ← `gsk hosted action_wait --id <id>` حتى `completed` ← `gsk hosted worker_get` للرابط. الرابط ثابت عبر إعادات النشر.
- **شروط النجاح (من تجربة وكيل آخر):** جذر صغير ونظيف، **لا symlinks** (`Source tree contains a symlink`)، لا KV/Durable Objects/cron، D1 وR2 واحد كحد أقصى. عند الفشل: `gsk hosted action_status --id <id>` واقرأ `result.first_error_line`.
- ❌ الرابط القديم `https://hrms-moain3.water-juniper-2b8.workers.dev` (حساب `--temporary`) يعرض صفحة تحقق Cloudflare للمتصفحات — لا تستخدمه.
- ⚠️ الواجهة على Cloudflare، لكن `API_ORIGIN` ما زال يشير إلى خادم الساندبوكس المؤقت؛ عند موته يتوقف الدخول. للديمومة: استضف .NET + SQL Server على خادم دائم، غيّر `API_ORIGIN` في `wrangler.jsonc`، ثم أعد الدورة أعلاه.

**حقيقة تقنية مهمة:** Cloudflare لا يشغّل .NET ولا SQL Server. أي رابط Cloudflare يعرض الواجهة ويمرّر `/api/*` إلى خادم الـAPI (`API_ORIGIN`). لكي يعمل دائماً يجب استضافة الـAPI والقاعدة على خادم دائم (VPS / Azure App Service + Azure SQL / Fly.io)، ثم تغيير `API_ORIGIN` في `wrangler.jsonc` و`deploy/cloudflare/wrangler.toml`.

## الفصل 4 — البنية وخريطة الكود
```
pack/            الحزمة الأصلية (لا تُعدَّل)
fixes/           06 إصلاحات + قانون يمني · 07 تحقق سلبي · 08 حسابات تجريبية
src/Hrms.Domain/ Attendance (محرك الحضور) · Payroll (محرك الرواتب) · Labor (قواعد القانون اليمني)
src/Hrms.Api/    Security · Common · Data · Features/{Auth,Definitions,Org,Admin,Employees,Attendance,Payroll} · wwwroot (الواجهة)
tests/           اختبارات الوحدة
tools/           setup_env.sh · e2e_smoke.py · e2e_payroll.py · run_build_agent.py · test_proxy_models.py · audit.py
deploy/cloudflare/  worker.js (واجهة + تمرير API) · wrangler.toml · public/
docs/            00 التقرير الشامل · 02 مواصفة المحرك · 03 حالة التنفيذ · هذا الكتاب
analysis/        مخرجات الوكلاء الخام · قائمة نماذج البروكسي · سجلات SQL
```

## الفصل 5 — القواعد المنفّذة (مختصر)
- **الحضور:** نوافذ البصمة، السماح، التأخير، الخروج المبكر، الغياب، الاستئذان، العمل الإضافي (سقف 12 ساعة م74)، الفترات الليلية، رمضان 6 ساعات (م71)، الجمعة راحة (م77)، العطل (عمل فيها ×2 م56).
- **الرواتب:** الأجر اليومي = الأساسي/30 (م9)، الإضافي 1.5× و2× من الأساسي (م56)، تأمين الموظف 6% وصاحب العمل 9%، سقف خصم العهدة 25% (م64)، منع الصافي السالب.
- **قانون العمل:** سنوية 2.5/شهر وترحيل النصف (م79)، مرضية متدرجة 100/85/75/50% (م80)، حج بعد 4 سنوات (م84)، وضع 60+20 (م45)، إشعار 30/15/7 (م38)، جزاء ≤20% ومهلة 15 يوماً (م93-94)، نهاية خدمة لغير المؤمَّن شهر/سنة (م120).
- **قاعدة الدليل ص22:** لا ربط بفترة دوام قبل «مباشرة العمل».

## الفصل 6 — كيف تكمل في بيئة جديدة
```bash
git clone https://github.com/alabasi2025/moain3.git && cd moain3
bash tools/setup_env.sh        # SQL Server + قاعدة + بيانات + .NET 8 + build + test
source /tmp/.hrms_env && ASPNETCORE_URLS=http://0.0.0.0:5001 setsid -f dotnet src/Hrms.Api/bin/Debug/net8.0/Hrms.Api.dll > /tmp/api.log 2>&1 < /dev/null
HRMS_PASSWORD=1 python3 tools/e2e_smoke.py
HRMS_PASSWORD=1 HRMS_SQL_PASSWORD="$(cat /tmp/.sapw)" python3 tools/e2e_payroll.py
```
المزالق المعروفة والحلول: `HANDOFF.md` §4.

## الفصل 7 — الوكلاء
- البروكسي فيه 81 نموذجاً (75 تعمل): `analysis/proxy_models/MODELS.md`.
- المستخدمة: `claude-opus-5-5` (الحضور، الدخول، التعريفات، الرواتب API)، `claude-fable-5-1` (محرك الرواتب، القانون، الواجهة)، `gpt-6-astra` (الأمان، الموظفين، الحضور API).
- **استخدم البث دائماً** وإلا خطأ 524. **لا تدمج كود وكيل قبل البناء والاختبار الحي.**

## الفصل 8 — الباقي بالأولوية
**P0:** بنود راتب الموظف API · السلف والعهد والمتغيرات API · شاشة الرواتب · قسيمة PDF وكشف «مع التوقيع» (م66).
**P1:** الإجازات والأرصدة · الطلبات والموافقات والتفويض · الاستئذان والإقفال الشهري · العطل · التأمينات · نهاية الخدمة · إدارة الصلاحيات (F-04) · التقارير ونطاق البيانات.
**P2:** سحب بصمات ZKTeco · تطبيق Flutter · Docker Compose + CI · الجزاءات ودوام رمضان التلقائي والبدلات (م57).
**P0 للنشر:** استضافة الـAPI والقاعدة على خادم دائم ثم ربط Cloudflare به.
**مفتوح مع صاحب النظام:** «الوثيقة الرئيسية» (الأقسام 3-13) — القواعد الحالية مستنتجة.

## الفصل 9 — فهرس الوثائق
`HANDOFF.md` (تسليم تقني) · `README.md` · `docs/00` (التحليل) · `docs/02` (مواصفة المحرك) · `docs/03` (حالة التنفيذ) · `pack/00_README.md` (الحزمة) · `analysis/proxy_models/MODELS.md` (النماذج).
