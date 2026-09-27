# تقرير مراجعة Conflict Resolution

**الملف المراجع:** `/home/mutassem/Downloads/conflict-resolution.md`

**تاريخ المراجعة:** 2026-09-21

**نطاق المراجعة:** القرارات `RESOLUTION-001` إلى `RESOLUTION-040` مقابل الباكيند الحالي، وOpenAPI، ونسخة الفرونت المحلية عند الحاجة.

**نوع المراجعة:** تحليل فقط؛ لم يُعدّل كود الباكيند أو الفرونت.

## 1. الخلاصة التنفيذية

الملف قوي من الناحية التحليلية، ومعظم اعتراضات مطور الفرونت صحيحة أو صحيحة جزئيًا. لكنه لا يصلح أن يُعتمد ويُنفذ حرفيًا للأسباب التالية:

- حالته التنفيذية قديمة؛ بعض العناصر المسجلة `NOT IMPLEMENTED` نُفذت لاحقًا.
- بعض القرارات تغيّر Business Requirements وليست مجرد إصلاحات ربط بين الفرونت والباكيند.
- بعض الحلول تتعارض مع قرارات المشروع السابقة، وخصوصًا تغيير شكل الـResponse الحالي.
- بعض الحلول معمارية اختيارية وليست الحل الصحيح الوحيد.
- بعض وصف الحالة الحالية لا يطابق نسخة الفرونت الموجودة محليًا.
- وجود كلمة `APPROVED` داخل الملف لا يكفي وحده لإثبات اعتماد كل التفاصيل دون مرجع قرار أو إصدار واضح.

الملف نفسه يوضح أن `APPROVED` تصف السلوك المستهدف فقط، ولا تعني أن التنفيذ أو التحقق قد تم.

تم التحقق أيضًا من أن Solution الباكيند الحالية تُبنى بنجاح:

- `0` أخطاء.
- `0` تحذيرات.

## 2. مفتاح التقييم

- **صحيح:** الاعتراض يصف مشكلة موجودة فعليًا.
- **صحيح جزئيًا:** جزء من المشكلة موجود، لكن جزءًا آخر نُفذ أو تغيّر.
- **قديم:** كان صحيحًا وقت كتابة الملف، لكنه لا يصف الكود الحالي بدقة.
- **قرار تجاري:** لا يمكن اعتباره خطأ تقنيًا قبل موافقة صاحب العمل.
- **حل اختياري:** تصميم صالح، لكنه ليس الحل الوحيد الممكن.

## 3. تقييم القرارات 001–010

| القرار | صحة الاعتراض | صحة الحل | الحالة الحالية |
|---|---|---|---|
| 001 — Atomic exactly-one user assignment | صحيح | صحيح ومهم | غير مطبق |
| 002 — Dotted permissions | قديم جزئيًا | صحيح | مطبق إلى حد كبير في الباكيند |
| 003 — Username-only | قديم جزئيًا | يحتاج قرارًا نهائيًا | Username موجود، لكن البريد مقبول للتوافق |
| 004 — Session وRefresh token | صحيح | صحيح أمنيًا | مطبق جزئيًا |
| 005 — Generic response contract | اختلاف حقيقي | يتعارض مع قرار المشروع السابق | لا يُطبق حرفيًا |
| 006 — Shared frontend transport | صحيح في الفرونت | صحيح | مسؤولية الفرونت |
| 007 — Atomic document draft save | صحيح جزئيًا | صالح لكنه تغيير كبير | غير مطبق |
| 008 — Workflow actors | صحيح جزئيًا | صحيح بعد تثبيت الأدوار | مطبق جزئيًا |
| 009 — Unit-conversion provenance | صحيح وخطير | صحيح وضروري | غير مطبق |
| 010 — Material classification lock | صحيح | صحيح وضروري | غير مكتمل |

### 3.1 القرار 001 — إنشاء المستخدم مع Role/Scope

