# قبل/بعد — Security & Performance V2

## قاعدة القراءة

هذه الوثيقة تسجل تغييرًا قابلًا للمراجعة، لا baseline تاريخيًّا مصطنعًا. لا يوجد زوج مستقل متكافئ من قياسات قبل/بعد على البيئة والـdataset نفسيهما؛ لذلك لا توجد نسبة تحسن أداء معتمدة.

| المجال | قبل التغيير/المشكلة | بعد التغيير المثبت | دليل أو قيد |
|---|---|---|---|
| Forwarded headers | غياب trusted peers كان يسمح بمسار غير آمن | تُعطّل المعالجة بلا proxy موثوق، مع حد hop وإعداد مقيد | اختبارات hardening؛ يلزم إثبات proxy حي. |
| Sessions | سباقات refresh/logout/suspend ونموذج token غير مهيأ للانتقال | locks حتمية، SessionId، rotation/replay controls، cookie transport متدرج | PostgreSQL integration؛ access token لا يُبطل فور logout. |
| Authorization cache | invalidation قبل commit وخطر نسخة محلية قديمة | invalidation بعد commit + authorization version داخل PostgreSQL | يثبت صحة نسختين فوق DB واحدة؛ ليس بديلًا عن قرار limiter موزع. |
| Read paths | pagination/aggregation قد تنفذ عملًا زائدًا | SQL pagination مستقرة، حدود query، dashboard أقل round trips | correctness/query-shape على Testcontainers، لا قياس dataset ممثل. |
| Writes | replay وrollback/worker hazards | idempotency، transaction barriers، rollback/worker guards | لا retry policy عام حتى الآن. |
| Files | signature لا تعني malware-free | ClamAV Required fail-closed وscope checks | يتطلب ClamAV منشورًا وقرار legacy re-scan. |
| CI/operability | checks أقل صرامة | PR lanes، SBOM، audit JSON، gitleaks، scripts guarded | لا يكفي ذلك لإثبات staging أو production. |

## القياس الوحيد المتاح

QuickValidation المنفذ في 2026-09-11 هو **بعد-only harness evidence**: 12 ثانية، Small/Testcontainers، Kestrel loopback HTTP، concurrency 2، 2,042 successful من 2,042، 170.17 req/s، p50/p95/p99 = 25/25/50ms، ولا unexpected HTTP أو rate limits أو timeouts أو transport/pool timeouts. recovery اجتاز، وعينات lock-wait كانت 412 بلا انتظار مرصود. هذه النتائج لا تمثل TLS أو proxy أو شبكة بعيدة أو Neon/production أو حملًا طويلًا.

أنتج comparator نتيجة `no_threshold_regression_detected` (عتبة latency/throughput 10%)، لكنه استخدم قيم baseline/candidate متطابقة؛ هو إثبات أن parser والمقارنة يرفضان regression وفق العقد، **وليس** إثبات أن تغييرًا حسّن الأداء.

## بروتوكول before/after المطلوب

1. اعتمد workload وSLO وMedium/Large dataset وseed وrunner وثبّت commit baseline وcandidate.
2. شغّل `FullSoak` مرتين مستقلتين على الأقل لكل جانب، لمدة 60–120 دقيقة، وبـTLS/proxy إن كانا جزءًا من النشر.
3. احفظ artifacts المنقحة خارج Git، ثم شغّل comparator على زوج متكافئ فقط. لا تخلط `IsolatedRequestCost` و`FullBackgroundWorkload` ولا بيئات مختلفة.
4. فسّر failures/429/timeout/pool saturation وmemory/lock observations، ولا تعلن تحسنًا من percentile واحد أو من self-comparison.

إعدادات الحراسة والعقد موجودة في `tests/IntegrationTests/Performance/ApiSoakSuiteConfiguration.cs` و`ApiSoakArtifactComparison.cs`، والتشغيل الآمن موثق في [OPERATIONS-RUNBOOK.md](OPERATIONS-RUNBOOK.md).
