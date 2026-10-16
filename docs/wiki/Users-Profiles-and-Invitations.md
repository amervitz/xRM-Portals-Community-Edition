# Users, Profiles and Invitations

Portal users are **Contacts**. Understanding how a Contact becomes a signed-in, profiled portal user is
the key to user administration.

## The user model

```
Contact (adx_contact)
  ├── adx_username / adx_contactusername   the sign-in name
  ├── emailaddress1                        email (also the two-factor address)
  ├── adx_identity                          credentials and Identity link
  ├── adx_externalidentity                  one row per linked external provider
  ├── adx_webrole assignment                via the role-assignment relationship
  ├── adx_invitation                        invitations issued to or redeemed by the contact
  └── parentcustomerid                      the account the contact belongs to
```

| Concept | Record |
|---|---|
| Portal user | `contact` |
| Stored credential / Identity record | `adx_identity` |
| Linked external login | `adx_externalidentity` |
| Invitation | `adx_invitation`, redemption in `adx_inviteredemption` |
| Language preference | `adx_preferredlanguageid` |
| Last successful sign-in | `adx_lastsuccessfullogon` |
| User image | fields surfaced by `get_user_image_url` |

Implementation lives in `Framework/Adxstudio.Xrm/AspNet/Identity`
(`CrmUser`, `CrmUserStore`, `CrmUserManager`, `CrmSignInManager`, `CrmUserLogin`,
`CrmClaimsIdentityFactory`, `CrmInvitationStore`, `InvitationManager`, `CrmPasswordValidator`,
`CrmEmailTokenProvider`, `CrmPhoneNumberTokenProvider`).

## Registration

Registration behaviour is entirely site-settings driven. The settings you will actually change:

| Goal | Settings |
|---|---|
| Turn registration off | `Authentication/Registration/OpenRegistrationEnabled` = `false` |
| Require an invitation | `Authentication/Registration/InvitationEnabled` = `true`, `OpenRegistrationEnabled` = `false` |
| Use email as the username | `Authentication/Registration/LocalLoginByEmail` = `true` |
| Require email confirmation | `Authentication/Registration/EmailConfirmationEnabled` = `true` |
| Enable two-factor | `Authentication/Registration/TwoFactorEnabled` = `true` |
| Enable phone support | `Authentication/Registration/MobilePhoneEnabled` = `true` |
| Add a captcha | `Authentication/Registration/CaptchaEnabled` = `true` |
| Let users reset their password | `Authentication/Registration/ResetPasswordEnabled` = `true` |
| Force profile completion | `Authentication/Registration/ProfileRedirectEnabled` = `true` |
| Enable external providers | `Authentication/Registration/ExternalLoginEnabled` = `true` |

Identity provides two token providers used for confirmation and code-based two-factor:
`CrmEmailTokenProvider` and `CrmPhoneNumberTokenProvider`. Email flows (confirmation, reset,
invitations, notifications) require working `<mailSettings>` on the server — the sample points at
`127.0.0.1`, which will silently fail in most environments.

The registration and sign-in UI lives in `Samples/MasterPortal/Areas/Account`, guarded by attributes such
as `LocalLoginAttribute`, `ExternalLoginAttribute` and `ThrottleAttribute` (rate limiting).

## Profiles

The profile page (`Pages/Profile.aspx`, `Areas/PublicProfile`) exposes contact fields to the signed-in
user.

The ASP.NET **profile provider** is registered as `CrmContactProfileProvider` with a property:

```xml
<profile defaultProvider="Xrm">
  <providers>
    <clear />
    <add name="Xrm" type="Adxstudio.Xrm.Web.Profile.CrmContactProfileProvider, Adxstudio.Xrm" />
  </providers>
  <properties>
    <add name="FullName" customProviderData="fullname" />
  </properties>
</profile>
```

That mapping is where a profile property name becomes a contact column.

### Anonymous profiles

`<anonymousIdentification enabled="true" cookieName="Dynamics365PortalAnalytics" ... />` enables an
anonymous profile, so a visitor can carry lightweight state before signing in.
`Profile_MigrateAnonymous` in `Global.asax.cs` and `IPortalAreaRegistrationState` handle migrating that
state to the authenticated user when the visitor signs in.

