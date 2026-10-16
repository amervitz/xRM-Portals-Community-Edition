# Community Features

The portal ships a full set of community features: discussion forums, blogs, idea and issue forums,
comments, ratings, tags, badges, alerts and subscriptions. They share a common shape: a **container**
(forum, blog, idea forum, issue forum) holds **content**, and visitors interact through **posts**,
**votes**, **comments** and **subscriptions**.

## Forums

A **community forum** is a threaded discussion board.

| Record | Role |
|---|---|
| `adx_communityforum` | A forum. |
| `adx_communityforumthread` | A thread in a forum. |
| `adx_communityforumpost` | A post in a thread. |
| `adx_communityforumaccesspermission` | Per-forum access rules for web roles. |
| `adx_forumannouncement` | Pinned announcements. |
| `adx_communityforumalert` | Alerts. |
| `adx_forumthreadtype` | Thread types (for example question vs. discussion). |
| `adx_communityforumthreadtag`, `adx_forumthreadtag` | Thread tagging. |
| `adx_forumnotification`, `adx_alertsubscription` | Subscriptions and notifications. |

Useful columns on a forum: `adx_communityforumid`, `adx_name`, `adx_forumpagetemplateid` (page template
used to render the forum), and language/website scoping.

### Settings

| Setting | Effect |
|---|---|
| `Forums/PostsPerPage` | Posts per page in a thread (default 20 in the Liquid environment). |
| `forums/forum/templatepath` | Template used to render a forum. |
| `forums/forumthread/templatepath` | Template used to render a thread. |

### Rendering

- Liquid: the `forums` global object (`ForumsDrop`, `ForumDrop`, `ForumThreadDrop`, `ForumPostDrop`,
  `ForumThreadsDrop`, `ForumPostsDrop`), the `forum` functions, and the shipped `forums` template.
- Site map: `ForumSiteMapProvider` contributes forums to the sitemap.
- ASP.NET: `Controls/ForumsPanel.ascx`, the Forums area, and the forum site map handler.
- JavaScript: `js/entity-notes.js` and forum-specific scripts in the Forums area.

### Access control

Forum visibility and participation are governed by `adx_communityforumaccesspermission` records for web
roles, layered on top of the site's normal role and table-permission model. Review these when a forum is
visible but "empty", or visible to the wrong audience.

## Blogs

A blog is a chronological collection of posts, each with comments.

| Record | Role |
|---|---|
| `adx_blog` | A blog. |
| `adx_blogpost` | A post: `adx_title`, `adx_copy`, `adx_summary`, publish date, author. |
| `adx_blogpostcomment` | A comment. |
| `adx_author` | Blog author profile. |

Columns worth knowing on `adx_blog`: `adx_bloghomepagetemplateid`, `adx_blogpostpagetemplateid`,
`adx_blogarchivepagetemplateid` — the page templates used for the blog home page, a single post, and the
archive.

### Rendering

- Liquid: the `blogs` global object (`BlogsDrop`, `BlogDrop`, `BlogPostsDrop`, `BlogPostDrop`,
  `BlogFunctions`), plus the shipped `blogs` template and `events_upcoming`-style partials.
- Site map: `BlogSiteMapProvider`.
- ASP.NET: `Controls/BlogsPanel.ascx`, `Controls/NewsPanel.ascx`, the Blogs area.
- JavaScript: `js/timeago/*` for relative timestamps.

Common recipe — recent posts:

```liquid
{% assign recent = blogs.posts | order_by: 'publish_date', 'descending' | take: 5 %}
{% for post in recent %}
  <article>
    <h2><a href="{{ post.url }}">{{ post.title }}</a></h2>
    <p>{{ post.summary }}</p>
  </article>
{% endfor %}
```

## Ideas

An **idea forum** collects suggestions with voting.

| Record | Role |
|---|---|
| `adx_ideaforum` | The forum: `adx_ideasubmissionpolicy`, `adx_votesperidea`, `adx_votesperuser`, `adx_votingpolicy`, `adx_votingtype`, `adx_closevotingdate` |
| `adx_idea` | An idea: `adx_title`, `adx_description`, `adx_votes`, `adx_votessum`, `adx_totalvotes`, `adx_returnrate` |
| `adx_ideavote` | A vote |
| `adx_ideacomment` | A comment |

Voting policy controls who may vote and how many votes they get. Rendering uses the Ideas area
(`IdeasController`, `IdeaController`), the `Areas/Ideas` views, rating/vote scripts, and the idea Liquid
drops.

## Issues

An **issue forum** is an ideas-like feature for reporting and tracking problems, with votes.