الاعتراض صحيح. إنشاء المستخدم الحالي ينشئ `User` فقط، دون إنشاء Role/Scope assignment معه. ثم يُنفذ `SaveChanges` مباشرة.

**الدليل:**

- `src/Application/Users/Create/CreateUserCommandHandler.cs`
- `src/Web.Api/Controllers/Users/CreateUserController.cs`

الحل المقترح صحيح:

1. يحتوي طلب إنشاء المستخدم على بيانات الحساب وAssignment واحد مطلوب.
2. يُنشأ User وAssignment داخل Transaction واحدة.
3. يؤدي فشل أي جزء إلى إلغاء العملية كلها.
4. يكون التعديل اللاحق Replacement ذريًا، وليس Grant إضافيًا.
5. لا يوجد Delete أو Revoke يترك المستخدم دون Assignment.

### 3.2 القرار 002 — Dotted permissions

الادعاء بأن الباكيند يستخدم Colon permissions أصبح قديمًا. الباكيند الحالي يستخدم أكوادًا مثل:

```text
document.post
document.reject
admin.user.manage
catalog.view
```

**الدليل:**

- `src/Application/Abstractions/Authorization/PermissionCodes.cs`
- `src/Domain/Permissions/WellKnownDottedPermissions.cs`

التصميم المقترح صحيح، لكن خانة `NOT IMPLEMENTED` في الملف لم تعد دقيقة بالنسبة للباكيند.

### 3.3 القرار 003 — Username-only

أصبح `username` موجودًا في المستخدم وطلب تسجيل الدخول، لكن الباكيند ما زال يقبل البريد الإلكتروني كتوافق مؤقت:

```csharp
user.Username == username || user.Email == username
```

**الدليل:**

- `src/Application/Users/Login/LoginUserCommandHandler.cs`
- `src/Application/Users/Login/LoginUserCommandValidator.cs`

التوصية هي الانتقال إلى Username-only بعد التأكد من:

- امتلاك جميع الحسابات Username صالحًا وفريدًا.
- انتقال الفرونت بالكامل إلى Username.
- وجود خطة واضحة لإزالة Email compatibility.

### 3.4 القرار 004 — Session وRefresh token

التصميم الأمني المقترح صحيح، لكن التنفيذ ما زال انتقاليًا:

- Refresh token يوضع في Cookie.
- يمكن أن يظهر أيضًا في JSON حسب الإعداد.
- Refresh request body ما زال مدعومًا.
- Session ما زالت تحتوي `activeRoles` و`scopeState`.
- Session user لا يعرض Username حاليًا.

**الدليل:**

- `src/Web.Api/Infrastructure/RefreshTokenTransport.cs`
- `src/Web.Api/Infrastructure/AuthCookies.cs`
- `src/Application/Users/GetSession/UserSessionResponse.cs`

السلوك المستهدف المقترح:

```json
{
  "accessToken": "...",
  "expiresInSeconds": 3600,
  "session": {
    "user": {},
    "activeRole": {},
    "activeScope": {},
    "permissionCodes": []
  }
}
```

ويجب أن يبقى Refresh token في HttpOnly cookie فقط.

### 3.5 القرار 005 — Response contract

الاعتراض صحيح لأن الفرونت والباكيند لا يفسران الـResponse بالطريقة نفسها. لكن الحل الموجود في الوثيقة يغيّر العقد إلى camelCase بالكامل ويضع Pagination داخل `data.pageInfo` وينقل Metadata إلى موضع آخر.

هذا يتعارض مع القرار السابق بالإبقاء على شكل الباكيند الحالي:

```json
{
  "success": true,
  "data": {},
  "pagination": {
    "page": 1,
    "page_size": 20,
    "total_items": 142,
    "total_pages": 8
  },
  "meta": {
    "request_id": "...",
    "timestamp": "..."
  }
}
```

والخطأ:

```json
{
  "success": false,
  "error": {
    "code": "...",
    "message": "...",
    "details": {},
    "request_id": "..."
  }
}
```

**الدليل:**

- `src/Web.Api/Infrastructure/ApiContracts.cs`

