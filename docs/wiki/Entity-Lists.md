# Entity Lists

An **entity list** renders a table of Dataverse records — with paging, sorting, searching, filtering,
actions, and optional calendar and map views. It is the portal equivalent of a Power Pages **list**.

An entity list is an `adx_entitylist` record, referenced by an `{% entitylist %}` tag or by a page
template.

## The record

| Column | Purpose |
|---|---|
| `adx_name` | Name, used to reference the list (`{% entitylist name: 'Active Cases' %}`). |
| `adx_entityname` | The Dataverse **logical name** of the table to list (for example `incident`). |
| `adx_view` | The selected view ids as a **comma-delimited list** (legacy form). |
| `adx_views` | **JSON** view metadata (the newer form). Parsed to find the view by id. |
| `adx_pagesize` | Rows per page. |
| `adx_searchenabled` | Whether the search box appears. |
| `adx_searchplaceholdertext`, `adx_searchtooltiptext` | Search box text. |
| `adx_emptylisttext` | What to show when there are no rows. |
| `adx_recordnotfoundmessage` | Shown when a requested record does not exist. |
| `adx_createbuttonlabel`, `adx_detailsbuttonlabel` | Button text. |
| `adx_filteraccount`, `adx_filterportaluser` | Field names used to scope rows to the current account or portal user. |
| `adx_review`, `adx_reviewer*` | Review/approval workflow fields. |
| `adx_entitypermissionsenabled` | Whether table permissions are enforced for this list. |
| `adx_previewunpublishedentities` | Whether unpublished records can appear. |
| `adx_createautonumber`, `adx_create`, `adx_delete` | Record-creation and deletion behaviour. |
| `adx_webpageforcreate`, `adx_webpagefordetailsview`, `adx_entityformforcreate` | Pages/forms to route to for creating and viewing records. |
| `adx_calendar_*` | Calendar view configuration (see below). |
| `adx_map_*`, `adx_bingmaplookup` | Map view configuration (see below). |
| `adx_subgrid_name`, `adx_subgrid_settings` | Subgrid (child list) configuration. |

### Views

An entity list does not define its own columns and sort order. It selects a Dataverse **`savedquery`**
(system view) for the table named in `adx_entityname`:

- **Newer configuration** — `adx_views` holds JSON view metadata (the columns to display, widths and
  sorting); the list looks up the requested view by id inside that JSON.
- **Legacy configuration** — `adx_view` holds a comma-delimited list of view GUIDs.

The view's FetchXML drives the query. If a view is missing or belongs to a different table, the list
fails to resolve (the adapter checks that the view's `returnedtypecode` matches the list's
`adx_entityname`).

`adx_views` JSON that cannot be parsed is logged as `Error parsing adx_views JSON` and the adapter falls
back to `adx_view`.

### Actions

Row actions come from the **view's** action configuration (the JSON on the view) plus the list's
create/details routing:

- **Details / Create** — `adx_webpagefordetailsview`, `adx_webpageforcreate`, `adx_entityformforcreate`.
- **Edit / Delete / Associate / Download** — driven by view action configuration and the
  `adx_entitypermission` rights for the current user.
- Custom action targets: link targets, workflows, and *download* actions.

JavaScript handles the client side (`js/entity-grid.js`, `js/entity-associate.js`,
`js/entity-lookup.js`, `js/serialized-query.js`, and the area-specific scripts).

## Calendar view

Set `adx_calendar_enabled` to add a calendar presentation. The list needs field names so the calendar can
place records:

| Column | Meaning |
|---|---|
| `adx_calendar_startdatefieldname` | Start date field. |
| `adx_calendar_enddatefieldname` | End date field. |
| `adx_calendar_summaryfieldname` | Summary/title field. |
| `adx_calendar_descriptionfieldname` | Description field. |
| `adx_calendar_organizerfieldname` | Organizer field. |
| `adx_calendar_locationfieldname` | Location field. |
| `adx_calendar_alldayfieldname` | All-day flag field. |
| `adx_calendar_initialdate`, `adx_calendar_initialview`, `adx_calendar_style` | Initial state and style. |
| `adx_calendar_timezone`, `adx_calendar_timezonemode` | Time-zone display. |

