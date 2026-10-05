# EIAMS Backend OpenAPI

`eiams-backend-v1.openapi.json` هو العقد المعتمد والمولّد من الباكيند المنفذ فعلياً.

يجب نسخ العقد مع ملف `eiams-backend-v1.provenance.json` إلى الفرونت، والتحقق من البصمة قبل توليد TypeScript types.

العقد الحالي يحتوي على 133 path و171 operation و291 schema، ولا توجد أي استجابة نجاح `2xx` بلا `content/schema`.

الـ`Role.nameAr` وحقول `UserSessionRoleDto.nameAr` أضيفت في `AddRoleNameAr` (2026-10-04) كحقول مطلوبة. أي عميل يرسل `POST /api/v1/admin/roles` أو `PUT /api/v1/admin/roles/{roleId}` بدون `nameAr` يستلم `400 REQUEST_VALIDATION_FAILED`.

## Role metadata concurrency (Tranche B)

`AddRoleRowVersion` (2026-10-04) أضاف `roles.row_version` و`rowVersion` إلى استجابة الدور.

- `PUT /api/v1/admin/roles/{roleId}` يتطلب الآن `expectedRowVersion` (مطلوب). إغفال الحقل
  يستلم `400 REQUEST_VALIDATION_FAILED` و`details.body` (رسالة ربط نموذج عامة، لا اسم حقل)،
  وليس `0` صامتاً.
- `expectedRowVersion` لا يطابق المخزَّن ⇒ `409` والرمز على السلك
  `ROLES_ROW_VERSION_MISMATCH` (النطاق داخلياً `Roles.RowVersionMismatch`؛ المظروف
  ينشره UPPER_SNAKE_CASE).
- استجابة `200` لم تعد `EmptyResponse`، بل projection الدور كاملة مع `rowVersion` الجديد.
- استجابة `POST` تبقى `ResourceIdResponse{id}` فقط، مع `rowVersion = 1`؛ انظر
  RESOLUTION-027 §17 الذي يطلب projection كاملة عند الإنشاء (غير منفَّذ بعد).
- مخططا `Application.Roles.GetList.RoleResponse` و`Application.Roles.GetById.RoleResponse`
  دُمجا في `Application.Roles.RoleResponse` واحد.

## استبدال صلاحيات الدور (Tranche D)

رُسي actualizaciones S15/S16/S17/S20/S21/S23 من RESOLUTION-027 (2026-10-05).

- `PUT /api/v1/admin/roles/{roleId}/permissions` يستبدل مجموعة الصلاحيات كاملة باستخدام
  الأكواد النقطية، ويتطلب `expectedRowVersion` (مطلوب)، ويعيد `Application.Roles.RoleResponse`
  مع `rowVersion` الجديد.
- **حُذفت** نقطتا `POST /api/v1/admin/roles/{roleId}/permissions` و
  `DELETE /api/v1/admin/roles/{roleId}/permissions/{permissionId}` (S23). بقى
  `GET /api/v1/admin/roles/{roleId}/permissions` لأنه قراءة.
- **`rowVersion` واحد مشترك**: كل من تحديث البيانات الوصفية واستبدال الصلاحيات يقدّم
  الإصدار نفسه. `{permissionCodes, expectedRowVersion}`.
- كل استبدال **كامل أو لا شيء**: أي كود غير معروف، أو كود لا تتقاطع نطاقاته المسموحة مع
  `allowedScopeTypes` للدور، يُرفض كخطأ `400 ROLES_UNKNOWN_PERMISSION_CODES` أو
  `400 ROLES_PERMISSION_CODES_NOT_ALLOWED_FOR_ROLE_SCOPES` قبل كتابة أي شيء. هذا يمنع
  منح صلاحية صامتة عديمة الأثر.
- `POST /api/v1/admin/roles` صار يقبل `permissionCodes` (مطلوب) وينشئ الدور وصلاحياته في
  معاملة واحدة، ويعيد **`201 Created`** مع `RoleResponse` كاملة بدل `ResourceIdResponse`.
  هو الاستدعاء الوحيد في الـAPI الذي لا يُرجع `{id}` الوحيد، والوحيد الذي يُرجع `201`.