**القرار المقترح:** لا يُطبق Resolution-005 حرفيًا. يجب أن يتكيف الفرونت مع Envelope الباكيند المعتمد.

### 3.6 القرار 007 — Complete document draft save

تقسيم حفظ المستند إلى رأس وخطوط وتفاصيل عبر طلبات مختلفة قد ينتج Draft جزئيًا إذا فشل أحد الطلبات.

لكن يوجد تصميمان صالحان:

1. **Complete aggregate save:** يحفظ المستند كاملًا داخل Transaction واحدة.
2. **Resumable draft:** يسمح بـDraft ناقص ومرئي، ويمنع Submit حتى اكتماله.

لا تُعد البنية الحالية خطأ تلقائيًا إذا كان الـDraft الجزئي مرئيًا وقابلًا للإكمال ولا ينتج آثار Inventory قبل Post. يحتاج هذا القرار إلى تثبيت تجربة الاستخدام قبل إعادة بناء كبيرة.

## 4. تقييم القرارات 011–020

| القرار | التقييم |
|---|---|
| 011 — Freeze modes | صحيح جزئيًا؛ الأنواع الثلاثة موجودة لكن Aborted والأسباب غير موجودة |
| 012 — Exact freeze overlap | صحيح؛ الفحص النهائي لا يزال أوسع من المواد المتداخلة فعليًا |
| 013 — حذف OrganizationalUnit scope | اختلاف حقيقي، لكن الحل يحتاج قرار Business صريحًا |
| 014 — Catalog and inbound identities | صحيح جزئيًا والحل جيد لكنه واسع |
| 015 — إلغاء Active Scope switching | صحيح ومتوافق مع نموذج Assignment المفرد |
| 016 — Singular role-scope API | صحيح؛ المسار الحديث موجود والمسارات القديمة ما زالت منشورة |
| 017 — One-based pagination | مطبق غالبًا؛ تغيير Envelope غير مقبول |
| 018 — Short catalog routes | مطبق تقريبًا مع Duplicate conversion routes |
| 019 — Organization ownership | صحيح جزئيًا |
| 020 — Atomic Warehouse policy | اختلاف حقيقي، لكن Complete replacement خيار تصميم |

### 4.1 القراران 011 و012 — Freeze policy

الأنواع الثلاثة موجودة:

```text
HardFreeze
SoftFreeze
NoFreeze
```

لكن ينقص التنفيذ:

- سبب إلزامي لاختيار HardFreeze أو NoFreeze.
- انتقال `Aborted` مضبوط ومراجع.
- فحص Exact overlap حسب Warehouse والمواد المتأثرة.
- منع العملية فقط عندما توجد مادة مشتركة فعليًا مع Count قيد التنفيذ.

الحل المقترح جيد، خصوصًا إعادة الفحص النهائي داخل Transaction الخاصة بـPost أوReversal.

### 4.2 القرار 013 — OrganizationalUnit scope

الوثيقة تريد قصر Scope على:

```text
Enterprise
Site
Warehouse
```

وحذف:

```text
OrganizationalUnit
```

لكن الباكيند الحالي يدعم الأنواع الأربعة.

**الدليل:**

- `src/Domain/Common/ScopeType.cs`
- `src/Infrastructure/Authorization/ScopeAuthorizationService.cs`

هذا الحل قد يتعارض مع الحاجة السابقة إلى منح المدير Scope على منطقة أو مديرية، ثم السماح له برؤية ما يقع تحتها فقط.

قبل التنفيذ يجب حسم السؤال:

> هل `Site` تمثل المنطقة/المديرية التي نريد إسناد المستخدم إليها، أم أن `OrganizationalUnit` هي التي تمثل المديرية؟

إذا كانت `OrganizationalUnit` تمثل المديرية فعلًا، فالتوصية هي إبقاؤها Scope صالحًا، مع القواعد التالية:

- Role واحدة لكل مستخدم.
- Scope واحدة لكل مستخدم.
- لا يوجد Active Scope switching.
- يرى المستخدم فقط الموارد الواقعة ضمن Scope الخاص به وتسلسله الهرمي.