### Marketing lists on the profile

The profile's marketing-list panel is **optional**:

| Setting | Default | Effect |
|---|---|---|
| `Profile/ShowMarketingListsPanel` | `false` | Whether the marketing-list picker appears on the profile. |

It defaults to `false` because the `adx_website_list` relationship may not exist in every environment.

## Invitations

Invitations let you control who can register — the standard pattern for partner portals and B2B sites.

| Record | Purpose |
|---|---|
| `adx_invitation` | An invitation: `adx_invitationcode`, `adx_invitecontact`, `adx_inviteredemption`, `adx_invitationcodeexpirydate`, `adx_maximumredemptions`, `adx_redeemedcontact`, `adx_redemptions` |
| `adx_inviteredemption` | Records an individual redemption |

Invitation flow:

1. You create an `adx_invitation` with a code, an optional expiry date and a maximum redemption count,
   associated with the contact who should receive it.
2. You send the recipient the registration URL containing the invitation code.
3. The recipient registers; the redemption is recorded and the `adx_invitation` counts usage.

`ApplicationInvitationManager` and `CrmInvitationStore` implement this; `Authentication/Registration/InvitationEnabled`
turns it on.

**Operational notes:** expiries and maximum redemptions are enforced, so a support call about "my
invitation stopped working" is usually an exhausted or expired invitation record.

## Public profiles

The `PublicProfile` area renders a public view of a portal user — useful for community features (forum
authors, idea submitters). Records include `adx_publicprofilecopy` for the profile body.

Whether a profile is public, and what it exposes, is a content and permission decision — review it before
enabling public profiles on a site with personal data.

## Roles and account membership

- Assign web roles to contacts (and optionally accounts) to grant permissions. See
  [Security and Authentication](Security-and-Authentication.md).
- `parentcustomerid` associates a contact with an account, which matters for account-scoped table
  permissions and for `adx_filteraccount` on entity lists.
- Partner scenarios use `adx_partnercreated`, `adx_numberofopportunities*` and the Partner area.

## Common administrative tasks

| Task | How |
|---|---|
| Create a portal user manually | Create a `contact`, set `adx_username`, create the `adx_identity`, and assign web roles. |
| Lock or disable a user | Use the lockout fields/Identity lockout, or disable the contact. |
| Reset a password | `Authentication/Registration/ResetPasswordEnabled` and the reset flow; or administratively reset via the Identity store. |
| Require email confirmation | `Authentication/Registration/EmailConfirmationEnabled` = `true` **and** working SMTP. |
| Force sign-out everywhere | `Authentication/Registration/SignOutEverywhereEnabled` = `true` (invalidates security stamps). |
| Let users delete their account | Governed by `Authentication/UserStore/DeleteByStatusCode`. |
| Change who can register | The registration settings above. |
| Give a group access to a page | Add a web page access control rule for their web role. |
| Give a group access to a table | Add a table permission for their web role. |

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Confirmation or reset email never arrives | `<mailSettings>` still points at `127.0.0.1`, or the sender address is rejected. |
| User signs in but sees no content | Their web roles carry no permissions, or none apply to what they are viewing. |
| "Remember me" does not last | `ExpireTimeSpan` / `SlidingExpiration` on the application cookie, or a missing machine key causing cookies to invalidate on restart. |
| Users signed out after a restart | No `<machineKey>` configured. |
| Registration rejected | `OpenRegistrationEnabled` is `false` and no valid invitation is present. |
| Invitation does not work | Expired, exhausted, or bound to a different contact. |
| Anti-forgery error during sign-in | Output caching on a page that emits an anti-forgery token — see [Security and Authentication](Security-and-Authentication.md#anti-forgery-and-output-caching). |

## Related pages

- [Security and Authentication](Security-and-Authentication.md)
- [Site Settings Reference](Site-Settings-Reference.md#authentication)
- [Portal Administration](Portal-Administration.md)
- [Ads, Polls and Marketing](Ads-Polls-and-Marketing.md) — marketing lists