The calendar data adapter also reads the user's time zone, so records display in the viewer's local time
where configured.

## Map view

Set `adx_map_enabled` to add a map presentation:

| Column | Meaning |
|---|---|
| `adx_map_latitude`, `adx_map_longitude` | Fields holding coordinates. |
| `adx_map_zoom` | Initial zoom. |
| `adx_map_resturl`, `adx_map_credentials` | Map service endpoint and key. |
| `adx_map_pushpinurl`, `adx_map_pushpinwidth`, `adx_map_pushpinheight` | Pushpin image. |
| `adx_map_distanceunits`, `adx_map_distancevalues` | Distance search options. |
| `adx_map_infoboxoffsetx`, `adx_map_infoboxoffsety` | Info box placement. |

Map-related site settings you may need: `BingMap/Key`, `BingMap/RestUrl`,
`BingMap/IncludeNeighborhood`, `BingMap/UserLocation/Latitude`, `BingMap/UserLocation/Longitude`, and the
`EntityList/Map/*` label settings.

## Rendering an entity list

### From Liquid

```liquid
{% entitylist name: 'Active Cases' %}
```

The shipped templates `entity_list`, `entity_list_grid` and `entity_list_filter` show a full
implementation, including the filter panel. A named entity list can also be rendered with
`| entitylist` style helpers, and individual drops (`EntityListDrop`, `EntityListViewDrop`,
`EntityListViewColumnDrop`) are available in templates.

### From a page template

Point a page at a page template whose associated record is the entity list. The built-in
`Pages/EntityList`-supporting pages and the `Areas/EntityList` controllers implement the conventional
listing experience.

## Filtering

There are three layers of filtering:

1. **The view's FetchXML** — the baseline query.
2. **Record-level scoping** — `adx_filteraccount` and `adx_filterportaluser` restrict rows to the current
   account or portal user by comparing those fields.
3. **The filter panel** — the `adx_filter_*` columns (`adx_filter_enabled`, `adx_filter_definition`,
   `adx_filter_applybuttonlabel`, `adx_filter_orientation`) define an interactive filter UI, surfaced by
   the `entity_list_filter` template. `EntityListFilters` and `SearchFilterOptionFilters` provide the
   Liquid-side helpers (`current_sort`, `metafilters`, `reverse_sort`, `search_filter_options`).

**Record scoping is not a security boundary.** It shapes the query; authorization comes from table
permissions. See the warning in [Security and Authentication](Security-and-Authentication.md).

## Security

When `adx_entitypermissionsenabled` is on, the list enforces **table permissions** (`adx_entitypermission`)
for the signed-in user's web roles before returning records, and the controls hide actions the user is not
permitted to perform. Permission decisions are cached per user, role set, website and language.

A list on a table the user has no permission to read returns nothing (or an access-denied message,
depending on configuration) — this is expected.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Empty list, no error | The view resolved but the user has no table permission; or the FetchXML matches nothing; or `adx_emptylisttext` is hiding the problem. |
| Nothing at all renders | The entity list name does not resolve, or the view id does not match the table's `returnedtypecode`. |
| `Error parsing adx_views JSON` in the trace | Malformed `adx_views`. Fix the JSON or fall back to `adx_view` GUIDs. |
| Calendar is empty | The `adx_calendar_*` field names do not match the table's columns. |
| Map shows no pins | Missing latitude/longitude field names, or `adx_entitylist` → `adx_map_*` misconfigured; also check the Bing Maps key. |
| Columns are wrong | Columns come from the **view**, not the entity list. Edit the view. |

## Related pages

- [Entity Forms](Entity-Forms.md) — creating and editing the records the list shows
- [Security and Authentication](Security-and-Authentication.md)
- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md#entitylist)
- [Portal Data Model](Portal-Data-Model.md)