### 4.3 القرار 016 — Singular role-scope resource

المسار الحديث موجود:

```http
GET /api/v1/admin/users/{userId}/role-scope
PUT /api/v1/admin/users/{userId}/role-scope
```

لكن OpenAPI ما زال ينشر مسارات النموذج القديم:

```http
GET /api/v1/admin/users/{userId}/role-scopes
POST /api/v1/admin/user-role-scopes
DELETE /api/v1/admin/user-role-scopes/{userRoleScopeId}
DELETE /api/v1/admin/users/{userId}/role-scope
```

الحل الصحيح:

- GET singular.
- PUT atomic replacement.
- لا DELETE للتكليف الوحيد.
- لا Grant مستقل.
- إضافة `expectedRowVersion`.
- إعادة Assignment كامل بعد النجاح، وليس ID فقط.

### 4.4 القرار 017 — Pagination

One-based pagination قرار صحيح ومتوافق مع الباكيند. لكن يجب الإبقاء على Pagination في المستوى العلوي وفق العقد الحالي، وعدم نقلها إلى `data.pageInfo`.

المشكلة الأساسية في الفرونت هي استمرار استعمال `pageIndex` يبدأ من صفر في مواضع متعددة.

## 5. تقييم القرارات 021–030

| القرار | صحة الاعتراض | تقييم الحل |
|---|---|---|
| 021 — Inventory projections | صحيح | جيد، لكن Decimal string يحتاج قرارًا |
| 022 — Lifecycle and reversal | صحيح جزئيًا | جيد؛ linked reversal draft موجود أصلًا |
| 023 — Attachment results | صحيح | جيد، ويمكن تقليل حجم النتيجة |
| 024 — Unified custody | صحيح جزئيًا | جيد ويحتاج Polymorphic contract واضحًا |
| 025 — Count actuals | صحيح | الحل جيد |
| 026 — Atomic adjustment draft | اختلاف صحيح | حل صالح لكنه ليس الوحيد |
| 027 — Administration commands | صحيح | الحل جيد |
| 028 — Counterpart security | صحيح ومهم أمنيًا | الحل قوي |
| 029 — Supplier-only Receiving | قرار تجاري | يحتاج موافقة صاحب العمل |
| 030 — Supplier-reference route | اختلاف صحيح | الاسم المقترح جيد لكنه غير إلزامي |

### 5.1 القرار 021 — Inventory projections

الاعتراض صحيح في النقاط التالية:

- لا يوجد Balance detail route مستقل.
- لا يوجد Low-stock projection كامل.
- Balance وMovement projections غير موحدة.
- توجد أسماء حقول مسطحة ومتكررة بدل Shallow references موحدة.

لكن تحويل Decimal إلى String في JSON قرار كبير. يفيد في منع فقد الدقة في JavaScript، لكنه يتطلب Decimal library وعقودًا جديدة في الفرونت. يجب تطبيقه عندما تكون دقة الأرقام الفعلية أكبر من دقة JavaScript، وليس كتحويل شكلي فقط.

### 5.2 القرار 022 — Lifecycle and reversal

ميزة Reversal ليست غائبة بالكامل. الباكيند الحالي:

- ينشئ Compensating Draft جديدًا.
- يربطه بالمستند الأصلي.
- لا يعدل الحركات القديمة.
- يمنع Reversal مكررًا.
- يمنع Reversal للـDisposal.

**الدليل:**

- `src/Application/WarehouseDocuments/CreateReversal/CreateReversalDocumentCommandHandler.cs`
- `src/Application/WarehouseDocuments/Post/PostDocumentCommandHandler.cs`

الناقص:

- سبب للعملية.
- Expected version للمستند الأصلي.
- Authoritative result كامل.
- بناء Adjustment-specific reversal كامل.
- توحيد اسم `revise` بدل `return-to-draft` في العقد العام إن تم اعتماده.

