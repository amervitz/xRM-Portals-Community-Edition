# Liquid Tags and Filters Reference

Every tag and filter the portal makes available in Liquid. The engine is **DotLiquid 2.0.385**, registered
in `Adxstudio.Xrm/Web/Mvc/Html/LiquidExtensions.cs`.

For objects, see [Liquid Objects Reference](Liquid-Objects-Reference.md). For the tutorial, see
[Web Templates and Liquid](Web-Templates-and-Liquid.md).

---

## Standard DotLiquid tags

These come from DotLiquid itself and behave as the Liquid standard specifies.

| Tag | Purpose |
|---|---|
| `{% assign name = expr %}` | Assign a variable. |
| `{% capture name %}…{% endcapture %}` | Capture rendered output into a variable. |
| `{% if %}` / `{% elsif %}` / `{% else %}` / `{% endif %}` | Conditionals. |
| `{% unless %}` / `{% endunless %}` | Negated conditional. |
| `{% case %}` / `{% when %}` / `{% else %}` / `{% endcase %}` | Multi-branch. |
| `{% for item in collection %}` / `{% endfor %}` | Iteration (`forloop` available inside). |
| `{% break %}` / `{% continue %}` | Loop control. |
| `{% cycle %}` | Alternating values inside a loop. |
| `{% tablerow %}` | Table-row generation. |
| `{% include 'name' %}` | Include another template (resolved through the file system). |
| `{% extends 'name' %}` / `{% block name %}…{% endblock %}` | Template inheritance. |
| `{% comment %}…{% endcomment %}` | Unrendered comment. |
| `{% raw %}…{% endraw %}` | Emit the enclosed text without rendering. |
| `{% increment %}` / `{% decrement %}` | Counters. |

### `include` and `extends` resolution

Both resolve against a **composite file system**:

1. `adx_webtemplate` records for the current website (looked up by `adx_name`), then
2. embedded framework templates in the `Adxstudio.Xrm.Liquid` resource namespace.

Create a web template with the same name as a shipped one to override it.

---

## Portal tags

Registered by `LiquidExtensions`. All are rendered server side.

### `entitylist`

Renders a list of Dataverse records.

```liquid
{% entitylist name: 'Active Cases' %}
{% entitylist id: 11111111-1111-1111-1111-111111111111 %}
{% entitylist key: 'active-cases' %}
```

| Attribute | Meaning |
|---|---|
| `name` | The `adx_entitylist` name. |
| `id` | The `adx_entitylist` id. |
| `key` | The `adx_key`, when that column exists on the record (newer schema). |
| `language_code` / `languagecode` | Language to render in. |

The tag also assigns the resulting `EntityListDrop` into the Liquid context under the name `entitylist`
(or the variable name, when the tag is used as a variable assignment).

### `entityform`

Renders a form over a Dataverse record.

```liquid
{% entityform name: 'New Support Request' %}
{% entityform key: 'new-support-request' %}
```

| Attribute | Meaning |
|---|---|
| `name`, `id`, `key` | Which `adx_entityform` to render. |

### `entityview`

Renders a Dataverse view.

```liquid
{% entityview logical_name: 'incident', name: 'Active Cases' %}
```

| Attribute | Meaning |
|---|---|
| `logical_name` / `logicalname` | The table. |
| `name` / `id` / `key` | Which entity list / view to use. |
| `page`, `page_size` / `pagesize` | Paging. |
| `search` | Search term. |
| `filter`, `metafilter` | Filter selections. |
| `order` | Sorting. |
| `language_code` / `languagecode` | Language. |

### `fetchxml`

Runs a FetchXML query and exposes its results.

```liquid
{% fetchxml query enable_entity_permissions: true, right: 'read' %}
  <fetch>
    <entity name="incident">
      <attribute name="title" />
    </entity>
  </fetch>
{% endfetchxml %}

{% for record in query.results.entities %}
  {{ record.title }}
{% endfor %}
```

| Attribute | Meaning |
|---|---|
| `right` | The permission right required (for example `read`). |
| *variable* | The name bound to the query (defaults to `query` when omitted). |

`enable_entity_permissions: true` enforces table permissions on the query.

### `webform`

Renders a multi-step web form.

```liquid
{% webform name: 'Support Request' %}
```

| Attribute | Meaning |
|---|---|
| `name`, `id`, `key` | Which `adx_webform` to render. |

### `chart`

Renders a Dataverse chart.

```liquid
{% chart id: "527B944C-E948-4012-BB10-F00A538F9903" %}
{% chart id: "…", viewid: "…", serviceurl: "…" %}
```

| Attribute | Meaning |
|---|---|
| `id` | The chart (saved visualization) id. |
| `viewid` | The view the chart uses. |
| `serviceurl` | An override for the organization service URL. |

### `searchindex`

Renders search results.

```liquid
{% searchindex query: 'billing', page: 1, page_size: 10 %}
```

| Attribute | Meaning |
|---|---|
| `query` | The search text. |
| `filter` | Facet filter selections. |
| `logical_names` / `logicalnames` | Restrict to specific tables. |
| `provider` | Use a specific search provider. |
| `page`, `page_size` / `pagesize` | Paging. |

