# Events and Conferences

The portal includes an events calendar with registrations, and a conference layer on top of it for
multi-session events with options and pricing.

## Events

| Record | Role |
|---|---|
| `adx_event` | An event: title, description, dates, location. |
| `adx_eventschedule` | A scheduled occurrence of an event (start/end time, recurrence). |
| `adx_eventschedule_eventscheduleexception` | Exceptions to a recurring schedule. |
| `adx_eventregistration` | A registration by a portal user. |
| `adx_eventschedule_eventregistration` | Registration against a specific occurrence. |
| `adx_eventspeaker`, `adx_eventspeaker_event` | Speakers and their event associations. |
| `adx_eventsponsor`, `adx_eventsponsor_event` | Sponsors and their event associations. |
| `adx_eventtag`, `adx_eventtag_event` | Event tags. |
| `adx_eventaccesspermission`, `adx_eventaccesspermission_webrole` | Who may see or register for an event. |
| `adx_eventproduct` | Products associated with an event. |
| `adx_eventsessionfeedback` | Session feedback. |
| `adx_eventspeakerid`, `adx_eventsponsorid` | Speaker/sponsor identity on associations. |

Recurrence fields on a schedule include `adx_recurrence`, `adx_recurrenceenddate`, `adx_interval`,
`adx_maxrecurrences`, `adx_startdate`, `adx_enddate`, `adx_starttime`, `adx_endtime`, `adx_alldayevent`,
plus day-of-week flags (`adx_monday` … `adx_sunday`) and `adx_timezone`.

Movement between occurrences uses `adx_loadeventkeyname`, `adx_savedeventkeyname`,
`adx_movepreviouseventkeyname` and `adx_movepreviouspermitted`.

## Conferences

A conference is a multi-session event with registrations and paid options.

| Record | Role |
|---|---|
| `adx_conference` | The conference. |
| `adx_conferenceoption` | An option (a session, a pass, an add-on). |
| `adx_conferenceregistration` | A registration. |
| `adx_conferenceregistration_option` | The options a registration includes. |
| `adx_conference_conferenceregistration`, `adx_conference_conferencediscount`, `adx_conference_membershiptype` | Associations and discount/membership rules. |

Conferences tie into commerce when options are priced — see [Commerce](Commerce.md).

## Rendering

| Route | What |
|---|---|
| Liquid | The `events` global object: `EventsDrop`, `EventDrop`, `EventOccurrencesDrop`, `EventScheduleDrop`, `EventOccurrenceDrop`, `SpeakerDrop`, `SponsorDrop` |
| Templates | The shipped `events_upcoming` template, plus `events` partials |
| Site map | `EventSiteMapProvider` (and `EventSiteMapHandler`) contributes event pages to the sitemap |
| ASP.NET | `Controls/EventsPanel.ascx`, the Events and Conference areas |
| JavaScript | The Events/Conference area scripts |

Common recipe — upcoming events:

```liquid
{% assign upcoming = events.upcoming | take: 5 %}
{% for event in upcoming %}
  <div class="event">
    <h3><a href="{{ event.url }}">{{ event.title }}</a></h3>
    <time>{{ event.start_date | date: 'yyyy-MM-dd HH:mm' }}</time>
    {% if event.location %}<span class="location">{{ event.location }}</span>{% endif %}
  </div>
{% endfor %}
```

## Access control

Events use their own permission records on top of the general model:

- `adx_eventaccesspermission` linked to `adx_webrole` decides which roles may see or register for an
  event.
- Registration may additionally require the registrant to have a **membership** (via
  `adx_conference_membershiptype` and the memberships feature).

Registration submissions are still gated by table permissions on the registration tables — review both.

## Registration flow

1. A visitor opens an event or conference page.
2. The event/occurrence is resolved, and access permission is evaluated.
3. The visitor registers — creating an `adx_eventregistration` (and, for conferences,
   `adx_conferenceregistration` plus its options).
4. If the option is priced, the flow hands off to commerce (cart/checkout) before the registration is
   confirmed.
5. Confirmation and any notifications follow.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| An event page 404s | No page/template is associated with events, or the event is not published. |
| "Upcoming" is empty | Schedule dates are in the past, or the schedule records are missing. |
| Recurring event appears at the wrong time | `adx_timezone` on the schedule versus the viewer's time zone. |
| Users cannot register | `adx_eventaccesspermission` does not include their web role, or the registration tables have no table permission. |
| Priced registration never completes | Payment provider configuration — see [Commerce](Commerce.md). |

## Related pages

- [Commerce](Commerce.md) — paid registrations
- [Content Management](Content-Management.md)
- [Security and Authentication](Security-and-Authentication.md)
- [Portal Data Model](Portal-Data-Model.md)