### 5.3 القرار 023 — Attachment mutations

رفع المرفق يعيد ID فقط، والحذف يعيد Empty response، رغم تغيير RowVersion للمستند.

الحل المقترح جيد، لكن لا يلزم إعادة جميع Attachments في كل نتيجة. يمكن إعادة:

```json
{
  "attachment": {},
  "document_row_version": 4,
  "policy": {}
}
```

ثم ينفذ الفرونت invalidate/refetch للقائمة عند الحاجة.

### 5.4 القرار 026 — Adjustment aggregate

حفظ Adjustment كاملًا في عملية واحدة يبسط الفرونت ويعطي Atomic save حقيقيًا، لكنه تغيير كبير وليس الحل الصحيح الوحيد.

يمكن إبقاء Draft مجزأ إذا تحققت الشروط التالية:

- يكون Draft مرئيًا وقابلًا للإكمال.
- يمنع Post قبل اكتمال جميع القواعد.
- لا ينتج أي أثر مخزني قبل Post.
- يوجد Optimistic concurrency مناسب.
- يعيد Post نتيجة Authoritative واضحة.

### 5.5 القرار 028 — Operation-aware counterpart search

هذا اعتراض أمني مهم. لا يجب السماح للمستخدم باستعراض جميع الجهات لمجرد امتلاكه `document.view`.

التصميم الصحيح:

- يأخذ البحث Operation context.
- يأخذ Warehouse/Document/Custody المالكة للعملية.
- يحدد السيرفر أنواع الجهات المسموحة.
- لا تعتبر نتيجة البحث دليل Authorization.
- يعاد التحقق عند Submit/Post/Transfer.

### 5.6 القرار 029 — Supplier-only Receiving

حذف `Transfer` و`Return` من ReceivingInfo تغيير Business Model وليس إصلاحًا تقنيًا.

الحل منطقي لأن النظام يملك أصلًا:

- Transfer document.
- Return document.
- Receiving document.

لكن يجب تأكيد أن Receiving في v1 يعني فقط الاستلام من مورد خارجي قبل تنفيذ التغيير.

## 6. تقييم القرارات 031–040

| القرار | التقييم |
|---|---|
| 031 — ExternalParty status | صحيح؛ الحالي Numeric ويعيد Empty |
| 032 — Mandatory Material Family | مطبق جزئيًا، مع حاجة لتشديد DeleteBehavior |
| 033 — Site annual numbering | مطبق في جوهره؛ حالة الملف قديمة |
| 034 — Signed delta ledger | مطبق بدرجة كبيرة |
| 035 — Custody lifecycle | مطبق جزئيًا |
| 036 — Adjustment shared-key lifecycle | الاعتراض صحيح ويكشف نقصًا مهمًا |
| 037 — Count taxonomy | النوع والنطاق مطبقان؛ Aborted ناقص |
| 038 — Catalog dictionary | مطبق جزئيًا مع تضارب BaseUnit |
| 039 — Same-origin topology | حل جيد أمنيًا ومطبق جزئيًا |
| 040 — Real integration default | الحل جيد لكن وصف الحالة الحالية غير دقيق |

### 6.1 القرار 033 — Site-scoped annual numbering

خانة `NOT IMPLEMENTED` غير دقيقة. الباكيند يملك:

- المفتاح `(SiteId, DocumentType, Year)`.
- Atomic PostgreSQL upsert.
- زيادة آمنة تحت التزامن.
- تخصيص الرقم من السيرفر.
- فصل System reference عن Paper reference.

**الدليل:**

- `src/Infrastructure/Numbering/ReferenceNumberGenerator.cs`
- `src/Infrastructure/DocumentSequences/DocumentSequenceConfiguration.cs`

الناقص الأساسي:

- السنة تستخدم UTC مباشرة بدل Business timezone مُعدّة.
- بعض Authoritative response والتوثيق والاختبارات الإضافية.

التصنيف الصحيح هو **Mostly Implemented**.

### 6.2 القرار 036 — Adjustment reversal completeness