### `editable`

Renders a value as editable markup for authorized users.

```liquid
{% editable page.title %}
{% editable snippets['Footer/Legal'] %}
{% editable page.content key: 'adx_copy' type: 'html' %}
```

| Part | Meaning |
|---|---|
| first argument | The editable object (a drop implementing `IEditable`) or a key. |
| second argument | An optional key. |
| `type` | The editor type (for example `html`, `text`). |

### `rating`

Renders a rating control. The XML documentation on the tag gives the full form:

```liquid
{% rating id: page.id, entity: page.logical_name, readonly: false, panel: true, snippet: "Rating Heading", step: "1", min: "0", max: "5", round: true %}
```

| Attribute | Meaning |
|---|---|
| `id` (**required**) | The record id. |
| `entity` | The table; defaults to `adx_webpage`. |
| `readonly` | Render without allowing changes. |
| `panel` | Render the surrounding panel. |
| `snippet` | Snippet name for the panel heading. |
| `step`, `min`, `max` | Rating scale. |
| `round` | Round the displayed average. |

### `redirect`

Redirects the response to a site marker's page.

```liquid
{% redirect sitemarker: 'Thank You' %}
```

| Attribute | Meaning |
|---|---|
| `sitemarker` (**required**) | The site marker name to redirect to. |

Without the attribute, the tag produces no output.

### `outputcache`

Sets the response's cacheability. It is a **statement**, not a block.

```liquid
{% outputcache cacheability: 'Server' %}
```

| Attribute | Meaning |
|---|---|
| `cacheability` | An `HttpCacheability` value (`NoCache`, `Private`, `Public`, `Server`, `ServerAndClient`, `NoStore`). |

The tag does nothing if it cannot access the response cache policy.

### `substitution`

Renders a donut-cache substitution placeholder for arbitrary Liquid inside the block. When the parent view
supports donut caching, the block is replaced by a placeholder that is rendered per request; otherwise the
Liquid is rendered inline.

```liquid
{% substitution %}
  {{ user.fullname }}
{% endsubstitution %}
```

### `{% editable %}` and related helpers

Besides the `editable` tag, drops expose `get_editable(context, …)` methods — used when you need to build
editable markup from a drop directly.

---

## Filters

Filters are registered from ten classes. Filter names in Liquid are the lowercased, snake-cased method
names (the invariant-culture naming convention).

### Core — `Filters`

| Filter | Signature | Purpose |
|---|---|---|
| `default` | `default: <value>` | Returns the value if present, otherwise the default. |
| `file_size` | `file_size: <precision>` | Formats a byte count as a human-readable size (default precision 1). |
| `has_role` | `has_role: '<Role>'` | True when the given `user` holds the role. |
| `liquid` | `liquid` | Renders the input as Liquid, returning the rendered string. |
| `web_template` | `web_template` | Renders the input as a web template reference / Liquid source. |

### Date — `DateFilters`

| Filter | Signature | Purpose |
|---|---|---|
| `date_add_days` | `date_add_days: <double>` | Adds days. |
| `date_add_hours` | `date_add_hours: <double>` | Adds hours. |
| `date_add_minutes` | `date_add_minutes: <double>` | Adds minutes. |
| `date_add_seconds` | `date_add_seconds: <double>` | Adds seconds. Keeps fractional parts (Power Pages truncates). |
| `date_add_months` | `date_add_months: <int>` | Adds months. |
| `date_add_years` | `date_add_years: <int>` | Adds years. |
| `date_to_local` | `date_to_local` | Converts to local time. |
| `date_to_utc` | `date_to_utc` | Converts to UTC. |
| `date_to_iso8601` | `date_to_iso8601` | ISO 8601 string. |
| `date_to_rfc822` | `date_to_rfc822` | RFC 822 string. |
| `date_to_xml_schema` | `date_to_xml_schema` | XML schema (xs:dateTime) string. |

The standard `date` filter (with a .NET format string) is provided by DotLiquid and is the one you will
use most: `{{ now | date: 'yyyy-MM-dd HH:mm:ss' }}`.

### Entity lists — `EntityListFilters`

| Filter | Purpose |
|---|---|
| `current_sort` | The current sort selection. |
| `metafilters` | The meta-filters applied to a list. |
| `reverse_sort` | Reverses the current sort direction. |

### Enumerable — `EnumerableFilters`

