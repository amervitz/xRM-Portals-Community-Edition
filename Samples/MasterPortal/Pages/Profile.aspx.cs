/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Linq;
using System.Web.UI.WebControls;
using Adxstudio.Xrm;
using Adxstudio.Xrm.Account;
using Adxstudio.Xrm.AspNet.Cms;
using Adxstudio.Xrm.Services;
using Adxstudio.Xrm.Services.Query;
using Adxstudio.Xrm.Web;
using Adxstudio.Xrm.Web.UI.WebControls;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Messages;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Site.Pages
{
	public partial class ProfilePage : PortalPage
	{
		private const string _userFetchXmlFormat = @"
			<fetch mapping=""logical"">
				<entity name=""contact"">
					<all-attributes />
					<filter type=""and"">
						<condition attribute=""statecode"" operator=""eq"" value=""0""/>
						<condition attribute=""contactid"" operator=""eq"" value=""{0}""/>
					</filter>
				</entity>
			</fetch>";
		private const string ConfirmationOneTimeMessageSessionKey = "Profile.ConfirmationMessage.OneTimeVisible";

		private bool _forceRedirect;

		public bool ShowMarketingOptionsPanel
		{
			get
			{
				var showMarketingSetting = this.Context.GetSiteSetting("Profile/ShowMarketingOptionsPanel") ?? "false";

				return showMarketingSetting.ToLower() == "true";
			}
		}

		public bool ShowMarketingListsPanel
		{
			get
			{
				var showMarketingListsSetting = this.Context.GetSiteSetting("Profile/ShowMarketingListsPanel") ?? "false";

				return showMarketingListsSetting.ToLower() == "true";
			}
		}

		public bool ForceRegistration
		{
			get
			{
				var siteSetting = this.Context.GetSiteSetting("Profile/ForceSignUp") ?? "false";

				return siteSetting.ToLower() == "true";
			}
		}

		protected void Page_Load(object sender, EventArgs e)
		{
			RedirectToLoginIfAnonymous();

			if (ForceRegistration && !ServiceContext.ValidateProfileSuccessfullySaved(Contact))
			{
				MissingFieldsMessage.Visible = true;
			}

			if (ShowMarketingOptionsPanel)
			{
				MarketingOptionsPanel.Visible = true;
			}

			ProfileDataSource.FetchXml = _userFetchXmlFormat.FormatWith(Contact.Id);
			ProfileDataSource.IsSingleSource = true;

			ProfileAlertInstructions.Visible = Contact.GetAttributeValue<bool?>("adx_profilealert") ?? false;

			if (Session[ConfirmationOneTimeMessageSessionKey] != null)
			{
				ConfirmationMessage.Visible = true;
				Session.Remove(ConfirmationOneTimeMessageSessionKey);
			}

			if (IsPostBack)
			{
				return;
			}

			var contact = !Contact.Contains("donotemail") || !Contact.Contains("donotfax") || !Contact.Contains("donotphone") || !Contact.Contains("donotpostalmail")
				? PortalOrganizationService.RetrieveSingle(
					"contact",
					new[] { "donotemail", "donotfax", "donotphone", "donotpostalmail" },
					new[] {
						new Condition("statecode", ConditionOperator.Equal, 0),
						new Condition("contactid", ConditionOperator.Equal, Contact.Id)
					})
				: Contact;

			if (contact == null)
			{
				throw new ApplicationException(string.Format("Couldn't retrieve contact record with contactid equal to {0}.", Contact.Id));
			}

			if (ShowMarketingOptionsPanel)
			{
				marketEmail.Checked = !contact.GetAttributeValue<bool>("donotemail");
				marketFax.Checked = !contact.GetAttributeValue<bool>("donotfax");
				marketPhone.Checked = !contact.GetAttributeValue<bool>("donotphone");
				marketMail.Checked = !contact.GetAttributeValue<bool>("donotpostalmail");
			}

			if (ShowMarketingListsPanel)
			{
				PopulateMarketingLists();
			}
			else
			{
				MarketingLists.Visible = false;
			}
		}

		protected void SubmitButton_Click(object sender, EventArgs e)
		{
			if (!Page.IsValid)
			{
				return;
			}

			MissingFieldsMessage.Visible = false;

			var contact = XrmContext.MergeClone(Contact);

			if (ShowMarketingListsPanel)
			{
				ManageLists(XrmContext, contact);
			}

			ProfileFormView.UpdateItem();

			var returnUrl = Request["returnurl"];

			var languageContext = this.Context.GetContextLanguageInfo();
			if (languageContext.IsCrmMultiLanguageEnabled && _forceRedirect)
			{
				// When forcing redirect for language change, make the confirmation message visible after redirect
				// It is only needed when redirecting back to Profile page
				if (string.IsNullOrWhiteSpace(returnUrl))
				{
					Session[ConfirmationOneTimeMessageSessionKey] = true;
				}

				// respect returnUrl if it was provided during the form submit
				// otherwise, redirect back to current page
				var redirectUri = string.IsNullOrWhiteSpace(returnUrl) ? Request.Url : returnUrl.AsAbsoluteUri(Request.Url);
				returnUrl = languageContext.FormatUrlWithLanguage(overrideUri: redirectUri);
			}

			if (!string.IsNullOrWhiteSpace(returnUrl))
			{
				Context.RedirectAndEndResponse(returnUrl);
			}
		}

		protected void OnItemUpdating(object sender, CrmEntityFormViewUpdatingEventArgs e)
		{
			e.Values["adx_profilemodifiedon"] = DateTime.UtcNow;
			e.Values["adx_profilealert"] = ProfileAlertInstructions.Visible = false;

			if (e.Values.ContainsKey("emailaddress1")
				&& Contact.GetAttributeValue<bool>("adx_identity_emailaddress1confirmed")
				&& !string.Equals(e.Values["emailaddress1"], Contact.GetAttributeValue<string>("emailaddress1")))
			{
				e.Values["adx_identity_emailaddress1confirmed"] = false;
			}

			if (ShowMarketingOptionsPanel)
			{
				e.Values["donotemail"] = !marketEmail.Checked;
				e.Values["donotbulkemail"] = !marketEmail.Checked;
				e.Values["donotfax"] = !marketFax.Checked;
				e.Values["donotphone"] = !marketPhone.Checked;
				e.Values["donotpostalmail"] = !marketMail.Checked;
			}

			var languageContext = this.Context.GetContextLanguageInfo();
			if (languageContext.IsCrmMultiLanguageEnabled)
			{
				object preferedLanguage;
				e.Values.TryGetValue("adx_preferredlanguageid", out preferedLanguage);
				UpdateCurrentLanguage((EntityReference)preferedLanguage, languageContext);
			}
		}

		protected void OnItemUpdated(object sender, CrmEntityFormViewUpdatedEventArgs e)
		{
			ConfirmationMessage.Visible = true;
		}

		public bool IsListChecked(object listoption)
		{
			var list = (Entity)listoption;

			if (Request.IsAuthenticated)
			{
				var lists = PortalOrganizationService.RetrieveRelatedEntities(this.Contact, "listcontact_association").Entities;

				return lists.Any(l => l.GetAttributeValue<Guid>("listid") == list.Id);
			}

			return false;
		}

		public void ManageLists(OrganizationServiceContext context, Entity contact)
		{
			foreach (var item in MarketingListsListView.Items)
			{
				if (item == null)
				{
					continue;
				}

				var listViewItem = item;

				var hiddenListId = (HiddenField)listViewItem.FindControl("ListID");

				if (hiddenListId == null)
				{
					continue;
				}

				var listId = new Guid(hiddenListId.Value);

				var ml = context.RetrieveSingle("list",
					FetchAttribute.All,
					new Condition("listid", ConditionOperator.Equal, listId));

				var listCheckBox = (CheckBox)item.FindControl("ListCheckbox");

				if (listCheckBox == null)
				{
					continue;
				}

				var contactLists = contact.GetRelatedEntities(XrmContext, new Relationship("listcontact_association")).ToList();

				var inList = contactLists.Any(list => list.GetAttributeValue<Guid>("listid") == ml.Id);

				if (listCheckBox.Checked && !inList)
				{
					context.AddMemberList(ml.GetAttributeValue<Guid>("listid"), contact.GetAttributeValue<Guid>("contactid"));
				}
				else if (!listCheckBox.Checked && inList)
				{
					context.RemoveMemberList(ml.GetAttributeValue<Guid>("listid"), contact.GetAttributeValue<Guid>("contactid"));
				}
			}
		}

		protected void PopulateMarketingLists()
		{
			if (Website == null)
			{
				MarketingLists.Visible = false;
				return;
			}

			var website = XrmContext.CreateQuery("adx_website").FirstOrDefault(w => w.GetAttributeValue<Guid>("adx_websiteid") == Website.Id);

			if (website == null)
			{
				MarketingLists.Visible = false;
				return;
			}

			// Note: Marketing Lists with 'Dynamic' Type (i.e. value of 1 or true) do not support manually adding members

			if (website.GetRelatedEntities(XrmContext, new Relationship("adx_website_list")).All(l => l.GetAttributeValue<bool>("type")))
			{
				MarketingLists.Visible = false;
				return;
			}

			MarketingListsListView.DataSource = website.GetRelatedEntities(XrmContext, new Relationship("adx_website_list")).Where(l => l.GetAttributeValue<bool>("type") == false);

			MarketingListsListView.DataBind();
		}

		/// <summary>
		/// Updates current language based on the changed user preference.
		/// </summary>
		/// <param name="preferredLanguage">Newest language preference for the user (Portal Language). Null if preference not set.</param>
		/// <param name="languageContext">Instance of the current <see cref="ContextLanguageInfo"/>.</param>
		private void UpdateCurrentLanguage(EntityReference preferredLanguage, ContextLanguageInfo languageContext)
		{
			if (languageContext.IsCrmMultiLanguageEnabled)
			{
				var websiteLangauges = languageContext.ActiveWebsiteLanguages.ToArray();
				var newWebsiteLanguage = preferredLanguage == null
					? languageContext.DefaultLanguage
					: languageContext.GetWebsiteLanguageByPortalLanguageId(preferredLanguage.Id, websiteLangauges, true);

				if (languageContext.ContextLanguage != newWebsiteLanguage)
				{
					// redirect is always required to apply a language change
					_forceRedirect = true;
				}
			}
		}
	}
}