- `Application.Roles.RoleResponse` يحمل `permissionCodes` في كل القراءات ونتائج الكتابة.
- `Application.Permissions.GetList.PermissionResponse` يحمل `allowedScopeTypes` لعرض سبب
  تعطيل كود في مصفوفة الصلاحيات.

**BREAKING**: `POST /admin/roles` بدون `permissionCodes` ⇒ `400`. `PUT /admin/roles/{id}`
بدون `expectedRowVersion` ⇒ `400`. نقطتا POST/DELETE للصلاحيات لم تعودا موجودتين.

## تسميات الصلاحيات العربية (Tranche E)

`AddPermissionArabicLabels` (2026-10-05) أضاف `name_ar` و`description_ar` إلى `permissions`،
وكشفهما في `PermissionResponse` (الفهرس وقراءة صلاحيات الدور).

- `name_ar` **مطلوب**، بحد أقصى 200، مع `ck_permissions_name_ar_not_blank`. عربي لكل
  الأكواد الـ29 (عرض الأدوار، ترحيل السند، إدخال الجرد، …) ومتوافق مع مفردات الواجهة
  الحالية (السند، ترحيل، مراجعة، عكس).
- `description_ar` اختياري، بحد أقصى 500.
- أزل الـ38 صفاً colon القديم من بذرة EF. كانت `CutoverToDottedOnlyPermissionVocabulary`
  تحذفها بـSQL خام، ونصّ `Down` ينص على قرار عدم إعادة إنشائها، بينما ما زالت
  البذرة تصرّح بها: النموذج كان يدّعي 67 صفاً وقاعدة البيانات فيها 29. أي `InsertData`
  مستقبلي على هذا الجدول كان سيُعيد أكواداً حذفها القطع عمداً. `PermissionCodeMapping`
  يحتفظ بالتاريخ.
- عمود `name_ar` **بلا DEFAULT** بعد الترحيل: الـDEFAULT الذي تركه `AddColumn` كان `''`
  وهو يناقض قيد عدم الفراغ، فكان أي إدخال يتجاهل العمود يفشل بخطأ مربك بدل خطأ NOT NULL
  واضح. والقيد نفسه يحمي الصفوف غير المزروعة عبر backfill بالرمز.

**BREAKING**: أي إدخال مباشر على `permissions` يجب أن يمرّر `name_ar`، وإلا فشل بخطأ NOT
NULL. `Permission.Create` صار يتطلب `nameAr`.

تم توليده من `IntegrationTestWebAppFactory` في بيئة Development المعزولة بواسطة
`OpenApiSnapshotTests`؛ شغّل `UPDATE_OPENAPI_SNAPSHOT=1 dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~OpenApiSnapshotTests` لإعادة التوليد والتحقق من drift.

مسارات الـsnapshot الحالية هي المرجع authoritative، وتشمل prefix `/api/v1` مثل
`/api/v1/auth` و`/api/v1/admin` و`/api/v1/catalog`. لا تستخدم المسارات القديمة
غير المسبوقة مثل `/auth` أو `/admin` أو `/catalog`.

## Complete warehouse-document drafts

`POST /api/v1/warehouse-documents/complete-draft` creates the header, lines, and type-specific
details atomically and returns the full authoritative document response. It requires warehouse-scoped
`document.create`, `document.update`, and `document.view` permissions; the request supports Receiving,
Issue, Transfer, Return, Opening, and Quantity Adjustment. Disposal adjustments are not supported by
this aggregate endpoint because they require specialized asset-lifecycle validation. An optional GUID
`Idempotency-Key` header replays the original response for the same canonical body and conflicts if
the body changes. The key record and aggregate data share the same transaction, so failed requests
can be retried with that key.

Issue/Return serialized-asset lines provide `assetIds`, exactly one per persisted base unit. Existing
asset eligibility and authorization rules validate each selection. Asset selections are rejected for
other document types. Binary attachments remain separate calls after draft creation and are not part
of the idempotency body.
