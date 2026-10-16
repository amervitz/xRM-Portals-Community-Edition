# Web Files and Attachments

The portal stores two kinds of binary content: **web files** (content you publish) and **attachments**
(files users upload, stored as Dataverse annotations/notes). Both can live in Dataverse or in Azure Blob
Storage.

## Web files

An `adx_webfile` is a published, downloadable file served at a URL like any page.

| Column | Purpose |
|---|---|
| `adx_name` | Name. |
| `adx_partialurl` | URL segment. |
| `adx_publishingstateid` | Whether the file is served. |
| `adx_mimetype` | Content type sent with the response. |
| `adx_contentdisposition` | Whether the browser displays it inline or downloads it. |
| `adx_summary` | Description. |
| `adx_cloudblobaddress` | The blob address, when the file is stored in Azure Blob Storage. |

Web files can be permission-controlled through **content access levels** and web page access control, the
same as pages.

### Master web files

`adx_masterwebfileid` lets one web file be the "master" a page resolves to — used by the built-in master
page/page-template plumbing so a page can serve a file while keeping its own URL.

### Cloud blob addresses

`adx_cloudblobaddress` is understood in **two forms**:

1. A **fully qualified** address (the form Power Pages writes), for example a complete blob URL.
2. A **path relative** to the storage account, which the portal concatenates onto the account endpoint.

Older builds only understood the relative form, so a fully qualified address produced a malformed URL.
Both are handled now, so blob web files created by either version are served.

When a web file resolves to a blob, the request is routed through the **cloud blob redirect handler**,
which either redirects to the blob or proxies it, depending on configuration.

### Storage configuration

| Site setting / app setting | Purpose |
|---|---|
| `WebFiles/StorageLocation` | Where web files are stored — Dataverse or cloud storage. |
| `WebFiles/CloudStorageAccount` | The storage account connection, when cloud storage is used. |
| `WebFiles/MaxFileSizeErrorMessage` | Message shown when the maximum file size is exceeded. |
| `FileStorage/CloudStorageAccount`, `FileStorage/CloudStorageContainerName` | File storage account and container. |

Azurite/Azure notes: when cloud storage is configured, the portal reads and writes blobs through the
storage account. Keep credentials out of source control — put them in configuration that is not
committed, or in the hosting platform's settings.

## Attachments (notes)

Portal attachments are **annotations** — Dataverse notes on a record. The portal implements these in
`Framework/Adxstudio.Xrm/Notes`:

- `AnnotationDataAdapter` — CRUD over annotations.
- `CrmAnnotationFile` — an annotation file stored in Dataverse.
- `AzureAnnotationFile` — an annotation file stored in Azure Blob Storage.
- `AnnotationSettings` / `IAnnotationSettings` — how annotations are configured.

### Where attachments appear

- **Entity forms** and **steps of web forms** can require or accept attachments per their `adx_attachfile*`
  columns. See [Entity Forms](Entity-Forms.md#attachments).
- **Notes and timeline controls** on portal pages and in custom markup show and edit a record's notes.
  The related JavaScript lives in `js/entity-notes.js`; the portal also exposes
  `Areas/Portal/Controllers/EntityNotesController.cs`.
- **Snippets and web page content** can embed note content.

### Attachment behaviour and known fixes

This branch fixed a set of attachment defects. If you are upgrading, these are now correct:

| Behaviour | Detail |
|---|---|
| **Saving note attachments to Azure Blob Storage** | The blob is uploaded, then the annotation is updated **through the organization service**, matching how it was created, and only the changed attribute is sent. Previously the update used a service context the annotation was never attached to, which threw. |
| **Replacing a blob attachment** | The previous blob is now removed. It used to be looked up under a hyphenated record id and the stored file name — neither of which matches how the blob was written — so it was never found and never deleted. |
| **Deleting a note** | No longer throws `NullReferenceException` during content map refresh. A deleted record is retrieved as null, and the annotation relationship check used to dereference it. |
| **Editing a note without changing the attachment** | No longer throws `NullReferenceException`. When the attachment is left unchanged no file is posted, and the code now reports that there is no new attachment instead of throwing while checking. |

### Attachment settings

| Setting | Purpose |
|---|---|
| `KnowledgeManagement/DisplayNotes` | Whether notes are displayed in the knowledge experience. |
| `KnowledgeManagement/NotesFilter` | Filter applied to notes. |
| `CustomerSupport/DisplayAllUserActivitiesOnTimeline` | Whether the timeline shows all user activities. |
| `Portal/Lookup/Modal/Grid/*` | Text and messages for the lookup modal used by related-record pickers. |

Attachment size is also bounded by the application-wide `maxRequestLength` in `Web.config` (100 MB in the
sample) — see [Configuration Reference](Configuration-Reference.md).

## Rendering files and notes in templates

Web files and notes are exposed through drops:

- `EntityNoteDrop` — a note in a template, with `documentbody`, `filename`, `mimetype` and related
  accessors. Its helpers include `get_document_body`.
- `EntityFileSystem` — the file system used for web templates, showing the pattern for reading file
  content from Dataverse entities.
- `LocalFileSystem` and `CompositeFileSystem` — the template file system layering used by `include` and
  `extends`.

Example — list a record's notes:

```liquid
{% assign notes = entities.notes[page.id] %}
{% for note in notes %}
  <div class="note">
    <strong>{{ note.subject }}</strong>
    <div>{{ note.notetext | text_to_html }}</div>
    {% if note.filename %}
      <a href="{{ note.url }}">{{ note.filename }}</a>
    {% endif %}
  </div>
{% endfor %}
```

(Exact property names depend on the drop — check [Liquid Objects Reference](Liquid-Objects-Reference.md).)

## Related pages

- [Web Files in Content Management](Content-Management.md#web-files)
- [Entity Forms — attachments](Entity-Forms.md#attachments)
- [Security and Authentication](Security-and-Authentication.md) — content access levels apply to web files
- [Configuration Reference](Configuration-Reference.md) — `maxRequestLength` and storage settings
