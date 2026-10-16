# SharePoint Integration

The portal can surface **SharePoint document locations** — the documents linked to Dataverse records —
as browsable document lists and grids. This is used for partner document exchange, customer folders and
case attachments that live in SharePoint rather than in Dataverse.

## Configuration

| Setting | Where | Purpose |
|---|---|---|
| `SharePoint/URL` | Site setting | The SharePoint site URL used by the integration. |
| `<sharePoint>` section | `Web.config` | `SharePointSection` configuration, read by `SharePointConfigurationManager`. |

`SharePointSection` (`Framework/Adxstudio.Xrm/SharePoint/Configuration`) configures the connection; the
site setting supplies the URL. If the section is absent, the manager falls back to its defaults.

Dataverse provides the document locations themselves:

| Record | Role |
|---|---|
| `sharepointsite` | A configured SharePoint site in Dataverse. |
| `sharepointdocumentlocation` | A folder location associated with a Dataverse record. |
| `sharepointdocument` | A document (read through the location). |

The portal reads these records through the SharePoint data adapter rather than connecting to SharePoint
directly for metadata — Dataverse is the source of truth for **where** documents live, and the portal uses
SharePoint to list and download them.

## Code layout

`Framework/Adxstudio.Xrm/SharePoint`:

| Type | Role |
|---|---|
| `ClientFactory` | Creates the SharePoint client. |
| `SharePointConnection` | Connection handling. |
| `SharePointDataAdapter`, `ISharePointDataAdapter` | Document location and document access. |
| `SharePointCollection`, `ISharePointCollection` | Collections of items. |
| `SharePointItem`, `ISharePointItem` | A single document. |
| `SharePointResult`, `ISharePointResult` | Operation results. |
| `OrganizationServiceContextExtensions` | Query helpers over `sharepointdocumentlocation` and `sharepointsite`. |
| `Handlers`, `Collections`, `Configuration` | Handlers, collection implementations and configuration. |

Liquid exposure is through the `sharepoint` global object:
`SharePointDrop`, `SharePointDocumentsDrop`, `SharePointDocumentListDrop`, `SharePointDocumentDrop`.

## Rendering documents

### Liquid

```liquid
{% assign documents = sharepoint.documents %}
{% for document in documents %}
  <div class="document">
    <a href="{{ document.url }}">{{ document.name }}</a>
    {% if document.modified_on %}<span>{{ document.modified_on | date: 'yyyy-MM-dd' }}</span>{% endif %}
  </div>
{% endfor %}
```

### ASP.NET and JavaScript

- `Areas/Portal/Controllers/SharePointGridController.cs` serves the grid data.
- `js/sharepoint-grid.js` renders the grid client side.
- The grid uses the same grid metadata conventions as entity lists, so column configuration follows the
  view metadata pattern.

## Permissions

SharePoint documents are **not** governed by portal table permissions directly. Access depends on:

1. The Dataverse **security** around the document location records, and
2. The **SharePoint** permissions of the identity the portal uses to read the library, or of the user when
   the integration maps the user's identity through.

This makes SharePoint the least intuitive permission surface in the portal. Before exposing a document
library:

- Confirm which identity actually reads the library, and what it can see.
- Test with a **restricted** user, not an administrator — administrator impersonation hides permission
  problems entirely.
- Be explicit about whether a user sees every document or only their own record's folder.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| No documents listed | `SharePoint/URL` unset, or no `sharepointdocumentlocation` records for the record. |
| "Access denied" from SharePoint | The reading identity lacks permission on the library. |
| Documents list but links 404 | The document location path changed, or the site URL does not match the library. |
| Grid renders empty | `sharepoint-grid.js` not loaded, or the grid controller route unreachable. |
| Works for admins, fails for users | You are testing with elevated permissions — retest as a restricted portal user. |

## Related pages

- [Security and Authentication](Security-and-Authentication.md)
- [Entity Lists](Entity-Lists.md) — grid conventions
- [Site Settings Reference](Site-Settings-Reference.md#sharepoint)
- [Liquid Objects Reference](Liquid-Objects-Reference.md#sharepoint)
