# Web Templates and Liquid

**Liquid** is the template language used across the portal: in web templates, in content snippets, in
web page content, and in many configuration values. Anyone customizing a portal ends up writing Liquid.
This page is the tutorial; the exhaustive lists are in
[Liquid Objects Reference](Liquid-Objects-Reference.md) and
[Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md).

## Where Liquid runs

| Context | Source of the Liquid |
|---|---|
| **Web template** (`adx_webtemplate`) | `adx_source`, rendered by a Web Template page template. |
| **Content snippet** (`adx_contentsnippet`) | `adx_value`. |
| **Web page content** | `adx_copy` when rendered by a Liquid-aware template. |
| **Liquid-enabled web controls** | Properties on entity forms, web forms and snippets that accept Liquid. |
| **Configuration values** | Fields whose values are passed through the `liquid` filter. |

## The engine

The portal uses **DotLiquid 2.0.385** — a best guess at the version Power Pages uses, chosen so that the
two render the same Liquid (see
[`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md)).

Two details of the implementation matter to template authors:

1. **Naming convention.** The portal registers an `InvariantCultureNamingConvention`, which converts
   .NET member names to lowercase with underscores (for example `WebTemplateId` → `web_template_id`).
   This avoids a bug in Turkish cultures where `id` does not map correctly. In practice: refer to members
   in **lowercase**, and use underscores where a name is composed of multiple words.
2. **Errors are printed, not thrown.** A syntax error is written into the page as its message text
   (DotLiquid's `SyntaxException.Message`), and render errors are logged. If you see an odd sentence in
   the middle of a page, that is a Liquid parse error.

Liquid is **on by default**. The site setting `Liquid/Enabled` (read as a boolean, defaulting to `true`)
can turn it off; when it is off the template source is emitted verbatim instead of being rendered.

## A first template

A web template renders with a set of **global objects** always in scope. The most useful are `page`,
`user`, `request`, `website`, `settings`, `sitemap`, `snippets` and `now`.

```liquid
{% assign account_company = user.parentcustomerid.name %}

<h1>{{ page.title | escape }}</h1>

{% if user %}
  <p>Welcome back, {{ user.fullname }}.</p>
{% else %}
  <p>Welcome, guest. <a href="{{ request.url | add_query: 'returnUrl', request.path }}">Sign in</a></p>
{% endif %}

<p>Rendered at {{ now | date: 'yyyy-MM-dd HH:mm:ss' }} UTC.</p>
```

Things worth noticing:

- `{% assign %}` and `{% if %}` are standard Liquid tags.
- `page`, `user`, `request`, `now` are portal globals.
- Filters are applied with `|`, and named arguments use `name: value`.
- `now` is `DateTime.UtcNow` — it is **UTC on all environments**, unlike date parsing (see the
  compatibility note below).

## Objects: the drop model

Every portal object exposed to Liquid is a **drop**. A drop behaves like a dictionary whose keys are
lowercased property names, and it lazily loads related data.

- `page` — the current **web page**: `id`, `title`, `copy`, `summary`, `url`, `breadcrumbs`,
  `parent`, `children`, `publishing_state`, tags, ratings, and more.
- `user` — the signed-in **contact**: `id`, `fullname`, `firstname`, `lastname`, `emailaddress1`,
  related lookups such as `parentcustomerid`.
- `request` — the HTTP request: `url`, `path`, `path_and_query`, `params`, `querystring`, `host`,
  `scheme`, `port`.
- `params` — shorthand for `request.params`.
- `website` — the current `adx_website`: `id`, `name`, `primary_domain_name`, languages.
- `settings` — **site settings** addressed by name: `{{ settings['Authentication/Registration/OpenRegistrationEnabled'] }}`.
- `sitemap` — navigation: `sitemap.current`, `sitemap.root`, `sitemap.breadcrumbs`, children traversal.
- `sitemarkers` — named page pointers: `{{ sitemarkers['Home'].url }}`.
- `snippets` — content snippets by name.
- `weblinks` — web link sets by name.
- `entities` — construct queries.
- `now` — the current UTC date and time.
- `context` — lower-level view context, plus `context.user` and `context.website`.
- `resx` — localized UI strings.
- `uniqueId` — generates unique DOM ids.

Feature-specific drops appear alongside: `forums`, `blogs`, `events`, `knowledge`, `ads`, `polls`,
`sharepoint`.

Drops that represent Dataverse records (for example `page`, or an entity from a query) expose the
record's attributes as lowercased column names, and related records through their relationship lookups.
[Liquid Objects Reference](Liquid-Objects-Reference.md) lists the properties.

### Registers

A few values are provided as DotLiquid **registers** rather than globals:

- `htmlHelper` — the current ASP.NET HTML helper.
- `file_system` — the template file system used by `include` and `extends`.
- `portalLiquidContext` — the portal request context.

You normally do not touch these, but the `file_system` register is what makes `include`/`extends` resolve
against Dataverse web templates and embedded templates.

## Tags

The portal registers these custom tags in addition to the standard DotLiquid set. Each has its own page
section in [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md).

| Tag | Purpose |
|---|---|
| `{% entitylist %}` | Render a list of Dataverse records. |
| `{% entityform %}` | Render a form over a Dataverse record. |
| `{% entityview %}` | Render a Dataverse view. |
| `{% fetchxml %}` | Run a FetchXML query and iterate its results. |
| `{% webform %}` | Render a multi-step web form. |
| `{% chart %}` | Render a chart. |
| `{% searchindex %}` | Render search results / a search UI. |
| `{% editable %}` | Render an inline-editable value. |
| `{% rating %}` | Render a ratings control. |
| `{% redirect %}` | Redirect the response. |
| `{% outputcache %}` | Cache a fragment of output. |
| `{% substitution %}` | Substitute placeholders in a string. |

Standard DotLiquid tags are also available, including `assign`, `capture`, `increment`, `decrement`,
`if`/`elsif`/`else`/`unless`, `case`/`when`, `for`, `cycle`, `tablerow`, `include`, `extends`, `block`,
`comment`, `raw`, and `break`/`continue`.

## Filters

Filters are grouped by source class, all registered at start-up:

| Group | Examples |
|---|---|
| Core (`Filters`) | `default`, `file_size`, `has_role`, `liquid`, `web_template` |
| Date (`DateFilters`) | `date_add_days`, `date_add_hours`, `date_add_minutes`, `date_add_seconds`, `date_add_months`, `date_add_years`, `date_to_iso8601`, `date_to_local`, `date_to_rfc822`, `date_to_utc`, `date_to_xml_schema` |
| Entity lists (`EntityListFilters`) | `current_sort`, `metafilters`, `reverse_sort` |
| Enumerable (`EnumerableFilters`) | `all`, `batch`, `category_number`, `concat`, `except`, `group_by`, `order_by`, `paginate`, `popular`, `random`, `recent`, `related`, `select`, `shuffle`, `skip`, `take`, `then_by`, `top`, `top_level`, `where` |
| Math (`MathFilters`) | `ceil`, `floor`, `round` |
| Type conversion (`TypeFilters`) | `boolean`, `decimal`, `integer`, `string` |
| String (`StringFilters`) | `text_to_html`, `url_escape`, `xml_escape` |
| Number format (`NumberFormatFilters`) | `base_currency`, `currency`, `decimals`, `format`, `invariant_culture_decimal_value`, `max_decimals` |
| URL (`UrlFilters`) | `add_query`, `base`, `host`, `is_sitemap_ancestor`, `is_sitemap_current`, `path`, `path_and_query`, `port`, `remove_query`, `scheme` |
| Search facets (`SearchFilterOptionFilters`) | `search_filter_options` |

Plus the standard Liquid filters DotLiquid provides (`date`, `escape`, `size`, `split`, `join`, `map`,
`first`, `last`, `sort`, `default`, `url_encode`, `strip_html`, and so on).

> Note: `escape` is DotLiquid's HTML-escape filter. Power Pages additionally HTML-encodes output from
> `user` and `request` by default and offers an `encode` filter; this codebase does neither. See
> [compatibility](#compatibility-with-power-pages).

## Including and extending templates

Templates compose through the `file_system` register:

```liquid
{# include a partial #}
{% include 'breadcrumbs' %}

{# include with named arguments #}
{% include 'page_header', title: page.title %}

{# extend a layout #}
{% extends 'layout_2_column_wide_left' %}
{% block content %}
  {{ page.copy }}
{% endblock %}
```

`include` and `extends` resolve against **Dataverse web templates first, then embedded framework
templates**, so creating a web template named `breadcrumbs` overrides the shipped one.

## Reusable content: snippets

```liquid
{{ snippets['Footer/Legal'] }}

{% assign phone = snippets['Contact/Phone'] %}
{% if phone != blank %}
  <a href="tel:{{ phone }}">{{ phone }}</a>
{% endif %}
```

## Practical recipes

### Build a navigation menu from the page tree

```liquid
<ul class="nav">
  {% for child in page.parent.children %}
    <li class="{% if child.url == page.url %}active{% endif %}">
      <a href="{{ child.url }}">{{ child.title }}</a>
    </li>
  {% endfor %}
</ul>
```

### Show a curated menu from a web link set

```liquid
{% assign main_nav = weblinks['Main Navigation'] %}
{% if main_nav %}
  <nav>
    {% for link in main_nav.links %}
      <a href="{{ link.url }}"{% if link.open_in_new_window %} target="_blank" rel="noopener"{% endif %}>{{ link.name }}</a>
    {% endfor %}
  </nav>
{% endif %}
```

### Link to a page by marker

```liquid
<a href="{{ sitemarkers['Support'].url }}">Support</a>
```

### Render a localized string

```liquid
{{ resx['Submit_Button_Label'] }}
```

### Render a list of records

```liquid
{% entitylist name: 'Active Cases' %}
```

The `entitylist` tag accepts `name`, `id` or `key`, and an optional `language_code`. Row count, columns,
sorting and search come from the entity list record and its view — not from the tag.

### Render a form

```liquid
{% entityform name: 'New Support Request' %}
```

The `entityform` tag accepts `name`, `id` or `key`.

### Conditionally show content by role

```liquid
{% if user | has_role: 'Administrators' %}
  <a href="/admin">Administration</a>
{% endif %}
```

### Sort and slice a collection

```liquid
{% assign newest = blogs.posts | order_by: 'publish_date', 'descending' | take: 5 %}
{% for post in newest %}
  <h3><a href="{{ post.url }}">{{ post.title }}</a></h3>
{% endfor %}
```

### Control response caching

The `outputcache` tag sets the response's cacheability — it is a statement, not a block:

```liquid
{% outputcache cacheability: 'Server' %}
```

For a cached page that needs a per-visitor fragment (donut caching), use the `substitution` block, which
renders a substitution placeholder when the parent view supports donut caching and renders inline
otherwise:

```liquid
{% substitution %}
  {{ user.fullname }}
{% endsubstitution %}
```

### Date arithmetic

```liquid
<p>Expires {{ now | date_add_days: 30 | date: 'yyyy-MM-dd' }}</p>
```

### Escaping and safety

```liquid
{{ user.fullname }}                 {# not escaped #}
{{ user.fullname | escape }}        {# HTML-escaped — do this for untrusted text #}
{{ somevar | xml_escape }}          {# for XML/attribute contexts #}
{{ somevar | url_escape }}          {# for URL contexts #}
```

## Compatibility with Power Pages

Because template parity is a project goal, some known differences matter when you write or migrate
templates:

| Difference | What it means for you |
|---|---|
| No `html_safe_escape` filter | Markup sanitization that Power Pages performs does **not** happen here; the value passes through unchanged. Do not rely on it to remove dangerous HTML. |
| No default HTML encoding of `user`/`request` | Power Pages rewrites `{{ user.x }}` to `{{ user.x \| encode }}`. Here you must add `\| escape` yourself where output is untrusted. |
| Fractional seconds | `date_add_seconds` keeps the fraction here; Power Pages truncates it. |
| Time zone | Date strings with an offset, and `'now'`, are parsed into **server local time** here; Power Pages runs in UTC. Set the server to UTC, or avoid time-zone-bearing literals, when parity matters. |

Full detail and the test cases are in
[`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md).

## Debugging templates

1. **Look at the page.** A Liquid error appears as text in the output.
2. **Raise the trace.** Set the `ADXTrace` source to `Verbose` in `Web.config` — parse and render errors
   are logged with the source identifier (`<logicalname>:<id>` for web templates).
3. **Check `Liquid/Enabled`.** If it is `false`, templates are not rendered at all.
4. **Check member casing.** Names are lowercased by the naming convention; `{{ page.Title }}` is not the
   same as `{{ page.title }}`.
5. **Isolate.** Paste the template into a scratch web template on a test page and remove half of it until
   the error moves.

## Related pages

- [Liquid Objects Reference](Liquid-Objects-Reference.md)
- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md)
- [Content Management](Content-Management.md)
- [Entity Lists](Entity-Lists.md), [Entity Forms](Entity-Forms.md)
- [`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md)
