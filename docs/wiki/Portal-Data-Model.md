# Portal Data Model

The portal's content, configuration and state live in Dataverse tables. This page lists the tables the
portal code reads and writes, grouped by feature area. Tables are the **legacy `adx_*` model**; the Power
Pages **enhanced data model** (`mspp_*` virtual tables) is not used by the portal for its own content,
though it can coexist in the same environment.

> **Naming.** Table names use the `adx_` prefix. Columns shown in the individual pages use the same
> prefix. Where a logical name is written in the code, it is always lowercase.

---

## Core content

| Table | Purpose |
|---|---|
| `adx_website` | A website. The root configuration object. |
| `adx_websitebinding` | Maps host names to a website. |
| `adx_websitelanguage` | A language enabled for a website. |
| `adx_portallanguage` | The catalog of portal languages. |
| `adx_webpage` | A page in the content tree. |
| `adx_pagetemplate` | How a page renders (Rewrite or Web Template). |
| `adx_webtemplate` | A Liquid template (`adx_source`). |
| `adx_contentsnippet` | A named, localized content fragment. |
| `adx_webfile` | A downloadable file. |
| `adx_publishingstate` | A content workflow state. |
| `adx_publishingstatetransitionrule` | Who may move content between states. |
| `adx_sitemarker` | A named pointer to a page. |
| `adx_shortcut` | Additional sitemap entries. |
| `adx_weblinkset`, `adx_weblink` | Curated navigation menus. |
| `adx_pagetag` | Page tags. |
| `adx_urlhistory` | Previous URLs, for redirects after a move. |
| `adx_redirect` | Redirect definitions. |
| `adx_image` | Portal images. |
| `adx_webnotificationurl` | Notification endpoints for change propagation. |
| `adx_requestlog` | Request log records. |
| `adx_problemurlrequest` | Requests for URLs that did not resolve. |

## Configuration

| Table | Purpose |
|---|---|
| `adx_setting` | Settings used by the provisioning/setup flow (for example `PackageImportComplete`). |
| `adx_sitesetting` | The site settings resolved at runtime. See [Site Settings Reference](Site-Settings-Reference.md). |
| `adx_configuration` | Portal configuration records. |
| `adx_autonumberingrequest` | Auto-numbering requests. |

## Security

| Table | Purpose |
|---|---|
| `adx_webrole` | A portal role. |
| `adx_entitypermission` | Table permissions for a web role. |
| `adx_webpageaccesscontrolrule` | Per-page access rules. |
| `adx_contentaccesslevel` | Content access levels. |
| `adx_websiteaccess` | Whole-website access restrictions. |
| `adx_caseaccess`, `adx_accountaccess`, `adx_contactaccess` | Case/account/contact access records. |
| `adx_channelpermissions` | Channel permissions. |
| `adx_opportunitypermissions` | Partner opportunity permissions. |
| `adx_identity` | Portal user credentials/identity. |
| `adx_externalidentity` | Linked external provider identities. |
| `adx_invitation` | Invitations to register. |
| `adx_inviteredemption` | Invitation redemptions. |
| `adx_membership`, `adx_membershiptype` | Memberships. |
| `adx_anonymoususersrole`, `adx_authenticatedusersrole` | Built-in role assignments. |

## Entity lists and forms

| Table | Purpose |
|---|---|
| `adx_entitylist` | An entity list definition (views referenced via `adx_view` / `adx_views`). |
| `adx_entityform` | An entity form definition. |
| `adx_entityformmetadata` | Field/section/tab customizations for an entity form. |
| `adx_webform` | A multi-step web form. |
| `adx_webformstep` | A step in a web form. |
| `adx_webformmetadata` | Metadata for a web form. |
| `adx_webformsession` | A visitor's web form progress. |

Views come from Dataverse **`savedquery`** (system views) and **`userquery`** (personal views), selected by
the entity list.

## Community

| Table | Purpose |
|---|---|
| `adx_communityforum` | A forum. |
| `adx_communityforumthread` | A thread. |
| `adx_communityforumpost` | A post. |
| `adx_communityforumannouncement` | A forum announcement. |
| `adx_communityforumalert` | Alerts. |
| `adx_communityforumthreadtag` | Thread tags. |
| `adx_forumthreadtype` | Thread types. |
| `adx_blog` | A blog. |
| `adx_blogpost` | A post. |
| `adx_ideaforum`, `adx_idea`, `adx_ideavote`, `adx_ideacomment` | Ideas and voting. |
| `adx_issueforum`, `adx_issue`, `adx_issuealert`, `adx_issuecomment` | Issues. |
| `adx_tag` | Tags. |
| `adx_badge`, `adx_badgetype` | Badges. |
| `adx_alertsubscription` | Alert subscriptions. |
| `adx_portalcomment` | Comments. |
| `adx_rating` | Ratings. |

## Knowledge and support

| Table | Purpose |
|---|---|
| `adx_knowledgearticle` | Portal knowledge article. |
| `adx_knowledgearticlecontentaccesslevel` | Article-to-content-access-level link. |
| `adx_supportplan`, `adx_supportrequest` | Support plans and requests. |
| `adx_caseaccess`, `adx_casedeflection` | Case access and deflection. |
| `adx_opportunitynote` | Opportunity notes. |
| `adx_psa` | PSA-related records. |
| `adx_permit` | Permits. |
| `adx_survey`, `adx_surveychoiceanswer`, `adx_surveychoicequestionid`, `adx_surveytextanswer`, `adx_surveytextareaquestionid` | Surveys. |
| `knowledgearticle`, `incident`, `list`, `savedquery` | Dataverse-standard tables the portal uses. |

