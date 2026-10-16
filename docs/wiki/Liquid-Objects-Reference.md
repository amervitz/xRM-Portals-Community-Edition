# Liquid Objects Reference

Every object available in a Liquid template, and the properties it exposes. Objects are **drops**: a drop
behaves like a dictionary whose keys are the lowercased .NET property names, and it can also load entity
attributes on demand.

> **Attribute access.** Drops derived from `PortalViewEntityDrop` (page, user, entity, and any record drop)
> expose a Dataverse record's attributes as **lowercased column names**. So `{{ page.adx_copy }}` also
> resolves through `{{ page.copy }}` style accessors, and `{{ entity.attributelogicalname }}` resolves any
> column. When a property is not explicitly defined, the underlying attribute is looked up.

For tags and filters, see [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md).

---

## Core globals

These are always defined for a request.

| Name | Type | Description |
|---|---|---|
| `page` | `PageDrop` | The current web page. **Only defined when the request resolves to an entity and a site-map node.** |
| `user` | `UserDrop` | The signed-in contact, or null for anonymous visitors. |
| `request` | `RequestDrop` | The HTTP request. |
| `params` | `Hash` | Shorthand for `request.params`. |
| `website` | `WebsiteDrop` | The resolved `adx_website`. |
| `settings` | `SettingsDrop` | Site settings, addressed by name. |
| `sitemap` | `SiteMapDrop` | Navigation over the content map. |
| `sitemarkers` | `SiteMarkersDrop` | Named page pointers. |
| `snippets` | `SnippetsDrop` | Content snippets by name and language. |
| `weblinks` | `WebLinkSetsDrop` | Web link sets by name. |
| `entities` | `EntitiesDrop` | Query and access Dataverse records. |
| `context` | `PortalViewContextDrop` | Lower-level view context. |
| `now` | `DateTime` (UTC) | Current UTC date and time. |
| `resx` | `ResourceManagerDrop` | Localized framework UI strings. |
| `uniqueId` | `UniqueDrop` | Unique id generation. |
| `ads` | `AdsDrop` | Ad placements. |
| `polls` | `PollsDrop` | Poll placements. |
| `forums` | `ForumsDrop` | Forums. |
| `blogs` | `BlogsDrop` | Blogs. |
| `events` | `EventsDrop` | Events. |
| `knowledge` | `KnowledgeDrop` | Knowledge articles. |
| `sharepoint` | `SharePointDrop` | SharePoint documents. |

### Custom globals

You can register additional global variables in code with
`LiquidExtensions.RegisterGlobalVariable(name, factory)`. See [Extensibility](Extensibility.md).

---

## `page` — `PageDrop`

Derives from `PortalViewEntityDrop`, so it exposes every `adx_webpage` column (lowercased) as well as:

| Property | Type | Description |
|---|---|---|
| `title` | string | The page title. |
| `description` | string | The page description. |
| `url` | string | The page's URL. |
| `breadcrumbs` | `IEnumerable<SiteMapNodeDrop>` | Ancestor trail. |
| `children` | `IEnumerable<SiteMapNodeDrop>` | Child pages. |
| `parent` | `SiteMapNodeDrop` | The parent page node. |
| `languages` | `IEnumerable<LanguageDrop>` | Languages for the page. |
| `available_languages` | `IEnumerable<LanguageDrop>` | Languages with a published translation. |

Common `adx_webpage` attributes reachable on `page`: `adx_copy`, `adx_summary`, `adx_meta`,
`adx_customcss`, `adx_customjavascript`, `adx_partialurl`, `adx_hiddenfromsitemap`, `adx_displaydate`,
`adx_enablerating`, `adx_commentpolicy`, `adx_feedbackpolicy`, `adx_redirect`, `adx_redirecturl`.

---

## `user` — `UserDrop`

Derives from `PortalViewEntityDrop`, so contact columns are available (`fullname`, `firstname`,
`lastname`, `emailaddress1`, `parentcustomerid`, `adx_username`, `adx_preferredlanguageid`, …) plus:

| Property | Type | Description |
|---|---|---|
| `roles` | `IEnumerable<string>` | The user's web roles (display names). |
| `role_keys` | `IEnumerable<string>` | The user's web roles (keys). |
| `is_user_in_role(roleName)` | bool | Role test. Also available as the `has_role` filter. |
| `basic_badges_url` | string | URL for the basic badges endpoint. |
| `profile_badges_url` | string | URL for the profile badges endpoint. |
| `forum_thread_subscriptions` | `IEnumerable<ForumThreadDrop>` | Threads the user follows. |

