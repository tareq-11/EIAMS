# تسليم عقد الواجهة — Security & Performance V2

لا يتضمن هذا التسليم أي تعديل frontend. المصدر الملزم هو [OpenAPI v1](../contracts/openapi/eiams-backend-v1.openapi.json) (132 paths، 170 operations، 279 schemas عند المراجعة) ومصدره [provenance](../contracts/openapi/eiams-backend-v1.provenance.json). هذا الملحق self-contained لعقد المرحلة 11 ولا يعتمد على handoff محلي غير متتبع.

## العقد الثابت الآن

- استخدم بادئة `/api/v1`: أمثلة auth هي `POST /api/v1/auth/login` و`/refresh` و`/logout` و`GET /api/v1/auth/session`. الـcontrollers تُضاف إليها البادئة بواسطة `ApiVersionRouteConvention`، وcookie path هو `/api/v1/auth`.
- اعتمد envelope النجاح/الخطأ و`request_id` كما في OpenAPI؛ لا تعتمد Problem Details القديم.
- المستخدم يملك Role واحدًا وScope ثابتًا؛ لا active-scope switcher ولا إرسال scope عام مع كل request.
- pagination تبدأ من 1، وpermission codes تستعمل `:`. استخدم OpenAPI لتفاصيل casing/DTO لكل operation.
- login/refresh يضعان refresh cookie HttpOnly، مع `Secure` على HTTPS و`SameSite=Strict`. أرسل `withCredentials: true` ولا تسجل tokens.

## انتقال refresh المتدرج

الإعداد الحالي متوافق مع العميل القديم: `AllowRequestBody=true` و`IncludeInResponseBody=true`. body token له أولوية صريحة أثناء الفترة الانتقالية؛ cookie request يرفض `Sec-Fetch-Site: cross-site` أو Origin غير مسموح.

لا تفترض أن cookie-only فُعّل. قبل تغيير الإعداد إلى `AllowRequestBody=false` و`IncludeInResponseBody=false`، يجب أن يسلّم الفرونت:

1. refresh/logout body فارغ مع `withCredentials`، single-flight عند 401، ومرة retry واحدة فقط.
2. اختبار same-origin وOrigin/cross-site rejection وlogout/refresh/session.
3. إزالة refresh token من state persistent/localStorage/logs/telemetry.
4. موافقة مشتركة على origins المنشورة ثم اختبار staging HTTPS فعلي.

لا يوجد دليل frontend integration أو live staging في هذه الجولة؛ لا تغيّر default في backend اعتمادًا على هذه الوثيقة فقط.
