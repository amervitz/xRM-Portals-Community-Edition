# Entity Forms

An **entity form** renders a form over a Dataverse record — create, edit or read-only — with field
customization, validation, attachments, captcha and redirect behaviour. It is the portal equivalent of a
Power Pages **form**.

An entity form is an `adx_entityform` record plus a set of `adx_entityformmetadata` child records that
customize individual fields, sections and tabs.

## The record

| Column | Purpose |
|---|---|
| `adx_name` | Referenced by `{% entityform name: 'New Support Request' %}`. |
| `adx_entityname` | Logical name of the table. |
| `adx_formname` | Which Dataverse form to base the layout on. |
| **`adx_mode`** | The form mode — see the table below. |
| `adx_recordidquerystringparametername` | Query-string parameter carrying the record id in Edit/ReadOnly mode. |
| `adx_entitypermissionsenabled` | Whether table permissions are enforced. |
| `adx_redirecturl`, `adx_redirectwebpage`, `adx_onsuccess` | Where to go after a successful save. |
| `adx_successmessage`, `adx_hideformonsuccess` | Success feedback. |
| `adx_recordnotfoundmessage` | Shown when the requested record does not exist. |
| `adx_captcharequired` | Require a captcha. |
| `adx_validationregularexpression`, `adx_validationregularexpressionerrormessage`, `adx_validationerrormessage`, `adx_requiredfieldvalidationerrormessage`, `adx_rangevalidationerrormessage`, `adx_multiplechoicevalidationerrormessage`, `adx_constantsum*` | Form-level validation messages and rules. |
| `adx_attachfile*` | Attachment support — see [attachments](#attachments). |
| `adx_recommendedfieldsrequired`, `adx_forceallfieldsrequired` | Field-requirement policy. |
| `adx_showunsupportedfields`, `adx_showownerfields` | Whether to render fields/owner fields Dataverse does not officially support on forms. |
| `adx_geolocation*` | Geolocation field handling and its error message. |
| `adx_registerstartupscript` | Script to emit with the form. |
| `adx_disablepagevalidation`, `adx_disabletargetvalidation` | Validation toggles. |
| `adx_editexpiredmessage`, `adx_editexpiredstatecode`, `adx_editexpiredstatuscode` | "Can no longer be edited" handling. |

### Modes

`adx_mode` maps to the form view mode in `EntityForm.cs`:

| `adx_mode` | Mode | Behaviour |
|---|---|---|
| `100000000` | **Insert** (Create) | Creates a record on save. |
| `100000001` | **Edit** | Loads the record identified by the query-string id and updates it. |
| `100000002` | **ReadOnly** | Displays the record without saving. |

Any other value falls back to the control's default mode. In Edit and ReadOnly modes the form needs the
record to resolve through its **entity source**; if it does not, the `adx_recordnotfoundmessage` (or the
default) is displayed and the form is not rendered.

## Metadata records

Each `adx_entityformmetadata` record targets part of the form — an attribute, a section, a tab, a
subgrid, or the whole form — and changes how it is presented. Common customizations:

| Kind | What metadata can do |
|---|---|
| **Attribute** | Override the label, add a description and its position, mark required, set a CSS class, choose a control style, set a default/prepopulated value, add a regular-expression validation and its message, restrict to a range or a set of allowed values, render read-only/hidden, or replace the control entirely. |
| **Section / tab** | Rename, reorder, show/hide, change layout. |
| **Subgrid** | Configure a child list (`adx_subgrid_name`, `adx_subgrid_settings`) embedded in the form. |
| **Lookup** | Populate or restrict a lookup's choices; configure the modal lookup experience (whose text and messages come from the `Portal/Lookup/Modal/*` site settings). |
| **Notes / timeline** | Configure the annotation/timeline control. |
| **Save behaviour** | Set additional attribute values on save (`adx_setvalueonsave`, `adx_onsavevalue`, `adx_onsavefromattribute`, `adx_onsavetype`). |

Metadata can also be expressed as **JSON** on the form, which is how newer configurations are stored;
both representations are understood.

### Prepopulating values

Metadata can prepopulate a field from:

- a constant value (`adx_prepopulatevalue`),
- a query-string parameter (`adx_prepopulatetype` + the parameter name),
- another field on the current record, or
- the current portal user or account.

This is the standard way to stamp an audit trail and to relate a new record to the signed-in contact.

### Setting values on save

`adx_setvalueonsave` with `adx_onsavevalue` / `adx_onsavefromattribute` writes a value when the record is
saved, which is how forms maintain ownership, status or a foreign key without user input.

## Rendering an entity form

### From Liquid

```liquid
{% entityform name: 'New Support Request' %}
```

With a query-string id in Edit mode (`?id=<guid>` by default, or the configured parameter name):

```liquid
{% entityform name: 'Edit Support Request' %}
```

### From a page template

Point a page's page template at the entity form; the built-in `EntityForm` control resolves it from the
template association. The `Web/UI/WebControls/EntityForm.cs` control is what actually renders it, and the
Liquid `{% entityform %}` tag delegates to it.

## Attachments

Attachment support is configured on the form:

| Column | Meaning |
|---|---|
| `adx_attachfile` | Enable attachments. |
| `adx_attachfilelabel` | Label for the file input. |
| `adx_attachfilerequired`, `adx_attachfilerequirederrormessage` | Require an attachment. |
| `adx_attachfileallowmultiple`, `adx_attachfilemaxsize` | Multiple files and size limit. |
| `adx_attachfileaccept`, `adx_attachfilerestrictaccept` | Accepted file types. |
| `adx_attachfilesizeerrormessage`, `adx_attachfiletypeerrormessage` | Validation messages. |
| `adx_attachfilestoragelocation` | Where the file is stored. |
| `adx_attachfilesaveoption` | When the attachment is written. |

Attachments become **annotations** (notes) on the record, stored in Dataverse or in Azure Blob Storage.
See [Web Files and Attachments](Web-Files-and-Attachments.md).

## Validation

Validation happens at two levels:

1. **Dataverse field metadata** — required-ness and types come from the table.
2. **Portal metadata and form columns** — descriptions, required overrides, regular expressions, ranges,
   multiple-choice constraints, and the messages shown when they fail.

Client-side validation uses the unobtrusive jQuery validation bundle (`js/jqueryval.bundle.js`), which is
registered in `SetupConfig.RegisterBundles`; server-side validation happens again in the control.

## Redirects and success behaviour

After a successful save the form can:

- redirect to `adx_redirecturl`,
- redirect to a web page via `adx_redirectwebpage`, optionally appending the record id
  (`adx_redirecturlappendentityidquerystring`) with custom query-string values
  (`adx_redirecturlcustomquerystring`, `adx_redirecturlquerystring*`), or
- re-render with `adx_successmessage`, optionally hiding the form (`adx_hideformonsuccess`).

`adx_redirect` / `adx_redirecturl` also exist on web pages, so a "thank you" flow can be modelled either
on the form or on the page.

## Security and permissions

`adx_entitypermissionsenabled` turns on enforcement of **table permissions** for the form's table. The
control evaluates the current user's web roles against the `adx_entitypermission` records and:

- refuses to load a record the user cannot read,
- switches to a read-only presentation or refuses to save when the user lacks create/write,
- hides or disables fields the user may not change.

Never rely on field-level hiding as a security measure — the permission records are the authority.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| "Record not found" message | The entity source did not resolve — usually a missing or wrong `adx_recordidquerystringparametername` parameter, or the user cannot read the record. |
| Form renders but saving does nothing | Validation failing silently, or missing create/write permission. |
| Fields missing | The Dataverse form named by `adx_formname` does not include them, or metadata hides them. |
| Attachment upload fails | Size limit (`maxRequestLength`), `adx_attachfilemaxsize`, or storage configuration. |
| Lookup modal text is wrong | The `Portal/Lookup/Modal/*` site settings. |
| Edit form cannot find the record | Ensure the record id is in the query string under the configured parameter name. |

## Related pages

- [Entity Lists](Entity-Lists.md)
- [Web Forms](Web-Forms.md) — multi-step forms
- [Web Files and Attachments](Web-Files-and-Attachments.md)
- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md#entityform)
- [Security and Authentication](Security-and-Authentication.md)