| Record | Role |
|---|---|
| `adx_issueforum` | The forum: `adx_issuesubmissionpolicy`, `adx_votesperuser`, `adx_votingtype` |
| `adx_issue` | An issue: `adx_title`, `adx_priority`, `adx_statuscomment`, `adx_resolution`, `adx_resolutiondate`, `adx_votessum` |
| `adx_issuevote`, `adx_issuecomment`, `adx_issuealert` | Votes, comments, alerts |

Rendering uses the Issues area (`IssuesController`, `IssueController`).

## Comments

Comments attach to many content types. The comment model is:

| Piece | Role |
|---|---|
| `ICommentDataAdapter`, `ICommentDataAdapterFactory`, `CommentDataAdapterFactory` | Comment storage per content type. |
| `ICommentPolicyReader`, `PageCommentPolicy` | Whether commenting is allowed. |
| `cms/defaultPageCommentPolicy` | The default page comment policy. |
| Per-page `adx_commentpolicy` | Per-page override. |
| `adx_portalcomment` | The comment record. |
| `Controls/CommentCreator.ascx`, `Controls/Comments.ascx` | ASP.NET comment UI. |
| `css/comments.css` | Comment styling. |

A page's comment policy decides whether comments are open, moderated, or disabled. Moderation state lives
on the comment records, so review the workflow before enabling open commenting on a public site.

## Ratings

Ratings let visitors rate content.

| Setting/record | Role |
|---|---|
| `adx_enablerating` / `adx_enableratings` | Enable ratings on a record. |
| `adx_rating`, `adx_ratingaverage`, `adx_ratingcount`, `adx_ratingsum`, `adx_ratingmaximumvalue`, `adx_ratingrationalvalue`, `adx_ratingaveragerationalvalue` | Rating state. |
| `Feedback` folder (`IRateable`, `IRating`, `IRatingInfo`, `RatingDataAdapter`) | Implementation. |
| `{% rating %}` tag | Renders a rating control. |
| `js/rating.js`, `js/jQuery.rateit.min.js`, `css/rateit.css` | Client-side. |
| `Areas/Cms/Controllers/RatingController.cs` | Server-side endpoint. |

## Tags

Tagging is used across forums, content and knowledge.

| Record | Role |
|---|---|
| `adx_tag` | A tag. |
| `adx_pagetag`, `adx_pagetagrelationship` | Page tagging. |
| `adx_forumthreadtag`, `adx_communityforumthreadtag` | Thread tagging. |

`Framework/Adxstudio.Xrm/Tagging` holds the implementation, and the `Tags` folder in `Web/Mvc/Liquid`
exposes them to templates. Tag autocomplete is served by `TagAutoCompleteHandler` and
`TimeTagAutoCompleteHandler`.

## Badges

Badges recognize contributors.

| Record | Role |
|---|---|
| `adx_badge` | A badge. |
| `adx_badgetype` | Badge category/type. |
| `adx_badge` award fields | Which activity earns it. |

Rendering: `badges` Liquid object and the shipped `badges` template, plus `js/badges.js`,
`get_basic_badges_url`, `get_profile_badges_url`.

## Polls and ads

Covered separately: [Ads, Polls and Marketing](Ads-Polls-and-Marketing.md).

## Public profiles

Community features depend on public profiles, which show an author's activity. See
[Users, Profiles and Invitations](Users-Profiles-and-Invitations.md#public-profiles).

## Sanitizing user-generated content

Forum posts, comments, ideas and issues are **user-generated HTML**. The portal sanitizes them with the
`SafeHtml` assembly (`SafeHtml.SafeHtmSanitizer.GetSafeHtml`) rather than trusting the input. Keep that in
place when customizing these features.

Note that `SafeHtml` is **not** wired to Power Pages' `html_safe_escape` filter — that filter does not
exist here. If you need to sanitize a value in a Liquid template, do it server-side in the control rather
than relying on a template filter. See
[`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md).

## Enabling and disabling community features

These features are not switched on by a single flag. A feature is "on" when:

1. Its records exist (a forum, a blog, an idea forum),
2. Its page templates and pages exist and are published, and
3. Web roles and permissions allow visitors to see and participate.

To remove a feature from a site, remove or unpublish its pages and revoke its permissions — the
application does not need to change.

## Related pages

- [Content Management](Content-Management.md)
- [Cases and Knowledge](Cases-and-Knowledge.md)
- [Users, Profiles and Invitations](Users-Profiles-and-Invitations.md)
- [Security and Authentication](Security-and-Authentication.md)
- [Portal Data Model](Portal-Data-Model.md)