## Events and conferences

| Table | Purpose |
|---|---|
| `adx_event` | An event. |
| `adx_eventschedule` | A scheduled occurrence. |
| `adx_eventregistration` | A registration. |
| `adx_eventspeaker`, `adx_eventsponsor` | Speakers and sponsors. |
| `adx_eventtag` | Event tags. |
| `adx_conference` | A conference. |

See [Events and Conferences](Events-and-Conferences.md) for the full association set.

## Commerce

| Table | Purpose |
|---|---|
| `adx_shoppingcart` | A cart. |
| `adx_shoppingcartitem` | A cart line. |
| `adx_discount` | Discount definitions. |
| `opportunity`, `quote`, `salesorder`, `invoice` | The Dataverse sales pipeline the portal drives. |

## Packages

| Table | Purpose |
|---|---|
| `adx_packagerepository` | A package repository. |
| `adx_package`, `adx_packageversion`, `adx_packagecomponent`, `adx_packagedependency`, `adx_packagepublisher`, `adx_packagecategory`, `adx_packageimage` | Package distribution records. |

## Ads, polls and marketing

| Table | Purpose |
|---|---|
| `adx_ad`, `adx_adplacement` | Ads and their placements. |
| `adx_poll`, `adx_polloption`, `adx_pollplacement`, `adx_pollsubmission` | Polls, options, placements and responses. |
| `adx_brand` | Brands. |
| `adx_review` | Reviews. |
| `adx_jobposting` | Careers job postings. |
| `adx_311alert` | Service 311 alerts. |

## Identity and profile

| Table | Purpose |
|---|---|
| `contact` | Portal users. |
| `account` | Accounts (parent customers). |
| `adx_identity`, `adx_externalidentity` | Credentials and provider links. |
| `adx_profilealert`, `adx_profileisanonymous`, `adx_profilelastactivity`, `adx_profilemodifiedon` | Profile state. |
| `adx_username`, `adx_contactusername` | Sign-in name storage. |

## Columns referenced most

These attributes are referenced constantly in code and configuration, so they are worth memorizing:

| Attribute | Meaning |
|---|---|
| `adx_name` | Display name. Also the template lookup key for `adx_webtemplate`. |
| `adx_partialurl` | URL segment. |
| `adx_publishingstateid` | Content workflow state. |
| `adx_parentpageid`, `adx_isroot` | Content tree shape. |
| `adx_pagetemplateid` | Renderer. |
| `adx_source` | Liquid source on a web template. |
| `adx_value` | Value on a content snippet or setting. |
| `adx_websiteid` | Website scope. |
| `adx_webpagelanguageid`, `adx_contentsnippetlanguageid` | Language. |
| `adx_entityname` | Table logical name used by lists, forms and permissions. |
| `adx_entitypermissionsenabled` | Permission enforcement on lists and forms. |
| `adx_mode` | Form/step mode (`100000000` insert, `100000001` edit, `100000002` read-only). |
| `adx_type` | Type discriminator (page template type, metadata type, step type). |
| `adx_view`, `adx_views` | Entity list view ids (legacy) and view metadata JSON (current). |
| `adx_cloudblobaddress` | Blob address for cloud-stored web files. |
| `adx_isdefault` | Default flag (page templates, publishing states, languages). |
| `adx_displayorder` | Manual ordering. |
| `adx_hidefromlisting`, `adx_hiddenfromsitemap` | Listing and sitemap visibility. |

## Type discriminators

| Field | Values |
|---|---|
| `adx_pagetemplate.adx_type` | `756150000` = Rewrite, `756150001` = Web Template |
| `adx_entityform.adx_mode` / `adx_webformstep.adx_mode` | `100000000` = Insert, `100000001` = Edit, `100000002` = ReadOnly |
| `adx_webformstep.adx_type` | `100000000` = Condition, `100000001` = LoadForm, `100000002` = LoadTab, `100000003` = Redirect, `100000004` = LoadUserControl |
| `adx_webformstep.adx_entitysourcetype` | `100000001` = QueryString, `100000002` = CurrentPortalUser, `100000003` = ResultFromPreviousStep |
| Web form progress position | `756150000` = Top, `756150001` = Bottom, `756150002` = Left, `756150003` = Right |

---

## Working with the data model

### Querying it yourself

The fastest way to explore is a Dataverse view or an advanced find over the table. The portal's own code is
the reference for which columns it honours — search `Framework/Adxstudio.Xrm` for the column name.

### Extending it

- Add your own columns to portal tables and reference them from Liquid (they are exposed as lowercased
  attribute names on entity drops).
- Add your own tables and surface them with entity lists and entity forms.
- Do **not** change the meaning of existing columns; the framework reads them by name in many places.

### Do not hand-edit generated records

Some records are managed by the base solution. Editing them can be overwritten on solution upgrade. Prefer
adding overrides (for example a web template that shadows an embedded template) rather than modifying
shipped records.

---

## Related pages

- [Content Management](Content-Management.md)
- [Entity Lists](Entity-Lists.md), [Entity Forms](Entity-Forms.md), [Web Forms](Web-Forms.md)
- [Security and Authentication](Security-and-Authentication.md)
- [Site Settings Reference](Site-Settings-Reference.md)
- [Framework Namespaces](Framework-Namespaces.md)
