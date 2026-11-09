# HRMS — تحليل وتجهيز نظام شؤون الموظفين

> 🔁 **للمتابعة في بيئة أو وكيل جديد: اقرأ [`HANDOFF.md`](HANDOFF.md) أولاً.** إعادة البيئة: `bash tools/setup_env.sh`. حسابات التجربة: `admin`/`1` و `user`/`1` (الشركة `C1`).

مستودع تحليل وتجهيز لإعادة بناء «نظام شؤون الموظفين» (أنظمة الجزيرة) على حزمة البناء `hrms_pack`، مع مطابقة قانون العمل اليمني 5/1995.

**ابدأ من:** [`docs/00_التقرير_الشامل.md`](docs/00_التقرير_الشامل.md) ثم [`docs/03_حالة_التنفيذ.md`](docs/03_حالة_التنفيذ.md)

## الهيكل
| المسار | المحتوى |
|---|---|
| `docs/00_التقرير_الشامل.md` | النتائج، سجل العيوب، المطابقة القانونية، الفجوات، أسئلة العميل، الجاهزية |
| `docs/02_مواصفة_محرك_الحضور_والرواتب.md` | مواصفة بديلة [مستنتج] للمحرك وحالات القبول ومثال راتب محقق |
| `pack/` | حزمة البناء الأصلية كما هي (01..05) — **دون تعديل** |
| `fixes/06_fixes.sql` | إصلاحات idempotent + `hr.usp_ApplyYemenLaborDefaults` |
| `fixes/07_verify.sql` | اختبارات سلبية للقيود |
| `source/` | استخراج الدليل صفحة بصفحة + نص قانون العمل الرسمي |
| `analysis/` | الفحص الآلي، سجل تشغيل SQL Server، مخرجات الوكلاء الأربعة الخام |
| `tools/` | سكربتات الاستخراج والفحص والوكلاء والتحقق |

## ترتيب التشغيل (تحقق على SQL Server 2022)
```bash
sqlcmd -I -d HRMS -i pack/01_schema.sql      # -I إلزامي (QUOTED_IDENTIFIER) وإلا تفشل الفهارس المفلترة بصمت
sqlcmd -I -d HRMS -i pack/02_seed.sql
sqlcmd -I -d HRMS -i fixes/06_fixes.sql
sqlcmd -I -d HRMS -i fixes/08_demo_users.sql   # تجريبي فقط: admin/1 و user/1
# لكل شركة:
#   INSERT hr.Companies(Code,NameAr) VALUES(...);
#   EXEC hr.usp_SeedCompanyDefaults     @CompanyId = <id>;
#   EXEC hr.usp_ApplyYemenLaborDefaults @CompanyId = <id>;
sqlcmd -I -d HRMS -i fixes/07_verify.sql     # على قاعدة تجريبية فقط
python3 tools/audit.py                        # من داخل pack/
python3 tools/check_cases.py
```


## التطبيق (.NET 8) — بُني بالوكلاء الثلاثة عبر مفتاح البروكسي
| الوحدة | الوكيل | الملفات |
|---|---|---|
| محرك الحضور اليومي (دالة نقية) | `claude-opus-5-5` | `src/Hrms.Domain/Attendance/*` |
| الأمان: JWT، صلاحيات `perm:*`، PBKDF2، AES-256-GCM، RFC7807 | `gpt-6-astra` | `src/Hrms.Api/Security/*`, `Common/*` |
| محرك الرواتب + قواعد قانون العمل اليمني | `claude-fable-5-1` | `src/Hrms.Domain/Payroll/*`, `Labor/*` |
| الدخول والتدوير، التعريفات، الهيكل الإداري | `claude-opus-5-5` | `src/Hrms.Api/Features/{Auth,Definitions,Org,Admin}` |
| ملف الموظف، الفترات، الربط، البصمات، احتساب الحضور | `gpt-6-astra` | `src/Hrms.Api/Features/{Employees,Attendance}` |
| كشوف الرواتب (احتساب/اعتماد/عكس/صرف) | `claude-opus-5-5` | `src/Hrms.Api/Features/Payroll` |
| واجهة الويب العربية RTL | `claude-fable-5-1` | `src/Hrms.Api/wwwroot/index.html` |

مخرجات الوكلاء الخام والبرومبتات: `analysis/agents/build/`.

### التشغيل
```bash
# 1) قاعدة البيانات (انظر الأعلى): 01 -I ، 02 ، 06 ، ثم usp_SeedCompanyDefaults و usp_ApplyYemenLaborDefaults لشركة Code='C1'
# 2) الأسرار عبر متغيرات البيئة فقط (لا شيء في المستودع):
export ConnectionStrings__Hrms='Server=localhost;Database=HRMS;User Id=...;Password=...;TrustServerCertificate=True'
export Jwt__Key="$(openssl rand -base64 48)"
export Encryption__Key="$(openssl rand -base64 32)"      # 32 بايت لـ AES-256
export Bootstrap__AdminPassword='...'                     # ينشئ admin مرة واحدة إن لم يوجد مستخدم
dotnet run --project src/Hrms.Api --urls http://0.0.0.0:5001
# 3) الاختبارات
dotnet test                                               # اختبارات الوحدة
HRMS_PASSWORD=1 python3 tools/e2e_smoke.py              # اختبار طرف-لطرف على القاعدة الحية
```

## أهم ما يجب معرفته
1. «الوثيقة الرئيسية» التي تحيل إليها الحزمة **غير مرفقة** ويجب طلبها.
2. `01_schema.sql` يفشل جزئياً بصمت بدون `sqlcmd -I`.
3. قيم البذرة للإجازات تخالف القانون اليمني (المرضية 30 يوماً بدل 8 أشهر متدرجة، الحج والعدة مفقودتان) — مصححة في 06.
4. نسب التأمينات (9%/6%) من قانون 26/1991 — تحقق من السارية قبل الإنتاج.
