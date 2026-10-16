# Multi-Language

A website can serve multiple languages. Language affects the resolved culture, content selection, URL
shape, and which localized UI strings and script bundles are used.

## The model

```
adx_website
  ├── adx_defaultlanguage ──────────▶ adx_websitelanguage
  └── adx_websitelanguage (one row per language enabled for the site)
            ├── adx_name            display name, e.g. "English (United States)"
            ├── adx_lcid            LCID, e.g. 1033
            ├── adx_languagecode    code, e.g. "en-US"
            ├── adx_portallanguageid ──▶ adx_portallanguage
            └── adx_isdefault       whether this is the default language
```

Related records carry a language reference:

- `adx_webpage.adx_webpagelanguageid`
- `adx_contentsnippet.adx_contentsnippetlanguageid`
- `adx_webfile` language via its webpage

`adx_portallanguage` (`adx_portallanguageid`, `adx_lcid`, `adx_languagecode`, `adx_displayname`,
`adx_systemlanguage`) is the catalog of languages available in the environment.

## Site settings

| Setting | Effect |
|---|---|
| `MultiLanguage/Fallback` | The language used when a translation is missing. Enables fallback rather than a blank or default-language result. |
| `MultiLanguage/DisplayLanguageCodeInURL` | Whether the language code appears in the URL (for example `/en-US/products`). |
| `KnowledgeManagement/Article/Language` | Language handling for knowledge articles. |

## How language is resolved for a request

1. `ContextLanguageInfo.Create` is registered per OWIN context in `Startup.ConfigurePortal`.
2. The current language is derived from the URL (when the language code is in the URL), the visitor's
   choice, or the website's default.
3. `Startup.ConfigureCulture` then configures culture-aware bundles; language-specific script bundles are
   registered through `BundleConfig.RegisterLanguageSpecificBundles`.
4. `Global.asax.cs::Application_PreRequestHandlerExecute` re-applies the culture to the current thread with
   `ContextLanguageInfo.SetCultureInfo(languageContext.ContextLanguage.Lcid)`.

That last step exists because .NET 4.6+ does not preserve culture set inside OWIN middleware across the
request. If you add your own middleware that changes culture, remember this.

## Culture affects output caching

The output cache varies by language. The `GetVaryByCustomString` implementation in `Global.asax.cs`
supports `language` and the combined forms:

- `roles;language`, `user;language`, `website;language`,
- `roles;website;language`, `user;website;language`,
- and `culture` (the current culture's LCID).

If you add a cache profile for a localized page and forget to vary by language, visitors will see the
wrong language served from cache.

## Localizing content

| Content | How it is localized |
|---|---|
| **Web pages** | Separate `adx_webpage` records per language, linked by the language column and by translation relationships. |
| **Content snippets** | One `adx_contentsnippet` per language, sharing `adx_name`. |
| **Web files** | Per-language web files, as with pages. |
| **Knowledge articles** | `adx_knowledgearticle` with per-language variants. |
| **UI strings (framework)** | Resource files, not site content. |

Because snippets are looked up by name and language, a snippet with no translation for the current language
falls back (or disappears) according to `MultiLanguage/Fallback`.

## Localized UI strings in templates

The framework ships localized strings for ~40 languages in
`Framework/Adxstudio.Xrm.Resources/Adxstudio.Xrm.Resources/ResourceFiles/strings.*.resx`
(for example `strings.cs-CZ.resx`, `strings.de-DE.resx`, `strings.pt-BR.resx`), exposed through
`ResourceManagerExpressionBuilder` and the `resx` Liquid object:

```liquid
{{ resx['Submit_Button_Label'] }}
```

In ASP.NET markup, use the expression builder:

```aspx
<%$ ResourceManager:Submit_Button_Label %>
```

Note the difference: **`resx` strings are framework UI strings** (buttons, validation messages), while
**content snippets are your content**. Localize both, but in different places.

## Language-specific assets

`BundleConfig.GetTimeAgoLanguageCode` maps the current culture to a `timeago` locale file, with special
cases for Norwegian (Bokmål), Portuguese (Brazil), Serbian (Cyrillic and Latin) and Chinese (Traditional,
Simplified, Hong Kong). `RegisterLanguageSpecificBundles` then includes the matching locale script. New
languages may need a mapping added there.

## Adding a language

1. Ensure the language exists as an `adx_portallanguage`.
2. Add an `adx_websitelanguage` row for the website with its `adx_lcid` and `adx_languagecode`.
3. Create localized web pages, snippets and files for that language.
4. Decide on URL shaping with `MultiLanguage/DisplayLanguageCodeInURL`.
5. Set `MultiLanguage/Fallback` if you do not intend to translate everything.
6. Verify: switch language on the site and confirm both **content** and **UI strings** change, and that
   cached pages do not leak the previous language.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Content shows in the wrong language after a language switch | Output cache not varying by language, or a missing/incorrect `adx_websitelanguage` row. |
| Dates and numbers are not localized | Culture not applied — check `ContextLanguageInfo` and the thread-culture step. |
| Some strings stay English | Missing `.resx` entries for that language, or a snippet with no translation. |
| Language does not appear in the URL | `MultiLanguage/DisplayLanguageCodeInURL` is off. |
| Wrong language after sign-in | The user's `adx_preferredlanguageid` versus the resolved context language. |
| Timeago labels are wrong | The culture has no mapping in `GetTimeAgoLanguageCode`. |

## Related pages

- [Content Management](Content-Management.md)
- [Site Settings Reference](Site-Settings-Reference.md)
- [Configuration Reference](Configuration-Reference.md) — output cache profiles
- [Framework Namespaces](Framework-Namespaces.md)
