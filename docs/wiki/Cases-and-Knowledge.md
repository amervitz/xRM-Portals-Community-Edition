# Cases and Knowledge

Support features: case management (service requests), the help desk layer, entitlements, and knowledge
articles with case deflection.

## Case management

Cases are Dataverse `incident` records surfaced on the portal. The portal adds routing, permissions,
deflection and self-service around them.

| Area | Location |
|---|---|
| Case management controllers/views | `Samples/MasterPortal/Areas/CaseManagement` |
| Service 311 (citizen service requests) | `Samples/MasterPortal/Areas/Service311` |
| Help desk | `Framework/Adxstudio.Xrm/HelpDesk` |
| Cases adapters | `Framework/Adxstudio.Xrm/Cases` |
| Case deflection script | `Areas/CaseManagement/js/case-deflection.js` |
| Entitlements script | `Areas/CaseManagement/js/entitlements.js` |
| Entitlements controller | `Areas/CaseManagement/Controllers/EntitlementsController.cs` |

### Case-related records

| Record | Role |
|---|---|
| `incident` | The case itself. |
| `adx_caseaccess`, `adx_casedeflection` | Case access and deflection configuration. |
| `adx_supportplan`, `adx_supportrequest` | Support plans and requests. |
| `adx_casetitle`, `adx_changeddate` | Case page configuration. |
| `adx_adx_servicerequest*` (`adx_servicerequest`, `adx_servicerequesttype`, `adx_servicerequestpriority`, `adx_servicerequestnumber`, `adx_servicestatus`) | Service request records used by the 311 scenarios. |
| `adx_311alert`, `adx_alertsubscription`, `adx_incidentdate` | Alerts and incident metadata. |
| `adx_permit`, `adx_permittype`, `adx_permitnumberfieldname` | Permits area records. |

`HelpDesk/SupportPlanNameFormat` formats the support plan name, and
`CustomerSupport/DisplayAllUserActivitiesOnTimeline` controls timeline breadth.

### Case deflection

Case deflection suggests knowledge articles while a user is creating or browsing a case, so they can solve
the problem without filing it. It is driven by `case-deflection.js` plus
`CaseDeflectionMetadataAttributes` and `FeedbackMetadataAttributes` in the framework, and by the
knowledge article search.

### Entitlements

Entitlements represent support the customer has purchased — the number of incidents they may raise.
`entitlements.js` and `EntitlementsController` surface them. Records include prepaid incident counters
(`adx_prepaidincidentscurrentlyavailable`, `adx_prepaidincidentsrequired`).

## Knowledge articles

Knowledge articles are Dataverse `knowledgearticle` records (also referenced historically as
`adx_knowledgearticle`) presented on the portal.

| Area | Location |
|---|---|
| Knowledge resources | `Framework/Adxstudio.Xrm/KnowledgeArticles` |
| Knowledge base area | `Samples/MasterPortal/Areas/KnowledgeBase` |
| Knowledge management area | `Samples/MasterPortal/Areas/KnowledgeManagement` |
| Article script | `Areas/KnowledgeManagement/js/article.js` |
| Article controllers | `Areas/KnowledgeBase/Controllers/ArticleController.cs`, `Areas/KnowledgeManagement/Controllers/ArticleController.cs` |
| Framework namespaces | `Adxstudio.Xrm/KnowledgeArticles` |

### Knowledge records and columns

| Record / column | Role |
|---|---|
| `knowledgearticle` | The article: title, body, keywords, publish dates. |
| `adx_knowledgearticle` | Portal-facing knowledge article record. |
| `adx_knowledgearticlecontentaccesslevel` | Links an article to a **content access level** for gated content. |
| `adx_article` / `adx_articleview` | Article view tracking. |
| `adx_kbarticle` | Knowledge base article record. |

### Knowledge settings

| Setting | Effect |
|---|---|
| `KnowledgeManagement/Article/Language` | Article language handling. |
| `KnowledgeManagement/ContentAccessLevel/Enabled` | Enables content-access-level gating for articles. |
| `KnowledgeManagement/DisplayNotes` | Whether annotations/notes appear. |
| `KnowledgeManagement/NotesFilter` | Filter applied to notes. |
| `KnowledgeManagement/Article/CaptureViewCount` | Whether article views are counted. |
| `KnowledgeManagement/Analytics/CaptureKnowledgeArticleReferrer` | Whether the referrer is recorded. |
| `KnowledgeManagement/Analytics/ExternalSearchEngines` | Search engines whose referrals count as external. |

### Rendering knowledge

Liquid exposes the `knowledge` global object (`KnowledgeDrop`, `KnowledgeArticleDrop`,
`KnowledgeArticleFunctions`, `KnowledgeArticlesDrop`). Combined with search, this is what powers
self-service:

```liquid
{% assign results = knowledge.articles | where: 'is_root_article', true | take: 10 %}
{% for article in results %}
  <a href="{{ article.url }}">{{ article.title }}</a>
{% endfor %}
```

Articles also participate in the **portal search index**, which is what case deflection queries. If
deflection returns nothing useful, check the search index before the deflection configuration.

## Access control

Support content is usually the most sensitive part of a portal, since it contains a customer's own data.
Apply, in order:

1. **Table permissions** on `incident`, `knowledgearticle` and related tables, with a **contact/account
   scope** so users see only their own records.
2. **Content access levels** for premium or licensed knowledge content.
3. **Web page access control rules** on the help desk and knowledge pages.
4. Form and list configuration (`adx_entitypermissionsenabled`) so the UI cannot be used to bypass the
   rules.

See [Security and Authentication](Security-and-Authentication.md).

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| A user sees another customer's cases | Table permission scope is global instead of contact/account scoped. Fix the permission, not the list. |
| Knowledge search returns nothing | The search index does not include the knowledge table/view; rebuild after adding it. |
| Deflection suggestions are irrelevant | Article indexing or the deflection metadata configuration. |
| Entitlement counts are wrong | The entitlement records, not the UI — counts come from Dataverse. |
| Article view counts are missing | `KnowledgeManagement/Article/CaptureViewCount` is off or the article view table is not writable. |

## Related pages

- [Security and Authentication](Security-and-Authentication.md)
- [Search](Search.md)
- [Entity Lists](Entity-Lists.md), [Entity Forms](Entity-Forms.md)
- [Site Settings Reference](Site-Settings-Reference.md#knowledge)