`user` is **null** when nobody is signed in. Always guard it:

```liquid
{% if user %}
  <p>{{ user.fullname }}</p>
{% endif %}
```

---

## `request` — `RequestDrop`

| Property | Type | Description |
|---|---|---|
| `url` | string | The request URL. |
| `raw_url` | string | The raw, unprocessed URL. |
| `raw_url_encode` | string | The URL-encoded raw URL. |
| `path` | string | The URL path. |
| `path_and_query` | string | Path plus query string. |
| `query` | string | Query string. |
| `params` | `Hash` | Query-string parameters. |
| `anti_forgery_token` | string | The current anti-forgery token. |

Note `user` and `request` values are **not** HTML-encoded by default here (Power Pages encodes them
automatically). Apply `| escape` where the values are untrusted.

---

## `website` — `WebsiteDrop`

| Property | Type | Description |
|---|---|---|
| `selected_language` | `LanguageDrop` | The resolved language for this request. |
| `languages` | `IEnumerable<LanguageDrop>` | Languages enabled for the site. |
| `forums` | `IEnumerable<ForumDrop>` | The site's forums. |
| `events` | `IEnumerable<EventDrop>` | The site's events. |
| `blogs` | `IEnumerable<BlogDrop>` | The site's blogs. |
| `sign_in_url` / `sign_out_url` | string | Sign-in/sign-out URLs. |
| `sign_in_url_substitution` / `sign_out_url_substitution` | string | URL templates with a return-URL substitution point. |
| `sign_in_link` / `sign_out_link` | string | Rendered anchor markup. |
| `sign_in_link_substitution` / `sign_out_link_substitution` | string | Link templates with a substitution point. |

Plus `adx_website` columns such as `name`, `adx_primarydomainname`.

---

## `settings` — `SettingsDrop`

Address settings by name, as a full path:

```liquid
{% if settings['Authentication/Registration/OpenRegistrationEnabled'] == 'true' %}
```

Values are strings. Convert explicitly with the type filters (`boolean`, `integer`, `decimal`) when you
need a typed value.

---

## `sitemap` — `SiteMapDrop`

| Property | Type | Description |
|---|---|---|
| `current` | `SiteMapNodeDrop` | The current node. |
| `root` | `SiteMapNodeDrop` | The root node. |

