# Web Forms

A **web form** is a multi-step process that walks a visitor through a sequence of steps, creates or
updates Dataverse records along the way, and can branch, redirect and run logic. Use it for anything more
complex than a single entity form: applications, registrations, multi-part surveys, guided data capture.

A web form is an `adx_webform` record with an ordered set of **steps** (`adx_webformstep`), each with its
own metadata, and a **session** record (`adx_webformsession`) tracking a visitor's progress.

## The record

| Column | Purpose |
|---|---|
| `adx_name` | Referenced by `{% webform name: 'Support Request' %}`. |
| `adx_startnewsessiononload` | Start a fresh session when the form loads. |
| `adx_progressindicatorenabled` | Show a progress indicator. |
| `adx_progressindicatorposition` | Where the indicator sits. |
| `adx_progressindicatorprependstepnum` | Prefix step numbers. |
| `adx_progressindicatortype` | Indicator style. |
| `adx_progressindicatorignorelaststep` | Whether the final step counts. |
| `adx_nextbuttontext`, `adx_nextbuttoncssclass` | Next button. |
| `adx_previousbuttontext`, `adx_previousbuttoncssclass` | Previous button. |
| `adx_submitbuttontext`, `adx_submitbuttoncssclass`, `adx_submitbuttonbusytext` | Submit button. |
| `adx_savechangeswarningmessage`, `adx_savechangeswarningonclose` | Unsaved-change warnings. |
| `adx_authenticationrequired` | Require sign-in. |
| `adx_captcharequired`, `adx_showcaptchaforauthenticatedusers` | Captcha policy. |
| `adx_recordnotfoundmessage` | Record-not-found text. |
| `adx_editexpiredmessage`, `adx_editexpiredstatecode`, `adx_editexpiredstatuscode` | "Editing expired" handling. |

## Steps

Each `adx_webformstep` has a **type** and a **mode**.

### Step types

`WebFormStepType` (column `adx_type`):

| Value | Type | Purpose |
|---|---|---|
| `100000000` | **Condition** | Branches: evaluates a condition and directs flow to another step. |
| `100000001` | **LoadForm** | Renders a form to capture or update data. |
| `100000002` | **LoadTab** | Renders tabbed content. |
| `100000003` | **Redirect** | Sends the visitor to a URL or web page. |
| `100000004` | **LoadUserControl** | Renders a custom ASP.NET user control. |

### Step modes

`WebFormStepMode` (column `adx_mode`), for the data-capturing step types:

| Value | Mode | Behaviour |
|---|---|---|
| `100000000` | **Insert** | Creates a record. |
| `100000001` | **Edit** | Updates an existing record. |
| `100000002` | **ReadOnly** | Displays without saving. |

### Entity source types

Where the step's record comes from — `WebFormStepSourceType` (column `adx_entitysourcetype`):

| Value | Source | Meaning |
|---|---|---|
| `100000001` | **QueryString** | A record id supplied in the query string. |
| `100000002` | **CurrentPortalUser** | The signed-in contact. |
| `100000003` | **ResultFromPreviousStep** | The record created or found by an earlier step. |

This is what makes multi-step forms work: step one creates a record, and step two's mode is Edit with
source type *ResultFromPreviousStep*, so it updates the same record.

### Other step columns