هذا اعتراض صحيح ومهم. Reversal الحالي ينسخ DocumentLines، لكنه لا يبني بالضرورة:

- `InventoryAdjustment` detail.
- `AdjustmentLines`.
- Purpose كاملًا.
- روابط Count/Asset اللازمة.

قد ينتج عن ذلك Reversal shell غير مكتمل لبعض Adjustments، لذلك يحتاج هذا البند أولوية عالية.

### 6.3 القرار 038 — Catalog data dictionary

حدود الحقول الأساسية موجودة، مثل:

- Material code بطول أقصى 100.
- `NameAr` و`NameEn` بطول أقصى 500.
- وجود `Material.BaseUnitId`.

**الدليل:**

- `src/Infrastructure/Materials/MaterialConfiguration.cs`

لكن يوجد تضارب: `MaterialFamily` ما زالت تحتوي BaseUnit، وبعض حسابات DocumentLine تعتمد Family BaseUnit بدل Material BaseUnit.

يجب اختيار مصدر واحد للحقيقة. التوصية:

```text
Material.BaseUnitId
```

وذلك لأن مادتين من العائلة نفسها قد تختلفان في وحدة التخزين الأساسية.

### 6.4 القرار 039 — Same-origin topology

الحل الأمني جيد:

- يستخدم المتصفح `/api/v1`.
- يعمل Vite proxy محليًا.
- تبقى Refresh cookie Same-origin.
- يفشل الإنتاج مغلقًا عند غياب Origins.
- لا يجمع Wildcard origin مع Credentials.

الباكيند يفشل مغلقًا في Production، لكنه يسمح `AllowAnyOrigin` في Development عندما تكون القائمة فارغة.

**الدليل:**

- `src/Web.Api/Extensions/CorsExtensions.cs`

هذا الفرع لا يستخدم `AllowCredentials`، لذلك ليس تسريب Cookie مباشرًا، لكنه لا يطابق تصميم Same-origin الصارم.

### 6.5 القرار 040 — Real integration default

وصف الحالة الحالية داخل الوثيقة لا يطابق نسخة الفرونت المحلية:

- Mocks default هي `true`.
- Proxy default هو `http://localhost:8080`.
- Auth bypass default هي `true`.

**الدليل في نسخة الفرونت المحلية:**

- `src/config/env.ts`
- `src/config/vite-dev-server.ts`
- `src/shared/services/dev-session.ts`

الحل المستهدف صحيح:

```dotenv
VITE_API_BASE_URL=/api/v1
VITE_ENABLE_API_MOCKS=false
VITE_AUTH_BYPASS=false
EIAMS_DEV_PROXY_TARGET=http://localhost:5000
```

مع Sandbox صريح ومرئي عند الحاجة.

## 7. التناقضات والملاحظات داخل الملف

### 7.1 Response contract

الملف يطلب تغيير Envelope، بينما القرار الحالي للمشروع هو الإبقاء على شكل الباكيند. يجب تعديل Resolution-005.

### 7.2 OrganizationalUnit scope

الملف يطلب حذفه، لكن ذلك قد يلغي Scope المديرية الذي يحتاجه النظام. يجب حسم تمثيل المنطقة والمديرية قبل التنفيذ.

### 7.3 Username-only

الملف يعتبر Username-only محسومًا، بينما الباكيند ما زال يقبل البريد للتوافق. يجب تحديد موعد إزالة التوافق رسميًا.

### 7.4 حالات تنفيذ قديمة

حالات التنفيذ غير دقيقة خصوصًا في:

- Dotted permissions.
- Username.
- Site numbering.
- Signed-delta ledger.
- Inventory count type/scope.
- Catalog field capacities.

### 7.5 Resolution-040

وصف Mocks وProxy الحالي لا يطابق نسخة الفرونت الموجودة محليًا. يجب ربط الوثيقة برقم Commit أو OpenAPI hash محدد قبل اعتمادها مرجعًا.

### 7.6 Material BaseUnit