Any other name resolves to a site-marker-named node. See
[Site Settings Reference](Site-Settings-Reference.md) for settings that affect navigation, and
[Content Management](Content-Management.md#navigation) for the navigation mechanisms.

---

## `sitemap.current` and `SiteMapNodeDrop`

| Property | Type | Description |
|---|---|---|
| `title` | string | Node title. |
| `description` | string | Node description. |
| `url` | string | Node URL. |
| `breadcrumbs` | `IEnumerable<SiteMapNodeDrop>` | Ancestors. |
| `children` | `IEnumerable<SiteMapNodeDrop>` | Children. |
| `parent` | `SiteMapNodeDrop` | Parent. |
| `entity` | `EntityDrop` | The underlying record, when there is one. |
| `get_editable(context, key, options)` | string | Renders an editable value for authorized users. |

---

## `sitemarkers` — `SiteMarkersDrop`

```liquid
<a href="{{ sitemarkers['Support'].url }}">{{ sitemarkers['Support'].title }}</a>
```

Resolves a site marker name to a `SiteMapNodeDrop`.

---

## `snippets` — `SnippetsDrop`

```liquid
{{ snippets['Footer/Legal'] }}
```

Resolves a content snippet name (and the current language) to its value.
`get_editable(context, key, options)` renders an editable snippet for authorized users.

---

## `weblinks` — `WebLinkSetsDrop`

Resolves a web link set by name to a `WebLinkSetDrop`:

| Property | Type | Description |
|---|---|---|
| `name`, `title`, `copy` | string | Set metadata. |
| `weblinks` | `IEnumerable<WebLinkDrop>` | The links. |
| `get_editable(context, options)` | string | Editable rendering. |

### `WebLinkDrop`

| Property | Type | Description |
|---|---|---|
| `name`, `description`, `tooltip` | string | Text. |
| `url` | string | Link URL. |
| `is_external` | bool | Whether the target is external. |
| `open_in_new_window` | bool | Target behaviour. |
| `nofollow` | bool | Adds `rel="nofollow"`. |
| `display_image_only` | bool | Render only the image. |
| `display_page_child_links` | bool | Render child links. |
| `image` | `WebLinkImageDrop` | The image. |
| `weblinks` | `IEnumerable<WebLinkDrop>` | Child links. |

---

## `entities` — `EntitiesDrop`

`entities` is the query entry point. A named lookup resolves an entity set:

```liquid
{% assign cases = entities.incident %}
{% for c in cases %}
  {{ c.title }}
{% endfor %}
```

An `EntitySetDrop` exposes:

| Property | Type | Description |
|---|---|---|
| `logical_name` | string | The table's logical name. |
| *any indexer* | `EntityDrop` | Named access to a single entity. |

### `EntityDrop`

| Property | Type | Description |
|---|---|---|
| `id` | string | The record id. |
| `logical_name` | string | The table's logical name. |
| `url` | string | The record's portal URL. |
| `name` | string | The record's primary name. |
| `notes` | `IEnumerable<EntityNoteDrop>` | The record's notes/annotations. |
| `permissions` | `EntityPermissionsDrop` | Rights for the current user. |
| `formatted_values` | `Hash` | Dataverse formatted values. |
| `cms` | `CmsDrop` | CMS-related helpers. |
| `can_read`, `can_change` | bool | Convenience permission checks. |
| `referenced`, `referencing` | `IEnumerable<EntityDrop>` | Relationship traversal. |
| `is_reflexive` | bool | Whether the relationship points at the same table. |
| `to_entity_reference()` | `EntityReference` | The raw reference. |
| `get_editable(context, key, options)` | string | Editable rendering. |

Any Dataverse column is also reachable as a lowercased attribute name.

### `EntityPermissionsDrop`

| Property | Type |
|---|---|
| `can_create`, `can_read`, `can_write`, `can_delete`, `can_append`, `can_append_to` | bool |
| `rules_exist` | bool |

### `EntityNoteDrop`

A recorded note; exposes its columns (`subject`, `notetext`, `filename`, `mimetype`, `documentbody`) and a
`url`.

---

## Entity lists

### `EntityListDrop`

| Property | Type | Description |
|---|---|---|
| `entity_logical_name` | string | Listed table. |
| `page_size` | int? | Rows per page. |
| `search_enabled` | bool | Whether search is available. |
| `create_enabled`, `create_label`, `create_url` | bool / string | Create action. |
| `detail_enabled`, `detail_label`, `detail_url`, `detail_id_parameter` | bool / string | Details action. |
| `empty_list_text` | string | Empty-state text. |
| `enable_entity_permissions` | bool | Whether permissions are enforced. |
| `filter_enabled`, `filter_definition`, `filter_apply_label`, `is_filter_vertical` | filter configuration |
| `filter_account_attribute_name`, `filter_portal_user_attribute_name`, `filter_website_attribute_name` | string | Scoping attributes. |
| `primary_key_name` | string | Primary key column. |
| `get_data_url`, `modal_form_template_url`, `layouts` | string | Grid plumbing. |
| `grid_metadata` | `GridMetadataDrop` | Grid metadata and dialogs. |
| `language_code` | int | Language. |

### `EntityListViewDrop` and `EntityListViewColumnDrop`

| Property | Type | Description |
|---|---|---|
| `id`, `name`, `display_name` | string | View identity. |
| `entity_logical_name`, `primary_key_logical_name` | string | Columns. |
| `sort_expression` | string | Sorting. |
| `language_code` | int | Language. |
| `columns` | `IEnumerable<EntityListViewColumnDrop>` | Columns. |

### `GridMetadataDrop`

Grid behaviour and dialog configuration: `loading_message`, `error_message`, `access_denied_message`,
`empty_message`, `grid_css_class`, `css_class`, `grid_column_width_style`, `column_overrides`, and the
dialog drops `view_actions`, `item_actions`, `details_form_dialog`, `edit_form_dialog`,
`create_form_dialog`, `delete_dialog`, `error_dialog`, `lookup_dialog`, `create_related_record_dialog`.

---

## Community objects

### `blogs` — `BlogsDrop`

| Property | Type | Description |
|---|---|---|
| `posts` | `BlogPostsDrop` | All posts. |
| `feed_url` | string | The blog feed URL. |
| `url` | string | The blog URL. |

`BlogsDrop` and `BlogDrop` also resolve by name. `BlogDrop`: `name`, `title`, `summary`, `url`,
`feed_url`, `posts`.

`BlogPostDrop`: `title`, `content`, `url`, `publish_date`, `last_updated_time`, `comment_count`,
`author` (`AuthorDrop`).

`BlogPostsDrop`: `all` (`IEnumerable<BlogPostDrop>`), `start_row_index`, `page_size`, `order_by_key`,
`sort_direction`, and `set_params(startRowIndex, pageSize, orderBy, sortDirection)`.

### `forums` — `ForumsDrop`

| Property | Type | Description |
|---|---|---|
| `threads` | `ForumThreadsDrop` | Recent threads. |
| `all` | `IEnumerable<ForumDrop>` | All forums. |
| `thread_count` | int | Thread count. |
| `post_count` | int | Post count. |

`ForumDrop`: `name`, `threads`, `thread_count`, `post_count`.
`ForumThreadDrop`: `name`, `author`, `posts`, `first_post`, `latest_post`, `post_count`, `reply_count`,
`is_answered`, `is_sticky`, `locked`.
`ForumPostDrop`: `name`, `content`, `author`, `thread`, `is_answer`, `can_edit`, `can_mark_as_answer`,
`helpful_vote_count`.

### `events` — `EventsDrop`

| Property | Type | Description |
|---|---|---|
| `occurrences` | `EventOccurrencesDrop` | Occurrences, filterable. |

`EventDrop`: `name`, `schedules`, `occurrences`, `speakers` (`SpeakerDrop`), `sponsors` (`SponsorDrop`).
`EventOccurrencesDrop`: `all`, plus the range operators `min`, `from`, `max`, `until`, `to`.
`EventScheduleDrop` and `EventOccurrenceDrop` expose schedule timing.

### `knowledge` — `KnowledgeDrop`

| Property | Type | Description |
|---|---|---|
| `articles` | `KnowledgeArticlesDrop` | Article collections. |

`KnowledgeArticlesDrop`: `popular`, `recent`, `top`.
`KnowledgeArticleDrop`: `title`, `content`, `name`, `keywords`, `article_public_number`, `rating`,
`view_count`, `comment_count`, `current_user_can_comment`, `is_rating_enabled`.

---

## Ads and polls

### `ads` — `AdsDrop`

| Property | Type | Description |
|---|---|---|
| `placements` | `AdPlacementsDrop` | Ad placements by name. |

`AdPlacementDrop`: `name`, `ads`, `placement_url`, `random_url`.
`AdDrop`: `name`, `title`, `copy`, `ad_url`, `redirect_url`, `open_in_new_window`, `image`
(`AdImageDrop`).

### `polls` — `PollsDrop`

| Property | Type | Description |
|---|---|---|
| `placements` | `PollPlacementsDrop` | Poll placements by name. |

`PollDrop`: `name`, `question`, `options` (`PollOptionDrop`), `votes`, `has_user_voted`,
`user_selected_option`, `poll_url`, `submit_url`, `submit_button_label`.
`PollOptionDrop`: `answer`, `votes`, `percentage`.

---

## SharePoint

`sharepoint` — `SharePointDrop`:

| Property | Type | Description |
|---|---|---|
| `documents` | `SharePointDocumentsDrop` | Document collections. |

`SharePointDocumentDrop`, `SharePointDocumentListDrop` and `SharePointDocumentsDrop` expose document
metadata and URLs.

---

## Localization

`resx` — `ResourceManagerDrop`: resolves a framework UI string by key.

```liquid
{{ resx['Submit'] }}
```

`LanguageDrop`: `name`, `code`, `url`, `url_substitution`.

---

## Utility

`uniqueId` — `UniqueDrop`:

| Property | Type | Description |
|---|---|---|
| `new_guid` | object | A new GUID, for generating unique DOM ids. |

```html
<div id="panel-{{ uniqueId.new_guid }}">…</div>
```

---

## Related pages

- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md)
- [Web Templates and Liquid](Web-Templates-and-Liquid.md) — tutorial and recipes
- [Extensibility](Extensibility.md) — registering your own globals and drops
