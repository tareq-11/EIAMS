# EIAMS Backend OpenAPI

`eiams-backend-v1.openapi.json` هو العقد المعتمد والمولّد من الباكيند المنفذ فعلياً.

يجب نسخ العقد مع ملف `eiams-backend-v1.provenance.json` إلى الفرونت، والتحقق من البصمة قبل توليد TypeScript types.

العقد الحالي يحتوي على 134 path و172 operation و280 schema، ولا توجد أي استجابة نجاح `2xx` بلا `content/schema`.

تم توليده من `IntegrationTestWebAppFactory` في بيئة Development المعزولة بواسطة
`OpenApiSnapshotTests`؛ شغّل `UPDATE_OPENAPI_SNAPSHOT=1 dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~OpenApiSnapshotTests` لإعادة التوليد والتحقق من drift.

مسارات الـsnapshot الحالية هي المرجع authoritative، وتشمل prefix `/api/v1` مثل
`/api/v1/auth` و`/api/v1/admin` و`/api/v1/catalog`. لا تستخدم المسارات القديمة
غير المسبوقة مثل `/auth` أو `/admin` أو `/catalog`.