يوجد أكثر من مصدر لوحدة القياس الأساسية بين Material وMaterialFamily. هذا تضارب حقيقي يجب إزالته.

### 7.7 حلول تُعرض كأنها إلزامية

الحلول التالية صالحة لكنها ليست الخيارات الوحيدة:

- Complete aggregate document save.
- Complete aggregate adjustment save.
- Atomic Warehouse capability replacement.
- Decimal-as-string.
- Supplier-only Receiving.

## 8. الأولويات المقترحة

### P0 — قرارات وعقود تمنع التكامل الآمن

1. تثبيت Response envelope الحالي رسميًا وتعديل Resolution-005.
2. حسم أنواع Scope، وخصوصًا `OrganizationalUnit`.
3. إنشاء User مع Role/Scope في Transaction واحدة.
4. حذف أو تقييد Grant/Revoke/Delete assignment APIs القديمة.
5. جعل Session مفردة فعلًا.
6. جعل Refresh token Cookie-only.
7. تحديد موعد إزالة Email login compatibility.
8. تحديث OpenAPI بعد تثبيت القرارات.

### P1 — سلامة البيانات والأمان

1. Unit-conversion immutable provenance.
2. Material classification lock بعد الاستخدام.
3. Exact inventory-freeze overlap.
4. إصلاح Adjustment reversal الكامل.
5. Operation-aware counterpart search.
6. Parent-versioned attachment results.
7. توحيد Material BaseUnit.

### P2 — تحسين عقود الواجهة

1. Inventory balance detail وLow-stock state.
2. Custody typed detail/history.
3. Authoritative count mutation results.
4. ExternalParty string status وAuthoritative result.
5. Supplier-reference route.
6. Shallow named references.

### P3 — تحسين تجربة التطوير

1. تعطيل Mocks افتراضيًا.
2. تعطيل Auth bypass افتراضيًا.
3. جعل Proxy الافتراضي يتجه إلى port 5000.
4. توفير Sandbox صريح ومرئي.
5. توحيد وثائق الفرونت مع الإعداد التنفيذي.

## 9. توزيع المسؤوليات

### Backend

- Atomic user provisioning.
- Session وRefresh-token hardening.
- Singular assignment resource.
- Conversion provenance.
- Classification lock.
- Freeze overlap.
- Adjustment reversal.
- Attachment results.
- Counterpart authorization.
- Inventory/custody projections.

### Frontend

- Shared API transport.
- فهم Envelope الحالي في مكان مركزي واحد.
- إزالة `pageIndex` صفرية الأساس.
- إزالة Active Scope switching إذا ثبت العقد المفرد.
- تعطيل Mocks وAuth bypass افتراضيًا.
- تحديث Mocks وTypes بعد تثبيت OpenAPI.

### Shared / Business decision

- أنواع Scope النهائية.
- Username-only وموعد إلغاء Email compatibility.
- Supplier-only Receiving.
- Aggregate save مقابل Resumable draft.
- Decimal transport policy.
- أسماء Routes النهائية قبل أي Breaking change.

## 10. الحكم النهائي

- معظم اعتراضات الفرونت صحيحة أو صحيحة جزئيًا.
- الحلول الأمنية وحلول سلامة البيانات جيدة عمومًا.
- لا ينبغي تنفيذ الملف كاملًا كما هو.
- Resolution-005 يحتاج تعديلًا مؤكدًا ليتوافق مع Envelope الباكيند الحالي.
- Resolution-013 يحتاج قرار Business صريحًا حول تمثيل المنطقة والمديرية.
- Resolution-029 يحتاج موافقة Business قبل حصر Receiving بالمورد.
- Resolution-040 يحتاج تصحيح وصف الحالة التنفيذية وربطه بنسخة Frontend محددة.
- يجب تحديث Implementation status لكل قرار قبل تحويل الوثيقة إلى خطة تنفيذ.

الملف مناسب كـWorking Decision Document، لكنه ليس حاليًا عقدًا نهائيًا جاهزًا للتنفيذ الكامل.
