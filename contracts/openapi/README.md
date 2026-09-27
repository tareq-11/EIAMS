# EIAMS Backend OpenAPI

`eiams-backend-v1.openapi.json` هو العقد المعتمد والمولّد من الباكيند المنفذ فعلياً.

يجب نسخ العقد مع ملف `eiams-backend-v1.provenance.json` إلى الفرونت، والتحقق من البصمة قبل توليد TypeScript types.

العقد الحالي يحتوي على 134 path و172 operation و292 schema، ولا توجد أي استجابة نجاح `2xx` بلا `content/schema`.

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
