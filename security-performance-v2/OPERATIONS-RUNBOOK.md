# دليل التشغيل الآمن — مرحلة 11

هذا دليل تنفيذ للمشغّل المخوّل، لا تصريح لتشغيله على production. لا تمرر كلمات مرور أو URLs أو tokens في shell history أو artifacts، ولا تستخدم `dotnet ef database update` من كل web process.

## قبل النشر

1. ثبّت المرجع: `git rev-parse HEAD`، وراجع [FINAL-REPORT.md](FINAL-REPORT.md) و[SIGNOFF.md](SIGNOFF.md).
2. وفّر PostgreSQL وClamAV وOTel collector وفق قرارات البيئة، وصدّق إعدادات `Jwt`، `Cors`، trusted proxy، `Authentication:RefreshTokenTransport` و`DatabaseConnection` من secret store.
3. أنشئ migration-owner منفصلًا ودور runtime محدودًا. راجع [provision-runtime-role.sql](../scripts/provision-runtime-role.sql)، ثم نفّذ preflight المقيد فقط على local أو staging approved. أدخل URL الاتصال في prompt مخفيًا؛ لا تضعه في command line أو shell history:

```bash
(
  read -rsp 'MIGRATION_DATABASE_URL: ' EIAMS_MIGRATION_URL; echo
  export MIGRATION_DATABASE_URL="$EIAMS_MIGRATION_URL"
  unset EIAMS_MIGRATION_URL
  EIAMS_MIGRATION_TARGET_NON_NEON=1 ./scripts/run-migration-preflight.sh
)
```

الأقواس تنشئ subshell، لذلك تزول المتغيرات المصدّرة فور انتهاء الـrunner حتى إن بقيت جلسة المستخدم مفتوحة. يجوز لjob/CI موثوق أن يحقن `MIGRATION_DATABASE_URL` من secret store مباشرة في بيئته بدلاً من prompt؛ لا تطبع القيمة ولا تحفظها في artifact. إن احتوى script المراجع `CREATE INDEX CONCURRENTLY`، استخدم فقط [run-reviewed-migration.sh](../scripts/run-reviewed-migration.sh) مع from/to وملف SQL مراجع؛ لا تستبدله بـidempotent EF script.

## فحص staging

الـharness [run-staging-release-evidence.sh](../scripts/run-staging-release-evidence.sh) لا يعمل افتراضيًا، ويتحقق أن اسمي API وDB يحملان علامة staging، ويرفض أسماء/hosts التي تشير إلى Neon أو production، ويرفض DNS الذي يحل إلى private أو loopback address. يكتب artifact منقحًا في temporary subtree. يتطلب موافقة هدف معزول؛ اجمع القيم عبر prompt خارج history ثم نظف البيئة:

```bash
(
  read -r  -p 'Approved HTTPS API origin: ' EIAMS_STAGING_API_URL
  read -r  -p 'Approved API host: ' EIAMS_STAGING_APPROVED_HOST
  read -r  -p 'Approved DB host: ' EIAMS_STAGING_DB_HOST
  read -r  -p 'Approved DB host (confirmation): ' EIAMS_STAGING_APPROVED_DB_HOST
  read -r  -p 'Staging DB name: ' EIAMS_STAGING_DB_NAME
  read -r  -p 'Staging DB user: ' EIAMS_STAGING_DB_USER
  read -rsp 'Staging DB password: ' EIAMS_STAGING_DB_PASSWORD; echo
  read -r  -p 'Staging administrator email: ' EIAMS_STAGING_ADMIN_EMAIL
  read -rsp 'Staging administrator password: ' EIAMS_STAGING_ADMIN_PASSWORD; echo
  export EIAMS_STAGING_API_URL EIAMS_STAGING_APPROVED_HOST EIAMS_STAGING_DB_HOST \
    EIAMS_STAGING_APPROVED_DB_HOST EIAMS_STAGING_DB_NAME EIAMS_STAGING_DB_USER \
    EIAMS_STAGING_DB_PASSWORD EIAMS_STAGING_ADMIN_EMAIL EIAMS_STAGING_ADMIN_PASSWORD
  export EIAMS_STAGING_EVIDENCE_DIR=/tmp/eiams-staging-evidence
  RUN_STAGING_RELEASE_EVIDENCE=1 ./scripts/run-staging-release-evidence.sh
)
```

الأقواس تحصر كل القيم المصدّرة في subshell؛ تزول بعد انتهاء الـrunner حتى لو ظلّت الجلسة الأصلية مفتوحة. هذا المسار read-only preflight ما لم تُفعّل mutation gate والتأكيد النصي والحساب/adjustment disposable المحدد؛ لا تفعّلها بلا نافذة تغيير. لا يثبت الحارس سلوك forwarded-header/proxy من black-box، ولا يوجد دليل live run مسجل في هذا التسليم. يمكن للـCI/job المصرّح حقن نفس المتغيرات من secret store في بيئة العملية بدلاً من prompts؛ لا تمررها كوسائط CLI.

## الأداء والاستعادة

- الشيك الأسبوعي المقيد يعتمد على binaries Release لأن script يستعمل `--no-build`. نفّذ أولًا `dotnet build CleanArchitectureTemplate.slnx --configuration Release`، ثم اجعل CI/job يحقن `GITHUB_SHA` ويمرر output directory إلى [run-weekly-performance-checks.sh](../scripts/run-weekly-performance-checks.sh). يتحقق من TRX وJSON المنقح، وهو Quick evidence لا soak ممثلًا.
- Full soak يتطلب `RUN_API_SOAK_SUITE=1` و`EIAMS_API_SOAK_PROFILE=FullSoak` و`EIAMS_ALLOW_LONG_SOAK_TEST=1` وdataset Medium/Large وrate/caps صريحة. راجع [BEFORE-AFTER.md](BEFORE-AFTER.md) قبل التشغيل.
- تمرين restore المحلي فقط يحتاج opt-in وأهداف RPO/RTO معلنة؛ راجع [backup-restore-rehearsal.sh](../scripts/backup-restore-rehearsal.sh). لا يعني نجاحه أن backup production صالحًا.

## تشغيل ومراقبة وrollback

- تحقق من `/api/v1/health`, `/api/v1/health/live`, `/api/v1/health/ready` عبر HTTPS بعد النشر. لا تكشف `/metrics` أو diagnostics علنًا.
- لا توجد عتبات تنبيه معتمدة بعد: [observability-alert-contract.json](../config/observability-alert-contract.json) معطّل وthresholds `null`. عيّن owner وSLO وretention ثم اربط كل alert بإجراء.
- عند خلل: أوقف rollout، احتفظ بالـartifacts المنقحة، استخدم rollback للتطبيق المتوافق فقط. لا تسقط migrations أو تحذف sessions/audit/files لاستعادة الخدمة. استعادة DB والمرفقات تتطلب runbook/restore مثبتًا على البيئة الفعلية.
