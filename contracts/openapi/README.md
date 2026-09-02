# EIAMS Backend OpenAPI

`eiams-backend-v1.openapi.json` هو العقد المعتمد والمولّد من الباكيند المنفذ فعلياً.

يجب نسخ العقد مع ملف `eiams-backend-v1.provenance.json` إلى الفرونت، والتحقق من البصمة قبل توليد TypeScript types.

العقد الحالي يحتوي على 132 path و170 operation و279 schema، ولا توجد أي استجابة نجاح `2xx` بلا `content/schema`.

لا تستبدل مسارات الباكيند بمسارات العقد المؤقت في الفرونت مثل `/auth` أو `/admin` أو `/catalog`.