| Group | Columns |
|---|---|
| **Navigation** | `adx_nextstep`, `adx_previousstep`, `adx_currentstepindex`, `adx_editstage` |
| **Entity source** | `adx_entityname`, `adx_entityformforcreate`, `adx_entitysourcetype`, `adx_referenceentity*`, `adx_referencequerystring*`, `adx_recordsource*`, `adx_primaryrecordentity*`, `adx_primarykey*`, `adx_idquerystringparametername`, `adx_recordidquerystringparametername` |
| **Prepopulation** | `adx_prepopulatetype`, `adx_prepopulatevalue`, `adx_prepopulatefromattribute`, `adx_populatereferenceentitylookupfield`, `adx_populateentityreferencelookupfield` |
| **On save** | `adx_setvalueonsave`, `adx_onsavevalue`, `adx_onsavefromattribute`, `adx_onsavetype`, `adx_setentityreference`, `adx_associatecurrentportaluser` |
| **Condition** | `adx_condition`, `adx_fromstate`, `adx_tostate`, `adx_targetentitylogicalname`, `adx_targetentityprimarykeylogicalname`, `adx_targetentityportaluserlookupattribute` |
| **Messages** | `adx_validationerrormessage`, `adx_requiredfieldvalidationerrormessage`, `adx_rangevalidationerrormessage`, `adx_multiplechoicevalidationerrormessage`, `adx_constantsum*`, `adx_ranking*` |
| **Attachments** | `adx_attachfile*` (same set as entity forms) |
| **Progress** | `adx_progressindicator*`, `adx_autogeneratesteps` |
| **UI / script** | `adx_usercontrolpath`, `adx_usercontroltitle`, `adx_mode`, `adx_type`, `adx_disablepagevalidation` |

Metadata (`adx_webformmetadata`) customizes fields, sections and tabs exactly as it does for entity forms.

## Rendering a web form

### From Liquid

```liquid
{% webform name: 'Support Request' %}
```

### From a page template

The built-in `Pages/WebForm.aspx` (and `WebFormNoValidation.aspx`) render web forms referenced by a page
template, wrapped in a Web Forms master page.

## Sessions

A visitor's progress is tracked in an `adx_webformsession`. Sessions let a form survive a page reload, and
they record which step the visitor is on and which record the run has produced. `adx_startnewsessiononload`
resets that state — useful on an entry page when you want a clean start.

## Progress indicator

When `adx_progressindicatorenabled` is on, the form renders a progress indicator. Position comes from
`adx_progressindicatorposition` (Top, Bottom, Left, Right per the underlying enum values `756150000`–
`756150003`), the style from `adx_progressindicatortype`, and step numbering from
`adx_progressindicatorprependstepnum`. `adx_progressindicatorignorelaststep` keeps the confirmation step
out of the count.

## Conditions and branching

A **Condition** step evaluates `adx_condition` and routes to a target step. Conditions can reference:

- the current record's fields,
- values captured earlier in the same run,
- the signed-in portal user or their account,
- states recorded on a target entity (`adx_fromstate`, `adx_tostate`, `adx_targetentity*`).

Model branching deliberately and keep the graph acyclic; a loop of conditions with no terminating path is
a support call waiting to happen.

## Security

- `adx_authenticationrequired` forces sign-in before the form runs.
- Table permissions (`adx_entitypermission`) govern what the step can actually create or update. A step
  can be configured and still fail at save time if the user's web roles lack the right.
- Captcha policy is per form (`adx_captcharequired`, `adx_showcaptchaforauthenticatedusers`) and also
  governed by `Authentication/Registration/CaptchaEnabled`.

## Scripts

Client behaviour is in `js/quickform.js` together with the shared entity-form scripts
(`js/entity-form.js`, `js/entity-associate.js`, `js/entity-lookup.js`). `adx_registerstartupscript` on the
relevant records lets you inject additional script.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| A step's record is not found | Wrong entity source type, missing query-string parameter, or the user cannot read the record. |
| Form never advances | A condition step with no matching target, or a validation failure on the step. |
| Second step creates a duplicate | Mode is Insert where it should be Edit with *ResultFromPreviousStep*. |
| Session state looks stale | Reset with `adx_startnewsessiononload`, or clear the session record. |
| Progress indicator is wrong | Indicator columns on the form and steps disagree. |
| Save fails with no message | Missing table permission for create/write. |

## Related pages

- [Entity Forms](Entity-Forms.md) — the single-record form used inside steps
- [Security and Authentication](Security-and-Authentication.md)
- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md#webform)
- [Portal Data Model](Portal-Data-Model.md)