| Filter | Signature | Purpose |
|---|---|---|
| `all` | `all` | Materializes/returns the collection. |
| `batch` | `batch: <size>` | Splits into batches of the given size. |
| `concat` | `concat: <other>` | Concatenates two collections. |
| `except` | `except: '<key>', <value>` | Excludes items whose key equals the value. |
| `group_by` | `group_by: '<key>'` | Groups items. |
| `order_by` | `order_by: '<key>', '<asc|desc>'` | Orders items (default `asc`). |
| `then_by` | `then_by: '<key>', '<asc|desc>'` | Secondary ordering. |
| `paginate` | `paginate: <index>, <count>` | A page of items. |
| `top` | `top: <pageSize>, '<lang>'` | Top items, language aware. |
| `recent` | `recent: <pageSize>, '<lang>'` | Recent items, language aware. |
| `popular` | `popular: <pageSize>, '<lang>'` | Popular items, language aware. |
| `top_level` | `top_level: <pageSize>` | Top-level items. |
| `category_number` | `category_number: '<number>'` | Items in a category number. |
| `related` | `related: '<categoryId>', <pageSize>` | Related items. |
| `random` | `random` | Random order (uses the Liquid context). |
| `shuffle` | `shuffle` | Shuffles (uses the Liquid context). |
| `select` | `select: '<key>'` | Projects a key from each item. |
| `skip` | `skip: <count>` | Skips items. |
| `take` | `take: <count>` | Takes items. |
| `where` | `where: '<key>', <value>` | Filters items by key equality. |

Both `order_by`/`then_by` and `orderby`/`thenby` spellings are accepted.

### Math — `MathFilters`

| Filter | Purpose |
|---|---|
| `ceil` | Rounds up. |
| `floor` | Rounds down. |
| `round` | Rounds to the nearest value. |

### Type conversion — `TypeFilters`

| Filter | Purpose |
|---|---|
| `boolean` | Converts to boolean. |
| `decimal` | Converts to decimal. |
| `integer` | Converts to integer. |
| `string` | Converts to string. |

Use these when a site setting (always a string) needs a typed value.

### String — `StringFilters`

| Filter | Purpose |
|---|---|
| `text_to_html` | Converts plain text to HTML (newlines to paragraphs/breaks). |
| `url_escape` | URL-escapes the value. |
| `xml_escape` | XML-escapes the value. |

### Number format — `NumberFormatFilters`

| Filter | Signature | Purpose |
|---|---|---|
| `base_currency` | `base_currency` | Formats using the base currency. |
| `currency` | `currency` | Formats as currency. |
| `decimals` | `decimals: <n>` | Formats with `n` decimals. |
| `max_decimals` | `max_decimals: <n>` | Formats with at most `n` decimals. |
| `format` | `format: '<format>'` | Formats with a .NET format string. |
| `invariant_culture_decimal_value` | `invariant_culture_decimal_value` | Decimal value using invariant culture. |

### URL — `UrlFilters`

| Filter | Signature | Purpose |
|---|---|---|
| `add_query` | `add_query: '<name>', <value>` | Adds or replaces a query-string parameter. |
| `remove_query` | `remove_query: '<name>'` | Removes a query-string parameter. |
| `base` | `base` | The URL base (scheme + host + port). |
| `host` | `host` | The host name of a URL. For a relative URL this is the requesting site's host. |
| `path` | `path` | The URL path. |
| `path_and_query` | `path_and_query` | Path plus query string. |
| `port` | `port` | The port. |
| `scheme` | `scheme` | The scheme. |
| `is_sitemap_current` | `is_sitemap_current: '<url>'` | True when the URL is the current sitemap node. |
| `is_sitemap_ancestor` | `is_sitemap_ancestor: '<url>'` | True when the URL is an ancestor of the current node. |

### Search — `SearchFilterOptionFilters`

| Filter | Purpose |
|---|---|
| `search_filter_options` | Produces the facet filter option list for the search UI. |

### DotLiquid standard filters

Also available (from DotLiquid): `date`, `escape` / `h`, `size`, `downcase`, `upcase`, `capitalize`,
`strip`, `lstrip`, `rstrip`, `strip_html`, `strip_newlines`, `split`, `join`, `map`, `first`, `last`,
`sort`, `sort_natural`, `reverse`, `uniq`, `compact`, `concat`, `default`, `plus`, `minus`, `times`,
`divided_by`, `modulo`, `abs`, `ceil`, `floor`, `round`, `truncate`, `truncatewords`, `replace`,
`replace_first`, `remove`, `remove_first`, `append`, `prepend`, `newline_to_br`, `url_encode`,
`url_decode`, `date`, `slice`.

> DotLiquid is compiled against .NET, so some standard Liquid filters may behave slightly differently from
> the Ruby/Liquid.js implementations. Verify anything subtle.

---

## Compatibility with Power Pages

| Difference | Detail |
|---|---|
| `html_safe_escape` | **Does not exist here.** In Power Pages it sanitizes HTML; here there is no equivalent filter, so values pass through unchanged. Do not rely on it. |
| `encode` | **Does not exist here.** Power Pages HTML-encodes `user` and `request` output by default; this codebase does not, so add `\| escape` yourself. |
| `date_add_seconds` | Keeps fractional seconds; Power Pages drops them. |
| Date parsing | Time-zone-bearing strings and `'now'` resolve to **server local time**; Power Pages runs in UTC. |
| `identifier` | xRM Portals has extras Power Pages may lack (`resx`, `uniqueId`, `sharepoint`, `rating`, `redirect`). |

See [`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md) for the test
matrix.

---

## Related pages

- [Liquid Objects Reference](Liquid-Objects-Reference.md)
- [Web Templates and Liquid](Web-Templates-and-Liquid.md)
- [Extensibility](Extensibility.md) — registering your own tags, filters and globals
