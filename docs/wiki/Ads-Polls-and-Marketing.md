# Ads, Polls and Marketing

Three lighter-weight features for engaging visitors: advertisements, polls, and marketing lists. Together
with feedback, they cover most "show a promotion and ask the visitor something" requirements.

## Ads

Ads are banners or placements shown in defined positions on a page.

| Record | Role |
|---|---|
| `adx_ad` | An ad: `adx_name`, `adx_title`, `adx_copy`, `adx_imageurl`, `adx_imagealttext`, `adx_imagewidth`, `adx_imageheight`, `adx_url`, `adx_openinnewwindow`, `adx_redirecturl`, `adx_backgroundcolor`, `adx_displayorder`, `adx_websiteid`, publishing state |
| `adx_adplacement` | A named position on a page (`adx_name`, `adx_websiteid`) |
| `adx_adplacementid` | Placement identity on the ad association |

Each placement groups ads; an ad association links an ad to a placement with a display order. Ads are also
publishable, so an ad can be scheduled in and out with the publishing-state workflow.

### Rendering

```liquid
{# a specific placement #}
{% assign banners = ads.placements['Home Top'] %}
{% for ad in banners.ads %}
  <a href="{{ ad.url }}"{% if ad.open_in_new_window %} target="_blank" rel="noopener"{% endif %}>
    <img src="{{ ad.image_url }}" alt="{{ ad.image_alt_text }}" />
  </a>
{% endfor %}
```

Also available:

- The shipped `ad`, `random_ad`, `ad_template` templates.
- `AdsDrop`, `AdDrop`, `AdImageDrop`, `AdPlacementsDrop`, `AdPlacementDrop` in Liquid.
- `Areas/Cms/Controllers/AdController.cs` and `js/ads.js` for the client-side behaviour.
- `get_ad_url`, `get_placement_url` and `get_random_url` helpers on the relevant drops.

## Polls

Polls ask a single question with a fixed set of answers and count responses.

| Record | Role |
|---|---|
| `adx_poll` | A poll: `adx_name`, `adx_question`, `adx_websiteid`, `adx_websiteid`, `adx_display` |
| `adx_polloption` | An answer option: `adx_answer`, `adx_displayorder` |
| `adx_pollplacement` | A named position for polls |
| `adx_pollsubmission` | A recorded response, with `adx_visitorid` and `adx_createdbycontact` |

Poll options and questions follow the record's language, so a poll can be localized.

### Rendering

```liquid
{% poll name: 'Feedback' %}
```

Also available:

- The shipped `poll`, `poll_template` and `random_poll` templates.
- `Pages/PollArchives.aspx` for archived poll results.
- `PollsDrop`, `PollDrop`, `PollOptionDrop`, `PollPlacementsDrop`, `PollPlacementDrop` in Liquid.
- `Areas/Cms/Controllers/PollController.cs` and `js/polls.js` for submission handling (`get_poll_url`,
  `get_random_url`).

Submissions are de-duplicated by visitor id, so a visitor cannot ballot-stuff a poll in a normal browser
session. Treat poll results as indicative, not authoritative.

## Ratings and feedback

Ratings (`adx_rating`) and the feedback helpers are covered in
[Community Features](Community-Features.md#ratings). Feedback metadata attributes
(`FeedbackMetadataAttributes`) let you choose which fields a feedback form asks for.

## Marketing lists

A marketing list is a Dataverse `list` (marketing list) that contacts can be added to. The portal exposes
them on the profile page so users can opt in and out of communications.

| Piece | Role |
|---|---|
| `list` / marketing list member records | The list and its members. |
| `adx_website_list` | The website-to-list association used by the profile panel. |
| `Profile/ShowMarketingListsPanel` | Whether the marketing-list panel appears on the profile. **Default `false`.** |
| `Marketing/EncryptionKey` | Key used to encrypt marketing data. |
| `Framework/Adxstudio.Xrm/Marketing` | The marketing data adapters. |
| `Areas/Marketing/Controllers/MarketingController.cs` | The marketing endpoint. |

### Why the panel is off by default

`Profile/ShowMarketingListsPanel` defaults to **`false`** because the `adx_website_list` relationship may
not exist in every environment; enabling the panel where the relationship is missing produces an error.

To turn it on:

1. Confirm the `adx_website_list` relationship exists in your environment.
2. Create the website-to-list associations.
3. Set `Profile/ShowMarketingListsPanel` to `true`.

### Consent and compliance

Marketing lists are a **consent** surface. Before enabling them:

- Make opt-in explicit and record when it happened.
- Provide a way to opt out that works even if the user never signs in.
- Treat `Marketing/EncryptionKey` as a secret — it belongs in a secret store, not in source control.

## Display mode–based ads

Ads also interact with the **display modes** feature: `DisplayModes/Facebook/HostName` and
`DisplayModes/Iframe/HostName` let a different presentation (and different placements) apply when the site
is embedded in Facebook or an iframe. This is a legacy integration path; it is inactive unless you set
those host names.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| No ads render | The placement name does not match, the ad is unpublished, or `adx_websiteid` scopes it to another website. |
| Same ad always shows | `random_ad` vs. an ordered placement — ordered placements show in `adx_displayorder`. |
| Poll does not record a vote | Submission table permission, or the poll is not associated with a placement. |
| Poll results look inflated | Visitor identity is reused across sessions by design; treat results as indicative. |
| Marketing list panel errors | `Profile/ShowMarketingListsPanel` is on but `adx_website_list` does not exist. |
| Marketing panel shows but no lists | No website-to-list associations. |

## Related pages

- [Community Features](Community-Features.md)
- [Users, Profiles and Invitations](Users-Profiles-and-Invitations.md)
- [Site Settings Reference](Site-Settings-Reference.md)
- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md)
