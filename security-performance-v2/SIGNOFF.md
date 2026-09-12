# سجل التوقيع والبوابات — مرحلة 11

الحالة: **غير موقّع للإنتاج (Pending operational sign-off)**. هذا قرار توثيقي مبني على الأدلة الحالية، لا رفض للكود.

| البوابة | الحالة | الدليل/المالك المطلوب |
|---|---|---|
| مراجعة الكود وmigrations | جزئي | `main...d4866e5`: 75 commits؛ المرجع المراجع `d4866e5` قبل commit الوثائق. يلزم reviewer release اعتمادًا للـdiff وmigration order. |
| Build/tests وworkflow | دليل محلي حديث متاح | 2026-09-12: Release build 0 warnings/errors وRelease publish نجح؛ unit 540/540؛ architecture 13/13؛ integration 591 passed و14 intentionally gated skipped من 605، 0 failures؛ staging safety 8/8؛ EF `No changes...` بـdotnet-ef 10.0.9؛ `validate-ci-workflow.sh` تحقق من عقدي CI/weekly؛ NuGet audit بنفس منطق workflow نجح لـ8 projects بلا vulnerabilities أو audit problems. ليست نتائج CI بعيد ولا نشرًا لبيئة، ولم يُشغّل Gitleaks محليًا ضمن هذه الأدلة. الجولة الأولى كشفت shadowing ثم أصلحها `d4866e5` وأعيدت المجموعة. مسؤول: CI owner. |
| Security matrix وmixed workload | منفذ محليًا؛ إعادة إثبات تشغيلية معلقة | دخلت actor matrix وtwo-host mixed workload ضمن IntegrationTests المحلية الناجحة (591 passed، 0 failures). يلزم فقط إعادة الإثبات على staging أو deployed release candidate وتسجيل artifact منقح. مسؤول: security owner. |
| 60–120m soak وbefore/after | مفتوح | Quick 12s فقط وself-comparison comparator فقط. مسؤول: performance owner. |
| SLO/alerts/telemetry retention | مفتوح | thresholds كلها null في العقد المعطّل. مسؤول: SRE/operations owner. |
| KDF/MFA | مفتوح | لا قرار إنتاجي أو MFA policy للمدير. مسؤول: security/product owner. |
| staging TLS/proxy/runtime role | مفتوح | harness guarded موجود، بلا live artifact. مسؤول: deployment owner. |
| backup/restore | مفتوح | local guarded rehearsal موجود، بلا live restore وRPO/RTO business agreement. مسؤول: data owner. |
| Frontend refresh transition | مفتوح | العقد متوافق حاليًا؛ cookie-only يحتاج تسليم واختبار frontend. مسؤول: frontend owner. |
| representative dataset/index decisions | مفتوح | Small evidence ليس أساس index/KDF/capacity decision. مسؤول: performance/data owner. |

## شروط التوقيع

يُستبدل هذا السجل بتوقيع release فقط بعد إرفاق مراجع artifacts المنقحة للـCI وstaging وsoak/restoration، وتوثيق قبول/إغلاق كل بند مفتوح أعلاه، وتسمية مالك وتاريخ لكل risk. لا توقع عبارة «خالية من الأخطاء»؛ المعيار هو تحقق بوابات محددة وقابلية rollback/restore.
