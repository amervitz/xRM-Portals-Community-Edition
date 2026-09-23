/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Cms
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Adxstudio.Xrm.Resources;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Sdk;
	using Newtonsoft.Json.Linq;
	using Adxstudio.Xrm.Cms.SolutionVersions;

	[Serializable]
	public abstract class EntityNode
	{
		private readonly EntityReference _reference;
		private readonly Entity _entity;
		private readonly string _alias;
		private readonly string _logicalName;
		private readonly string _idAttributeName;
		private readonly string _nameAttributeName;

		public Guid Id { get { return IsReference ? _reference.Id : GetValue<Guid>(_idAttributeName); } }
		public virtual string Name { get { return IsReference ? _reference.Name : GetValue<string>(_nameAttributeName); } }
		public bool IsReference { get { return _reference != null; } }

		protected EntityNode(Entity entity, string alias, string logicalName, string idAttributeName)
			: this(entity, alias, logicalName, idAttributeName, "adx_name")
		{
		}

		protected EntityNode(Entity entity, string alias, string logicalName, string idAttributeName, string nameAttributeName)
		{
			_entity = entity;
			_alias = alias;
			_logicalName = logicalName;
			_idAttributeName = idAttributeName;
			_nameAttributeName = nameAttributeName;
		}

		protected EntityNode(EntityReference reference, string logicalName, string idAttributeName)
			: this(reference, logicalName, idAttributeName, "adx_name")
		{
		}

		protected EntityNode(EntityReference reference, string logicalName, string idAttributeName, string nameAttributeName)
		{
			_reference = reference;
			_logicalName = logicalName;
			_idAttributeName = idAttributeName;
			_nameAttributeName = nameAttributeName;
		}

		protected T GetValue<T>(string attributeLogicalName)
		{
			if (_entity == null)
			{
				CmsEventSource.Log.ContentMapReferenceNodeAccess(ToEntityReference(), attributeLogicalName);

				throw new InvalidOperationException("Can't get an attribute value from a reference node.");
			}

			return _entity.GetAttributeAliasedValue<T>(attributeLogicalName, _alias);
		}

		public EntityReference ToEntityReference()
		{
			// do not use the original _entity.LogicalName/Id/Name since it could be an aliased entity, use the de-aliased values

			return _reference ?? new EntityReference(_logicalName, Id) { Name = Name };
		}

		public Entity ToEntity(Type entityType = null)
		{
			if (entityType != null && !entityType.IsA<Entity>())
			{
				throw new InvalidOperationException("The specified type {0} isn't a type of {1}.".FormatWith(entityType, typeof(Entity)));
			}

			if (IsReference)
			{
				var attributes = new List<KeyValuePair<string, object>>
				{
					new KeyValuePair<string, object>(_idAttributeName, _reference.Id),
				};

				if (!string.IsNullOrWhiteSpace(_reference.Name))
				{
					attributes.Add(new KeyValuePair<string, object>(_nameAttributeName, _reference.Name));
				}

				var entity = CreateEntity(entityType, _reference.LogicalName, _reference.Id, null, attributes, null);

				return entity;
			}

			if (string.IsNullOrWhiteSpace(_alias))
			{
				// this is a non-aliased entity

				if (entityType == null || entityType == _entity.GetType())
				{
					return _entity;
				}

				var attributes = _entity.Attributes;
				var names = _entity.FormattedValues;

				var entity = CreateEntity(entityType, _logicalName, Id, _entity.EntityState, attributes, names);

				return entity;
			}
			else
			{
				// transfer the appropriate aliased attributes

				var attributes = _entity.Attributes.Where(a => a.Key.StartsWith(_alias + ".")).Select(Dealias).ToList();
				var names = _entity.FormattedValues.Where(a => a.Key.StartsWith(_alias + ".")).Select(Dealias).ToList();

				var entity = CreateEntity(entityType, _logicalName, Id, _entity.EntityState, attributes, names);

				return entity;
			}
		}

		private static Entity CreateEntity(
			Type entityType, string logicalName, Guid id, EntityState? entityState,
			IEnumerable<KeyValuePair<string, object>> attributes,
			IEnumerable<KeyValuePair<string, string>> names)
		{
			var entity = entityType == null || entityType == typeof(Entity) || entityType == typeof(CrmEntity)
				? new CrmEntity(logicalName)
				: Activator.CreateInstance(entityType) as Entity;

			entity.Attributes.AddRange(attributes);
			if (names != null) entity.FormattedValues.AddRange(names);

			entity.LogicalName = logicalName;
			entity.Id = id;
			entity.EntityState = entityState;

			return entity;
		}

		private KeyValuePair<string, object> Dealias(KeyValuePair<string, object> attribute)
		{
			return new KeyValuePair<string, object>(
				attribute.Key.Substring(_alias.Length + 1),
				attribute.Value is AliasedValue ? (attribute.Value as AliasedValue).Value : attribute.Value);
		}

		private KeyValuePair<string, string> Dealias(KeyValuePair<string, string> attribute)
		{
			return new KeyValuePair<string, string>(attribute.Key.Substring(_alias.Length + 1), attribute.Value);
		}

		public JObject ToJson()
		{
			var reference = ToEntityReference();

			var d = new JObject
			{
				{
					"__metadata",
					new JObject
					{
						{ "type", new JValue(GetType().FullName) },
					}
				},
				{ "Id", new JValue(reference.Id.ToString()) },
				{ "LogicalName", new JValue(reference.LogicalName) },
				{ "Name", new JValue(reference.Name) },
				{ "IsReference", new JValue(IsReference) },
			};

			return new JObject { { "d", ToJson(d) } };
		}

		protected virtual JObject ToJson(JObject d)
		{
			return d;
		}
	}

	[Serializable]
	public class PublishingStateNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_displayorder", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_isdefault", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_isvisible", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public int? DisplayOrder { get { return GetValue<int?>("adx_displayorder"); } }
		public bool? IsDefault { get { return GetValue<bool?>("adx_isdefault"); } }
		public bool? IsVisible { get { return GetValue<bool?>("adx_isvisible"); } }

		public WebsiteNode Website { get; set; }

		public ICollection<WebPageAccessControlRuleToPublishingStateNode> WebPageAccessControlRuleIntersects { get; private set; }

		public IEnumerable<WebPageAccessControlRuleNode> WebPageAccessControlRules
		{
			get { return WebPageAccessControlRuleIntersects.Select(rulestate => rulestate.WebPageAccessControlRule); }
		}

		public PublishingStateNode(EntityReference reference)
			: base(reference, "adx_publishingstate", "adx_publishingstateid")
		{
		}

		public PublishingStateNode(Entity entity, string alias)
			: base(entity, alias, "adx_publishingstate", "adx_publishingstateid")
		{
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);

			WebPageAccessControlRuleIntersects = new List<WebPageAccessControlRuleToPublishingStateNode>();
		}
	}

	[Serializable]
	public class WebRoleNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_webroleid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_authenticatedusersrole", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_anonymoususersrole", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_description", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public string Description { get { return GetValue<string>("adx_description"); } }
		public bool? AuthenticatedUsersRole { get { return GetValue<bool?>("adx_authenticatedusersrole"); } }
		public bool? AnonymousUsersRole { get { return GetValue<bool?>("adx_anonymoususersrole"); } }

		public WebsiteNode Website { get; set; }

		public ICollection<WebPageAccessControlRuleToWebRoleNode> WebPageAccessControlRuleIntersects { get; private set; }

		public ICollection<IdeaForumWriteToWebRoleNode> IdeaForumWriteIntersects { get; private set; }

		public ICollection<IdeaForumReadToWebRoleNode> IdeaForumReadIntersects { get; private set; }

		public ICollection<ForumAccessPermissionsToWebRoleNode> ForumAccessPermissionsIntersects { get; private set; }

		public ICollection<BlogToWebRoleNode> BlogPermissionIntersects { get; private set; }

		public ICollection<WebsiteAccessToWebRoleNode> WebsiteAccessIntersects { get; private set; }

		public IEnumerable<WebPageAccessControlRuleNode> WebPageAccessControlRules
		{
			get { return WebPageAccessControlRuleIntersects.Select(rulerole => rulerole.WebPageAccessControlRule); }
		}

		public IEnumerable<WebsiteAccessNode> WebsiteAccesses
		{
			get { return WebsiteAccessIntersects.Select(accessrole => accessrole.WebsiteAccess); }
		}

		public WebRoleNode(EntityReference reference)
			: base(reference, "adx_webrole", "adx_webroleid")
		{
		}

		public WebRoleNode(Entity entity, string alias)
			: base(entity, alias, "adx_webrole", "adx_webroleid")
		{
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);

			WebPageAccessControlRuleIntersects = new List<WebPageAccessControlRuleToWebRoleNode>();
			WebsiteAccessIntersects = new List<WebsiteAccessToWebRoleNode>();
			IdeaForumReadIntersects = new List<IdeaForumReadToWebRoleNode>();
			IdeaForumWriteIntersects = new List<IdeaForumWriteToWebRoleNode>();
			ForumAccessPermissionsIntersects = new List<ForumAccessPermissionsToWebRoleNode>();
			BlogPermissionIntersects = new List<BlogToWebRoleNode>();
		}
	}

	[Serializable]
	public class WebPageAccessControlRuleNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_webpageaccesscontrolruleid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_description", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_right", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webpageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_scope", BaseSolutionVersions.PotassiumVersion) 
		};

		public string Description { get { return GetValue<string>("adx_description"); } }
		public int? Right { get { return GetValue<int?>("adx_right"); } }
		public int? Scope => GetValue<int?>("adx_scope");

		public WebPageNode WebPage { get; set; }
		public WebsiteNode Website { get; set; }

		public ICollection<WebPageAccessControlRuleToWebRoleNode> WebRoleIntersects { get; private set; }
		public ICollection<WebPageAccessControlRuleToPublishingStateNode> PublishingStateIntersects { get; private set; }

		public IEnumerable<WebRoleNode> WebRoles
		{
			get { return WebRoleIntersects.Select(rulerole => rulerole.WebRole); }
		}

		public IEnumerable<PublishingStateNode> PublishingStates
		{
			get { return PublishingStateIntersects.Select(rulestate => rulestate.PublishingState); }
		}

		public WebPageAccessControlRuleNode(EntityReference reference)
			: base(reference, "adx_webpageaccesscontrolrule", "adx_webpageaccesscontrolruleid")
		{
		}

		public WebPageAccessControlRuleNode(Entity entity, string alias)
			: base(entity, alias, "adx_webpageaccesscontrolrule", "adx_webpageaccesscontrolruleid")
		{
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
			WebPage = entity.GetEntityIdentifier<WebPageNode>("adx_webpageid", alias);

			WebRoleIntersects = new List<WebPageAccessControlRuleToWebRoleNode>();
			PublishingStateIntersects = new List<WebPageAccessControlRuleToPublishingStateNode>();
		}
	}

	[Serializable]
	public class WebPageNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_webpageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_authorid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_category", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_copy", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_customcss", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_customjavascript", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_displaydate", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_displayorder", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_expirationdate", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_hiddenfromsitemap", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_image", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_imageurl", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_masterwebpageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_meta_description", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_navigation", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_pagetemplateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_parentpageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_partialurl", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_releasedate", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_subjectid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_summary", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_title", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("modifiedon", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webpagelanguageid", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_rootwebpageid", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_isroot", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_enablerating", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_feedbackpolicy", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_alloworigin", BaseSolutionVersions.PotassiumVersion),
		};

		public static readonly string[] ColumnSetWebForms = new[]
		{
			"adx_entityform",
			"adx_entitylist",
			"adx_webform",
		};

		public EntityReference EntityForm { get { return GetValue<EntityReference>("adx_entityform"); } }
		public EntityReference EntityList { get { return GetValue<EntityReference>("adx_entitylist"); } }
		public EntityReference WebForm { get { return GetValue<EntityReference>("adx_webform"); } }
		public int? Category { get { return GetValue<int?>("adx_category"); } }
		public string Copy { get { return GetValue<string>("adx_copy"); } }
		public string CreatedByIPAddress { get { return GetValue<string>("adx_createdbyipaddress"); } }
		public string CreatedByUsername { get { return GetValue<string>("adx_createdbyusername"); } }
		public DateTime? DisplayDate { get { return GetValue<DateTime?>("adx_displaydate"); } }
		public int? DisplayOrder { get { return GetValue<int?>("adx_displayorder"); } }
		public DateTime? ExpirationDate { get { return GetValue<DateTime?>("adx_expirationdate"); } }
		public bool? HiddenFromSiteMap { get { return GetValue<bool?>("adx_hiddenfromsitemap"); } }
		public string ImageUrl { get { return GetValue<string>("adx_imageurl"); } }
		public string ModifiedByIPAddress { get { return GetValue<string>("adx_modifiedbyipaddress"); } }
		public string ModifiedByUsername { get { return GetValue<string>("adx_modifiedbyusername"); } }
		public string PartialUrl { get { return GetValue<string>("adx_partialurl"); } }
		public DateTime? ReleaseDate { get { return GetValue<DateTime?>("adx_releasedate"); } }
		public string Summary { get { return GetValue<string>("adx_summary"); } }
		public string Title { get { return GetValue<string>("adx_title"); } }
		public DateTime? ModifiedOn { get { return GetValue<DateTime?>("modifiedon"); } }
		public int? StateCode { get { return GetValue<int?>("statecode"); } }
		public int? StatusCode { get { return GetValue<int?>("statuscode"); } }
		public bool? IsRoot { get { return GetValue<bool?>("adx_isroot"); } }
		public string AllowOrigin { get { return GetValue<string>("adx_alloworigin"); } }
		public bool? IsCircularReference { get; set; }

		public WebFileNode Image { get; set; }
		public WebPageNode Master { get; set; }
		public WebLinkSetNode Navigation { get; set; }
		public PageTemplateNode PageTemplate { get; set; }
		public WebPageNode Parent { get; set; }
		public PublishingStateNode PublishingState { get; set; }
		public SubjectNode Subject { get; set; }
		public WebsiteNode Website { get; set; }
		public WebPageNode RootWebPage { get; set; }
		public WebsiteLanguageNode WebPageLanguage { get; set; }

		public ICollection<ShortcutNode> Shortcuts { get; private set; }
		public ICollection<SiteMarkerNode> SiteMarkers { get; private set; }
		public ICollection<WebPageNode> WebPages { get; private set; }
		public ICollection<WebPageNode> Subscribers { get; private set; }
		public ICollection<WebPageNode> LanguageContentPages { get; private set; }
		public ICollection<ForumNode> Forums { get; private set; }

		// Language-agnostic WebPage properties
		protected ICollection<WebPageAccessControlRuleNode> _webPageAccessControlRules;
		protected ICollection<WebFileNode> _webFiles;

		public ICollection<WebPageAccessControlRuleNode> WebPageAccessControlRules
		{
			get
			{
				// Property is language-agnostic, so return the Root WebPage's copy of this, unless this is the Root.
				if (this.IsRoot == false && this.RootWebPage != null && !this.RootWebPage.IsReference)
				{
					if (this == this.RootWebPage)
					{
						ADXTrace.Instance.TraceError(TraceCategory.Application, $"Invalid content: Cyclic reference on RootWebPage: {this.Id}");
					}
					else if (this.RootWebPage.IsRoot == false)
					{
						ADXTrace.Instance.TraceError(TraceCategory.Application, $"Invalid content: RootWebPage lookup is not root: {this.Id}");
					}
					else if (this.RootWebPage.WebPageAccessControlRules != null)
					{
						// Return a memberwise clone of the root's WebPageAccessControlRules so even if caller removes items from the collection, it won't affect the root's copy.
						return new List<WebPageAccessControlRuleNode>(this.RootWebPage.WebPageAccessControlRules);
					}
				}
				return this._webPageAccessControlRules;
			}
		}

		public ICollection<WebFileNode> WebFiles
		{
			get
			{
				// Property is language-agnostic, so return the Root WebPage's copy of this, unless this is the Root.
				if (this.IsRoot == false && this.RootWebPage != null && !this.RootWebPage.IsReference)
				{
					if (this == this.RootWebPage)
					{
						ADXTrace.Instance.TraceError(TraceCategory.Application, $"Invalid content: Cyclic reference on RootWebPage: {this.Id}");
					}
					else if (this.RootWebPage.IsRoot == false)
					{
						ADXTrace.Instance.TraceError(TraceCategory.Application, $"Invalid content: RootWebPage lookup is not root: {this.Id}");
					}
					else if (this.RootWebPage.WebFiles != null)
					{
						// Return a memberwise clone of the root's WebFiles so even if caller removes items from the collection, it won't affect the root's copy.
						return new List<WebFileNode>(this.RootWebPage.WebFiles);
					}
				}
				return this._webFiles;
			}
		}

		public WebPageNode(EntityReference reference)
			: base(reference, "adx_webpage", "adx_webpageid")
		{
		}

		public WebPageNode(Entity entity, string alias)
			: base(entity, alias, "adx_webpage", "adx_webpageid")
		{
			Image = entity.GetEntityIdentifier<WebFileNode>("adx_image", alias);
			Master = entity.GetEntityIdentifier<WebPageNode>("adx_masterwebpageid", alias);
			Navigation = entity.GetEntityIdentifier<WebLinkSetNode>("adx_navigation", alias);
			PageTemplate = entity.GetEntityIdentifier<PageTemplateNode>("adx_pagetemplateid", alias);
			Parent = entity.GetEntityIdentifier<WebPageNode>("adx_parentpageid", alias);
			PublishingState = entity.GetEntityIdentifier<PublishingStateNode>("adx_publishingstateid", alias);
			Subject = entity.GetEntityIdentifier<SubjectNode>("adx_subjectid", alias);
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
			this.RootWebPage = entity.GetEntityIdentifier<WebPageNode>("adx_rootwebpageid", alias);
			this.WebPageLanguage = entity.GetEntityIdentifier<WebsiteLanguageNode>("adx_webpagelanguageid", alias);
			IsCircularReference = null;

			Shortcuts = new List<ShortcutNode>();
			SiteMarkers = new List<SiteMarkerNode>();
			WebPages = new List<WebPageNode>();
			LanguageContentPages = new List<WebPageNode>();
			Subscribers = new List<WebPageNode>();
			Forums = new List<ForumNode>();

			this._webPageAccessControlRules = new List<WebPageAccessControlRuleNode>();
			this._webFiles = new List<WebFileNode>();
		}

		protected override JObject ToJson(JObject d)
		{
			d["Category"] = Category;
			d["Copy"] = Copy;
			d["CreatedByIPAddress"] = CreatedByIPAddress;
			d["CreatedByUsername"] = CreatedByUsername;
			d["DisplayDate"] = DisplayDate;
			d["DisplayOrder"] = DisplayOrder;
			d["EntityForm"] = EntityForm == null ? null : new JObject(new { EntityForm.Id, EntityForm.LogicalName });
			d["EntityList"] = EntityList == null ? null : new JObject(new { EntityList.Id, EntityList.LogicalName });
			d["ExpirationDate"] = ExpirationDate;
			d["HiddenFromSiteMap"] = HiddenFromSiteMap;
			d["ImageUrl"] = ImageUrl;
			d["ModifiedByIPAddress"] = ModifiedByIPAddress;
			d["ModifiedByUsername"] = ModifiedByUsername;
			d["PartialUrl"] = PartialUrl;
			d["Summary"] = Summary;
			d["Title"] = Title;
			d["ReleaseDate"] = ReleaseDate;
			d["ModifiedOn"] = ModifiedOn;
			d["StateCode"] = StateCode;
			d["StatusCode"] = StatusCode;
			d["WebForm"] = WebForm == null ? null : new JObject(new { WebForm.Id, WebForm.LogicalName });
			d["AllowOrigin"] = AllowOrigin;

			return d;
		}
	}

	[Serializable]
	public class WebsiteNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = AspNet.Cms.WebsiteConstants.WebsiteAttributes;

		public int? StateCode { get { return GetValue<int?>("statecode"); } }
		public int? WebsiteLanguage { get { return GetValue<int?>("adx_website_language"); } }

		public WebsiteNode Master { get; set; }
		public PortalLanguageNode BaseLanguage { get; set; }
		public WebsiteLanguageNode DefaultLanguage { get; set; }
		public Version CurrentBaseSolutionCrmVersion { get; set; }

		public ICollection<WebPageNode> WebPages { get; private set; }
		public ICollection<WebLinkSetNode> WebLinkSets { get; private set; }
		public ICollection<WebFileNode> WebFiles { get; private set; }
		public ICollection<SiteMarkerNode> SiteMarkers { get; private set; }
		public ICollection<PageTemplateNode> PageTemplates { get; private set; }
		public ICollection<PublishingStateNode> PublishingStates { get; private set; }
		public ICollection<ContentSnippetNode> ContentSnippets { get; private set; }
		public ICollection<WebsiteAccessNode> WebsiteAccesses { get; private set; }
		public ICollection<ShortcutNode> Shortcuts { get; private set; }
		public ICollection<WebRoleNode> WebRoles { get; private set; }
		public ICollection<WebPageAccessControlRuleNode> WebPageAccessControlRules { get; private set; }
		public ICollection<WebsiteNode> Subscribers { get; private set; }
		public ICollection<WebsiteLanguageNode> WebsiteLanguages { get; private set; }
		public ICollection<ForumNode> Forums { get; private set; }
		public ICollection<BlogNode> Blogs { get; private set; }
		public ICollection<IdeaForumNode> Ideas { get; private set; }

		public WebsiteNode(EntityReference reference)
			: base(reference, "adx_website", "adx_websiteid")
		{
		}

		public WebsiteNode(Entity entity, string alias)
			: base(entity, alias, "adx_website", "adx_websiteid")
		{
			Master = entity.GetEntityIdentifier<WebsiteNode>("adx_parentwebsiteid", alias);
			DefaultLanguage = entity.GetEntityIdentifier<WebsiteLanguageNode>("adx_defaultlanguage", alias);

			WebPages = new List<WebPageNode>();
			WebLinkSets = new List<WebLinkSetNode>();
			WebFiles = new List<WebFileNode>();
			SiteMarkers = new List<SiteMarkerNode>();
			PageTemplates = new List<PageTemplateNode>();
			PublishingStates = new List<PublishingStateNode>();
			ContentSnippets = new List<ContentSnippetNode>();
			WebsiteAccesses = new List<WebsiteAccessNode>();
			Shortcuts = new List<ShortcutNode>();
			WebRoles = new List<WebRoleNode>();
			WebPageAccessControlRules = new List<WebPageAccessControlRuleNode>();
			Subscribers = new List<WebsiteNode>();
			WebsiteLanguages = new List<WebsiteLanguageNode>();
			Forums = new List<ForumNode>();
			Blogs = new List<BlogNode>();
			Ideas = new List<IdeaForumNode>();
		}
	}

	[Serializable]
	public class WebLinkSetNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_weblinksetid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_copy", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_title", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websitelanguageid", BaseSolutionVersions.CentaurusVersion)
		};

		public string Copy { get { return GetValue<string>("adx_copy"); } }
		public string Title { get { return GetValue<string>("adx_title"); } }
		public PublishingStateNode PublishingState { get; set; }
		public WebsiteNode Website { get; set; }
		public ICollection<WebLinkNode> WebLinks { get; private set; }
		public WebsiteLanguageNode WebsiteLanguage { get; set; }

		public WebLinkSetNode(EntityReference reference)
				: base(reference, "adx_weblinkset", "adx_weblinksetid")
		{
		}

		public WebLinkSetNode(Entity entity, string alias)
			: base(entity, alias, "adx_weblinkset", "adx_weblinksetid")
		{
			PublishingState = entity.GetEntityIdentifier<PublishingStateNode>("adx_publishingstateid", alias);
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
			WebsiteLanguage = entity.GetEntityIdentifier<WebsiteLanguageNode>("adx_websitelanguageid", alias);
			WebLinks = new List<WebLinkNode>();
		}
	}

	[Serializable]
	public class WebLinkNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_weblinkid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_description", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_disablepagevalidation", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_displayorder", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_externalurl", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_imagealttext", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_imageheight", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_imageurl", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_imagewidth", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_openinnewwindow", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_pageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_robotsfollowlink", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_weblinksetid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public string CreatedByIPAddress { get { return GetValue<string>("adx_createdbyipaddress"); } }
		public string CreatedByUsername { get { return GetValue<string>("adx_createdbyusername"); } }
		public string Description { get { return GetValue<string>("adx_description"); } }
		public bool? DisablePageValidation { get { return GetValue<bool?>("adx_disablepagevalidation"); } }
		public int? DisplayOrder { get { return GetValue<int?>("adx_displayorder"); } }
		public string ExternalUrl { get { return GetValue<string>("adx_externalurl"); } }
		public string ImageAltText { get { return GetValue<string>("adx_imagealttext"); } }
		public int? ImageHeight { get { return GetValue<int?>("adx_imageheight"); } }
		public string ImageUrl { get { return GetValue<string>("adx_imageurl"); } }
		public int? ImageWidth { get { return GetValue<int?>("adx_imagewidth"); } }
		public string ModifiedByIPAddress { get { return GetValue<string>("adx_modifiedbyipaddress"); } }
		public string ModifiedByUsername { get { return GetValue<string>("adx_modifiedbyusername"); } }
		public bool? OpenInNewWindow { get { return GetValue<bool?>("adx_openinnewwindow"); } }
		public bool? RobotsFollowLink { get { return GetValue<bool?>("adx_robotsfollowlink"); } }
		public int? StateCode { get { return GetValue<int?>("statecode"); } }
		public int? StatusCode { get { return GetValue<int?>("statuscode"); } }

		public WebPageNode WebPage { get; set; }
		public PublishingStateNode PublishingState { get; set; }
		public WebLinkSetNode WebLinkSet { get; set; }

		public WebLinkNode(EntityReference reference)
			: base(reference, "adx_weblink", "adx_weblinkid")
		{
		}

		public WebLinkNode(Entity entity, string alias)
			: base(entity, alias, "adx_weblink", "adx_weblinkid")
		{
			WebPage = entity.GetEntityIdentifier<WebPageNode>("adx_pageid", alias);
			PublishingState = entity.GetEntityIdentifier<PublishingStateNode>("adx_publishingstateid", alias);
			WebLinkSet = entity.GetEntityIdentifier<WebLinkSetNode>("adx_weblinksetid", alias);
		}
	}

	[Serializable]
	public class WebsiteAccessNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_websiteaccessid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_managecontentsnippets", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_managesitemarkers", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_manageweblinksets", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_previewunpublishedentities", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public bool? ManageContentSnippets { get { return GetValue<bool?>("adx_managecontentsnippets"); } }
		public bool? ManageSiteMarkers { get { return GetValue<bool?>("adx_managesitemarkers"); } }
		public bool? ManageWebLinkSets { get { return GetValue<bool?>("adx_manageweblinksets"); } }
		public bool? PreviewUnpublishedEntities { get { return GetValue<bool?>("adx_previewunpublishedentities"); } }

		public WebsiteNode Website { get; set; }

		public ICollection<WebsiteAccessToWebRoleNode> WebRoleIntersects { get; private set; }

		public IEnumerable<WebRoleNode> WebRoles
		{
			get { return WebRoleIntersects.Select(accessrole => accessrole.WebRole); }
		}

		public WebsiteAccessNode(EntityReference reference)
			: base(reference, "adx_websiteaccess", "adx_websiteaccessid")
		{
		}

		public WebsiteAccessNode(Entity entity, string alias)
			: base(entity, alias, "adx_websiteaccess", "adx_websiteaccessid")
		{
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);

			WebRoleIntersects = new List<WebsiteAccessToWebRoleNode>();
		}
	}

	[Serializable]
	public class SubjectNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("subjectid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("title", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public SubjectNode(EntityReference reference)
			: base(reference, "subject", "subjectid", "title")
		{
		}

		public SubjectNode(Entity entity, string alias)
			: base(entity, alias, "subject", "subjectid", "title")
		{
		}
	}

	[Serializable]
	public class WebFileNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_webfileid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_cloudblobaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_contentdisposition", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_displaydate", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_displayorder", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_expirationdate", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_hiddenfromsitemap", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_masterwebfileid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_parentpageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_partialurl", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_releasedate", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_subjectid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_summary", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_title", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("modifiedon", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_alloworigin", BaseSolutionVersions.PotassiumVersion),
		};

		public static readonly string[] ColumnSetBlogs = new[]
		{
			"adx_blogpostid",
		};

		public EntityReference BlogPost { get { return GetValue<EntityReference>("adx_blogpostid"); } }
		public string CloudBlobAddress { get { return GetValue<string>("adx_cloudblobaddress"); } }
		public int? ContentDisposition { get { return GetValue<int?>("adx_contentdisposition"); } }
		public string CreatedByIPAddress { get { return GetValue<string>("adx_createdbyipaddress"); } }
		public string CreatedByUsername { get { return GetValue<string>("adx_createdbyusername"); } }
		public DateTime? DisplayDate { get { return GetValue<DateTime?>("adx_displaydate"); } }
		public int? DisplayOrder { get { return GetValue<int?>("adx_displayorder"); } }
		public DateTime? ExpirationDate { get { return GetValue<DateTime?>("adx_expirationdate"); } }
		public bool? HiddenFromSiteMap { get { return GetValue<bool?>("adx_hiddenfromsitemap"); } }
		public string ModifiedByIPAddress { get { return GetValue<string>("adx_modifiedbyipaddress"); } }
		public string ModifiedByUsername { get { return GetValue<string>("adx_modifiedbyusername"); } }
		public string PartialUrl { get { return GetValue<string>("adx_partialurl"); } }
		public DateTime? ReleaseDate { get { return GetValue<DateTime?>("adx_releasedate"); } }
		public string Summary { get { return GetValue<string>("adx_summary"); } }
		public string Title { get { return GetValue<string>("adx_title"); } }
		public DateTime? ModifiedOn { get { return GetValue<DateTime?>("modifiedon"); } }
		public int? StateCode { get { return GetValue<int?>("statecode"); } }
		public int? StatusCode { get { return GetValue<int?>("statuscode"); } }
		public string AllowOrigin { get { return GetValue<string>("adx_alloworigin"); } }

		public WebFileNode Master { get; set; }
		public WebPageNode Parent { get; set; }
		public PublishingStateNode PublishingState { get; set; }
		public SubjectNode Subject { get; set; }
		public WebsiteNode Website { get; set; }

		public ICollection<AnnotationNode> Annotations;
		public ICollection<WebFileNode> Subscribers;

		public WebFileNode(EntityReference reference)
			: base(reference, "adx_webfile", "adx_webfileid")
		{
		}

		public WebFileNode(Entity entity, string alias)
			: base(entity, alias, "adx_webfile", "adx_webfileid")
		{
			Master = entity.GetEntityIdentifier<WebFileNode>("adx_masterwebfileid", alias);
			Parent = entity.GetEntityIdentifier<WebPageNode>("adx_parentpageid", alias);
			PublishingState = entity.GetEntityIdentifier<PublishingStateNode>("adx_publishingstateid", alias);
			Subject = entity.GetEntityIdentifier<SubjectNode>("adx_subjectid", alias);
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);

			Annotations = new List<AnnotationNode>();
			Subscribers = new List<WebFileNode>();
		}

		protected override JObject ToJson(JObject d)
		{
			d["BlogPost"] = BlogPost == null ? null : new JObject(new { BlogPost.Id, BlogPost.LogicalName });
			d["CloudBlobAddress"] = CloudBlobAddress;
			d["ContentDisposition"] = ContentDisposition;
			d["CreatedByIPAddress"] = CreatedByIPAddress;
			d["CreatedByUsername"] = CreatedByUsername;
			d["DisplayDate"] = DisplayDate;
			d["DisplayOrder"] = DisplayOrder;
			d["ExpirationDate"] = ExpirationDate;
			d["HiddenFromSiteMap"] = HiddenFromSiteMap;
			d["ModifiedByIPAddress"] = ModifiedByIPAddress;
			d["ModifiedByUsername"] = ModifiedByUsername;
			d["PartialUrl"] = PartialUrl;
			d["ReleaseDate"] = ReleaseDate;
			d["Summary"] = Summary;
			d["Title"] = Title;
			d["ModifiedOn"] = ModifiedOn;
			d["StateCode"] = StateCode;
			d["StatusCode"] = StatusCode;
			d["AllowOrigin"] = AllowOrigin;

			return d;
		}
	}

	[Serializable]
	public class SiteMarkerNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_sitemarkerid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_pageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public WebPageNode WebPage { get; set; }
		public WebsiteNode Website { get; set; }

		public SiteMarkerNode(EntityReference reference)
			: base(reference, "adx_sitemarker", "adx_sitemarkerid")
		{
		}

		public SiteMarkerNode(Entity entity, string alias)
			: base(entity, alias, "adx_sitemarker", "adx_sitemarkerid")
		{
			WebPage = entity.GetEntityIdentifier<WebPageNode>("adx_pageid", alias);
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
		}
	}

	[Serializable]
	public class PageTemplateNode : EntityNode
	{
		public enum TemplateType
		{
			Rewrite = 756150000,
			WebTemplate = 756150001
		}

		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_pagetemplateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_isdefault", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_rewriteurl", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_type", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webtemplateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_usewebsiteheaderandfooter", BaseSolutionVersions.NaosAndOlderVersions)
		};

		public bool? IsDefault { get { return GetValue<bool?>("adx_isdefault"); } }
		public string RewriteUrl { get { return GetValue<string>("adx_rewriteurl"); } }
		public int? Type { get { return GetValue<int?>("adx_type"); } }
		public bool? UseWebsiteHeaderAndFooter { get { return GetValue<bool?>("adx_usewebsiteheaderandfooter"); } }
		public EntityReference WebTemplateId { get { return GetValue<EntityReference>("adx_webtemplateid"); } }

		public WebsiteNode Website { get; set; }

		public ICollection<WebPageNode> WebPages;

		public PageTemplateNode(EntityReference reference)
			: base(reference, "adx_pagetemplate", "adx_pagetemplateid")
		{
		}

		public PageTemplateNode(Entity entity, string alias)
			: base(entity, alias, "adx_pagetemplate", "adx_pagetemplateid")
		{
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);

			WebPages = new List<WebPageNode>();
		}
	}

	[Serializable]
	public class ContentSnippetNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_contentsnippetid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_createdbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyipaddress", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_modifiedbyusername", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_value", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_display_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_contentsnippetlanguageid", BaseSolutionVersions.CentaurusVersion)
		};

		public string CreatedByIPAddress { get { return GetValue<string>("adx_createdbyipaddress"); } }
		public string CreatedByUsername { get { return GetValue<string>("adx_createdbyusername"); } }
		public string ModifiedByIPAddress { get { return GetValue<string>("adx_modifiedbyipaddress"); } }
		public string ModifiedByUsername { get { return GetValue<string>("adx_modifiedbyusername"); } }
		public string Value { get { return GetValue<string>("adx_value"); } }

		public WebsiteLanguageNode Language { get; set; }
		public WebsiteNode Website { get; set; }

		public ContentSnippetNode(EntityReference reference)
			: base(reference, "adx_contentsnippet", "adx_contentsnippetid")
		{
		}

		public ContentSnippetNode(Entity entity, string alias)
			: base(entity, alias, "adx_contentsnippet", "adx_contentsnippetid")
		{
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
			Language = entity.GetEntityIdentifier<WebsiteLanguageNode>("adx_contentsnippetlanguageid", alias);
		}
	}

	[Serializable]
	public class IdeaForumNode : EntityNode
	{
		/// <summary>
		/// The column set for Ideas
		/// </summary>
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_ideaforumid", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", IdeaSolutionVersions.NaosAndOlderVersions)
		};

		/// <summary>
		/// Gets the state code.
		/// </summary>
		public int? StateCode
		{
			get
			{
				return this.GetValue<int?>("statecode");
			}
		}

		/// <summary>
		/// Gets the status code.
		/// </summary>
		public int? StatusCode
		{
			get
			{
				return this.GetValue<int?>("statuscode");
			}
		}

		/// <summary>
		/// Gets or sets the website.
		/// </summary>
		public WebsiteNode Website { get; set; }

		/// <summary>
		/// Gets the web role read intersect entities.
		/// </summary>
		public ICollection<IdeaForumReadToWebRoleNode> WebRoleReadIntersects { get; private set; }

		/// <summary>
		/// Gets the web roles read.
		/// </summary>
		public IEnumerable<WebRoleNode> WebRolesRead
		{
			get { return this.WebRoleReadIntersects.Select(rulerole => rulerole.WebRole); }
		}

		/// <summary>
		/// Gets the web role write intersect entities.
		/// </summary>
		public ICollection<IdeaForumWriteToWebRoleNode> WebRoleWriteIntersects { get; private set; }

		/// <summary>
		/// Gets the web roles write.
		/// </summary>
		public IEnumerable<WebRoleNode> WebRolesWrite
		{
			get { return this.WebRoleWriteIntersects.Select(rulerole => rulerole.WebRole); }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IdeaForumNode"/> class.
		/// </summary>
		/// <param name="reference">
		/// The reference.
		/// </param>
		public IdeaForumNode(EntityReference reference)
			: base(reference, "adx_ideaforum", "adx_ideaforumid")
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IdeaForumNode"/> class.
		/// </summary>
		/// <param name="entity">
		/// The entity.
		/// </param>
		/// <param name="alias">
		/// The alias.
		/// </param>
		public IdeaForumNode(Entity entity, string alias)
			: base(entity, alias, "adx_ideaforum", "adx_ideaforumid")
		{
			this.WebRoleReadIntersects = new List<IdeaForumReadToWebRoleNode>();
			this.WebRoleWriteIntersects = new List<IdeaForumWriteToWebRoleNode>();

			this.Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
		}

	}

	/// <summary>
	/// The idea forum read to web role node intersect entity.
	/// </summary>
	[Serializable]
	public class IdeaForumReadToWebRoleNode : EntityNode
	{
		/// <summary>
		/// The column set.
		/// </summary>
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_webrole_ideaforum_readid", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_ideaforumid", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webroleid", IdeaSolutionVersions.NaosAndOlderVersions),
		};

		/// <summary>
		/// overrides the name to null.
		/// </summary>
		public override string Name { get { return null; } }

		/// <summary>
		/// Gets or sets the idea.
		/// </summary>
		public IdeaForumNode Idea { get; set; }

		/// <summary>
		/// Gets or sets the web role.
		/// </summary>
		public WebRoleNode WebRole { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IdeaForumReadToWebRoleNode"/> class.
		/// </summary>
		/// <param name="reference">
		/// The reference.
		/// </param>
		public IdeaForumReadToWebRoleNode(EntityReference reference)
			: base(reference, "adx_webrole_ideaforum_read", "adx_webrole_ideaforum_readid", null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IdeaForumReadToWebRoleNode"/> class.
		/// </summary>
		/// <param name="entity">
		/// The entity.
		/// </param>
		/// <param name="alias">
		/// The alias.
		/// </param>
		public IdeaForumReadToWebRoleNode(Entity entity, string alias)
			: base(entity, alias, "adx_webrole_ideaforum_read", "adx_webrole_ideaforum_readid", null)
		{
			this.Idea = entity.GetIntersectEntityIdentifier<IdeaForumNode>("adx_ideaforum", "adx_ideaforumid", alias);
			this.WebRole = entity.GetIntersectEntityIdentifier<WebRoleNode>("adx_webrole", "adx_webroleid", alias);
		}
	}

	/// <summary>
	/// The idea forum write to web role node intersect entity.
	/// </summary>
	[Serializable]
	public class IdeaForumWriteToWebRoleNode : EntityNode
	{
		/// <summary>
		/// The column set.
		/// </summary>
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_webrole_ideaforum_writeid", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_ideaforumid", IdeaSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webroleid", IdeaSolutionVersions.NaosAndOlderVersions),
		};

		/// <summary>
		/// Overrides the name to Null.
		/// </summary>
		public override string Name { get { return null; } }

		/// <summary>
		/// Gets or sets the idea.
		/// </summary>
		public IdeaForumNode Idea { get; set; }

		/// <summary>
		/// Gets or sets the web role.
		/// </summary>
		public WebRoleNode WebRole { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IdeaForumWriteToWebRoleNode"/> class.
		/// </summary>
		/// <param name="reference">
		/// The reference.
		/// </param>
		public IdeaForumWriteToWebRoleNode(EntityReference reference)
			: base(reference, "adx_webrole_ideaforum_write", "adx_webrole_ideaforum_writeid", null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IdeaForumWriteToWebRoleNode"/> class.
		/// </summary>
		/// <param name="entity">
		/// The entity.
		/// </param>
		/// <param name="alias">
		/// The alias.
		/// </param>
		public IdeaForumWriteToWebRoleNode(Entity entity, string alias)
			: base(entity, alias, "adx_webrole_ideaforum_write", "adx_webrole_ideaforum_writeid", null)
		{
			this.Idea = entity.GetIntersectEntityIdentifier<IdeaForumNode>("adx_ideaforum", "adx_ideaforumid", alias);
			this.WebRole = entity.GetIntersectEntityIdentifier<WebRoleNode>("adx_webrole", "adx_webroleid", alias);
		}
	}

	[Serializable]
	public class ForumNode : EntityNode
	{
		/// <summary>
		/// The column set for Fourms
		/// </summary>
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_communityforumid", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_parentpageid", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_partialurl", ForumSolutionVersions.NaosAndOlderVersions), 
			new EntityNodeColumn("adx_websiteid", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websitelanguageid", ForumSolutionVersions.CentaurusVersion)
		};

		/// <summary>
		/// Gets or sets the parent page.
		/// </summary>
		public WebPageNode ParentPage { get; set; }

		/// <summary>
		/// Gets or sets the website.
		/// </summary>
		public WebsiteNode Website { get; set; }

		/// <summary>
		/// Gets the forum access permission.
		/// </summary>
		public ICollection<ForumAccessPermissionNode> ForumAccessPermissions { get; private set; }

		/// <summary>
		/// Get or set Forum Language
		/// </summary>
		public WebsiteLanguageNode WebsiteLanguage { get; set; }

		/// <summary>
		/// Gets the partial url.
		/// </summary>
		public string PartialUrl
		{
			get
			{
				return this.GetValue<string>("adx_partialurl");
			}
		}

		/// <summary>
		/// Gets the state code.
		/// </summary>
		public int? StateCode
		{
			get
			{
				return this.GetValue<int?>("statecode");
			}
		}

		/// <summary>
		/// Gets the status code.
		/// </summary>
		public int? StatusCode
		{
			get
			{
				return this.GetValue<int?>("statuscode");
			}
		}

		public PublishingStateNode PublishingState { get; set; }

		public ForumNode(EntityReference reference)
			: base(reference, "adx_communityforum", "adx_communityforumid")
		{

		}
		public ForumNode(Entity entity, string alias)
			: base(entity, alias, "adx_communityforum", "adx_communityforumid")
		{
			this.Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
			this.ParentPage = entity.GetEntityIdentifier<WebPageNode>("adx_parentpageid", alias);
			this.PublishingState = entity.GetEntityIdentifier<PublishingStateNode>("adx_publishingstateid", alias);
			this.ForumAccessPermissions = new List<ForumAccessPermissionNode>();
		}
	}

	[Serializable]
	public class ForumAccessPermissionNode : EntityNode
	{

		/// <summary>
		/// The column set for Fourms
		/// </summary>
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_communityforumaccesspermissionid", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_forumid", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_right", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statecode", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("statuscode", ForumSolutionVersions.NaosAndOlderVersions)
		};

		/// <summary>
		/// Gets the right.
		/// </summary>
		public RightOption Right
		{
			get
			{
				return this.GetValue<int>("adx_right").ToEnum<RightOption>();
			}
		}

		/// <summary>
		/// The right option.
		/// </summary>
		public enum RightOption
		{
			GrantChange = 1,
			RestrictRead = 2
		}

		/// <summary>
		/// Gets or sets the forum.
		/// </summary>
		public ForumNode Forum { get; set; }

		/// <summary>
		/// Gets the web role intersect entities.
		/// </summary>
		public ICollection<ForumAccessPermissionsToWebRoleNode> WebRoleIntersect { get; private set; }

		/// <summary>
		/// Gets the web roles.
		/// </summary>
		public IEnumerable<WebRoleNode> WebRoles
		{
			get { return this.WebRoleIntersect.Select(rulerole => rulerole.WebRole); }
		}

		/// <summary>
		/// Gets the state code.
		/// </summary>
		public int? StateCode
		{
			get
			{
				return this.GetValue<int?>("statecode");
			}
		}

		/// <summary>
		/// Gets the status code.
		/// </summary>
		public int? StatusCode
		{
			get
			{
				return this.GetValue<int?>("statuscode");
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ForumAccessPermissionNode"/> class.
		/// </summary>
		/// <param name="reference">
		/// The reference.
		/// </param>
		public ForumAccessPermissionNode(EntityReference reference)
			: base(reference, "adx_communityforumaccesspermission", "adx_communityforumaccesspermissionid")
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ForumAccessPermissionNode"/> class.
		/// </summary>
		/// <param name="entity">
		/// The entity.
		/// </param>
		/// <param name="alias">
		/// The alias.
		/// </param>
		public ForumAccessPermissionNode(Entity entity, string alias)
			: base(entity, alias, "adx_communityforumaccesspermission", "adx_communityforumaccesspermissionid")
		{
			this.Forum = entity.GetEntityIdentifier<ForumNode>("adx_forumid", alias);

			this.WebRoleIntersect = new List<ForumAccessPermissionsToWebRoleNode>();
		}

	}

	[Serializable]
	public class ForumAccessPermissionsToWebRoleNode : EntityNode
	{
		/// <summary>
		/// The column set.
		/// </summary>
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_communityforumaccesspermission_webroleid", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_communityforumaccesspermissionid", ForumSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webroleid", ForumSolutionVersions.NaosAndOlderVersions),
		};

		/// <summary>
		/// overrides the name to null.
		/// </summary>
		public override string Name { get { return null; } }

		/// <summary>
		/// Gets or sets the idea.
		/// </summary>
		public ForumAccessPermissionNode ForumAccessPermission { get; set; }

		/// <summary>
		/// Gets or sets the web role.
		/// </summary>
		public WebRoleNode WebRole { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ForumAccessPermissionsToWebRoleNode"/> class.
		/// </summary>
		/// <param name="reference">
		/// The reference.
		/// </param>
		public ForumAccessPermissionsToWebRoleNode(EntityReference reference)
			: base(reference, "adx_communityforumaccesspermission_webrole", "adx_communityforumaccesspermission_webroleid", null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ForumAccessPermissionsToWebRoleNode"/> class.
		/// </summary>
		/// <param name="entity">
		/// The entity.
		/// </param>
		/// <param name="alias">
		/// The alias.
		/// </param>
		public ForumAccessPermissionsToWebRoleNode(Entity entity, string alias)
			: base(entity, alias, "adx_communityforumaccesspermission_webrole", "adx_communityforumaccesspermission_webroleid", null)
		{
			this.ForumAccessPermission = entity.GetIntersectEntityIdentifier<ForumAccessPermissionNode>("adx_communityforumaccesspermission", "adx_communityforumaccesspermissionid", alias);
			this.WebRole = entity.GetIntersectEntityIdentifier<WebRoleNode>("adx_webrole", "adx_webroleid", alias);
		}
	}

	[Serializable]
	public class ShortcutNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_shortcutid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_description", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_disabletargetvalidation", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_displayorder", BaseSolutionVersions.NaosAndOlderVersions),
			//"adx_eventid",
			new EntityNodeColumn("adx_externalurl", BaseSolutionVersions.NaosAndOlderVersions),
			//"adx_forumid",
			new EntityNodeColumn("adx_parentpage_webpageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_title", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webfileid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webpageid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("modifiedon", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public string Description { get { return GetValue<string>("adx_description"); } }
		public bool? DisableTargetValidation { get { return GetValue<bool?>("adx_disabletargetvalidation"); } }
		public int? DisplayOrder { get { return GetValue<int?>("adx_displayorder"); } }
		public string ExternalUrl { get { return GetValue<string>("adx_externalurl"); } }
		public string Title { get { return GetValue<string>("adx_title"); } }
		public DateTime? ModifiedOn { get { return GetValue<DateTime?>("modifiedon"); } }

		public WebPageNode Parent { get; set; }
		//public EventNode Event { get; set; }
		//public ForumNode Forum { get; set; }
		public WebFileNode WebFile { get; set; }
		public WebPageNode WebPage { get; set; }
		public WebsiteNode Website { get; set; }

		public ShortcutNode(EntityReference reference)
			: base(reference, "adx_shortcut", "adx_shortcutid")
		{
		}

		public ShortcutNode(Entity entity, string alias)
			: base(entity, alias, "adx_shortcut", "adx_shortcutid")
		{
			Parent = entity.GetEntityIdentifier<WebPageNode>("adx_parentpage_webpageid", alias);
			//Event = entity.GetEntityIdentifier<EventNode>("adx_eventid", alias);
			//Forum = entity.GetEntityIdentifier<ForumNode>("adx_forumid", alias);
			WebFile = entity.GetEntityIdentifier<WebFileNode>("adx_webfileid", alias);
			WebPage = entity.GetEntityIdentifier<WebPageNode>("adx_webpageid", alias);
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
		}

		protected override JObject ToJson(JObject d)
		{
			d["Description"] = Description;
			d["DisableTargetValidation"] = DisableTargetValidation;
			d["DisplayOrder"] = DisplayOrder;
			d["ExternalUrl"] = ExternalUrl;
			d["Title"] = Title;
			d["ModifiedOn"] = ModifiedOn;

			return d;
		}
	}

	[Serializable]
	public class AnnotationNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("annotationid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("subject", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("createdon", BaseSolutionVersions.NaosAndOlderVersions),
			//"documentbody",
			new EntityNodeColumn("filename", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("filesize", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("isdocument", BaseSolutionVersions.NaosAndOlderVersions),
			//"langid",
			new EntityNodeColumn("mimetype", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("modifiedon", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("notetext", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("objectid", BaseSolutionVersions.NaosAndOlderVersions),
			//"objecttypecode",
		};

		public string Subject { get { return GetValue<string>("subject"); } }
		public DateTime? CreatedOn { get { return GetValue<DateTime?>("createdon"); } }
		//public string DocumentBody { get { return GetValue<string>("documentbody"); } }
		public string FileName { get { return GetValue<string>("filename"); } }
		public int? FileSize { get { return GetValue<int?>("filesize"); } }
		public bool? IsDocument { get { return GetValue<bool?>("isdocument"); } }
		public string MimeType { get { return GetValue<string>("mimetype"); } }
		public DateTime? ModifiedOn { get { return GetValue<DateTime?>("modifiedon"); } }
		public string NoteText { get { return GetValue<string>("notetext"); } }

		public WebFileNode WebFile { get; set; }

		public AnnotationNode(EntityReference reference)
			: base(reference, "annotation", "annotationid", "subject")
		{
		}

		public AnnotationNode(Entity entity, string alias)
			: base(entity, alias, "annotation", "annotationid", "subject")
		{
			var regarding = entity.GetAttributeValue<EntityReference>("objectid");

			if (regarding == null || regarding.LogicalName != "adx_webfile")
			{
				return;
			}

			WebFile = entity.GetEntityIdentifier<WebFileNode>("objectid", alias);
		}
	}

	[Serializable]
	public class WebPageAccessControlRuleToPublishingStateNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_accesscontrolrule_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webpageaccesscontrolruleid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_publishingstateid", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public override string Name { get { return null; } }
		public WebPageAccessControlRuleNode WebPageAccessControlRule { get; set; }
		public PublishingStateNode PublishingState { get; set; }

		public WebPageAccessControlRuleToPublishingStateNode(EntityReference reference)
			: base(reference, "adx_accesscontrolrule_publishingstate", "adx_accesscontrolrule_publishingstateid", null)
		{
		}

		public WebPageAccessControlRuleToPublishingStateNode(Entity entity, string alias)
			: base(entity, alias, "adx_accesscontrolrule_publishingstate", "adx_accesscontrolrule_publishingstateid", null)
		{
			WebPageAccessControlRule = entity.GetIntersectEntityIdentifier<WebPageAccessControlRuleNode>("adx_webpageaccesscontrolrule", "adx_webpageaccesscontrolruleid", alias);
			PublishingState = entity.GetIntersectEntityIdentifier<PublishingStateNode>("adx_publishingstate", "adx_publishingstateid", alias);
		}
	}

	[Serializable]
	public class WebPageAccessControlRuleToWebRoleNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_webpageaccesscontrolrule_webroleid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webpageaccesscontrolruleid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webroleid", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public override string Name { get { return null; } }
		public WebPageAccessControlRuleNode WebPageAccessControlRule { get; set; }
		public WebRoleNode WebRole { get; set; }

		public WebPageAccessControlRuleToWebRoleNode(EntityReference reference)
			: base(reference, "adx_webpageaccesscontrolrule_webrole", "adx_webpageaccesscontrolrule_webroleid", null)
		{
		}

		public WebPageAccessControlRuleToWebRoleNode(Entity entity, string alias)
			: base(entity, alias, "adx_webpageaccesscontrolrule_webrole", "adx_webpageaccesscontrolrule_webroleid", null)
		{
			WebPageAccessControlRule = entity.GetIntersectEntityIdentifier<WebPageAccessControlRuleNode>("adx_webpageaccesscontrolrule", "adx_webpageaccesscontrolruleid", alias);
			WebRole = entity.GetIntersectEntityIdentifier<WebRoleNode>("adx_webrole", "adx_webroleid", alias);
		}
	}

	[Serializable]
	public class WebsiteAccessToWebRoleNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_websiteaccess_webroleid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteaccessid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webroleid", BaseSolutionVersions.NaosAndOlderVersions),
		};

		public override string Name { get { return null; } }
		public WebsiteAccessNode WebsiteAccess { get; set; }
		public WebRoleNode WebRole { get; set; }

		public WebsiteAccessToWebRoleNode(EntityReference reference)
			: base(reference, "adx_websiteaccess_webrole", "adx_websiteaccess_webroleid", null)
		{
		}

		public WebsiteAccessToWebRoleNode(Entity entity, string alias)
			: base(entity, alias, "adx_websiteaccess_webrole", "adx_websiteaccess_webroleid", null)
		{
			WebsiteAccess = entity.GetIntersectEntityIdentifier<WebsiteAccessNode>("adx_websiteaccess", "adx_websiteaccessid", alias);
			WebRole = entity.GetIntersectEntityIdentifier<WebRoleNode>("adx_webrole", "adx_webroleid", alias);
		}
	}

	[Serializable]
	public class PortalLanguageNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_portallanguageid", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_lcid", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_displayname", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_description", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_systemlanguage", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_languagecode", BaseSolutionVersions.CentaurusVersion)
		};

		public int? Lcid { get { return GetValue<int?>("adx_lcid"); } }
		public string DisplayName { get { return GetValue<string>("adx_displayname"); } }
		public string Description { get { return GetValue<string>("adx_description"); } }
		public int? CrmLanguage { get { return GetValue<int?>("adx_systemlanguage"); } }
		public string Code { get { return GetValue<string>("adx_languagecode"); } }

		public PortalLanguageNode(Entity entity, string alias)
			: base(entity, alias, "adx_portallanguage", "adx_portallanguageid")
		{
		}

		public PortalLanguageNode(EntityReference reference)
			: base(reference, "adx_portallanguage", "adx_portallanguageid")
		{
		}
	}

	[Serializable]
	public class WebsiteLanguageNode : EntityNode
	{
		public static readonly EntityNodeColumn[] ColumnSet = new[]
		{
			new EntityNodeColumn("adx_websitelanguageid", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_publishingstate", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_portallanguageid", BaseSolutionVersions.CentaurusVersion),
			new EntityNodeColumn("adx_name", BaseSolutionVersions.CentaurusVersion)
		};

		public WebsiteNode Website { get; set; }
		public PublishingStateNode PublishingState { get; set; }
		public PortalLanguageNode PortalLanguage { get; set; }
		public ICollection<ContentSnippetNode> Snippets { get; private set; }
		public ICollection<ForumNode> Forums { get; private set; }

		public WebsiteLanguageNode(Entity entity, string alias)
			: base(entity, alias, "adx_websitelanguage", "adx_websitelanguageid")
		{
			Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
			PublishingState = entity.GetEntityIdentifier<PublishingStateNode>("adx_publishingstate", alias);
			PortalLanguage = entity.GetEntityIdentifier<PortalLanguageNode>("adx_portallanguageid", alias);
			Snippets = new List<ContentSnippetNode>();
		}

		public WebsiteLanguageNode(EntityReference reference)
			: base(reference, "adx_websitelanguage", "adx_websitelanguageid")
		{
		}
	}

	[Serializable]
	public class BlogNode : EntityNode
	{
		private static readonly EntityNodeColumn[] columnSet = new[]
		{
			new EntityNodeColumn("adx_blogid", BaseSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_websiteid", BaseSolutionVersions.NaosAndOlderVersions)
		};

		public static EntityNodeColumn[] ColumnSet
		{
			get { return (EntityNodeColumn[])columnSet.Clone(); }
		}

		public WebsiteNode Website { get; set; }

		public ICollection<BlogToWebRoleNode> WebRoleIntersect { get; private set; }

		public IEnumerable<WebRoleNode> WebRoles
		{
			get { return this.WebRoleIntersect.Select(rulerole => rulerole.WebRole); }
		}

		public BlogNode(Entity entity, string alias)
			: base(entity, alias, "adx_blog", "adx_blogid")
		{
			this.Website = entity.GetEntityIdentifier<WebsiteNode>("adx_websiteid", alias);
			this.WebRoleIntersect = new List<BlogToWebRoleNode>();
		}

		public BlogNode(EntityReference reference)
			: base(reference, "adx_blog", "adx_blogid")
		{
		}
	}

	[Serializable]
	public class BlogToWebRoleNode : EntityNode
	{
		private static readonly EntityNodeColumn[] columnSet = new[]
		{
			new EntityNodeColumn("adx_blog_webroleid", BlogSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_blogid", BlogSolutionVersions.NaosAndOlderVersions),
			new EntityNodeColumn("adx_webroleid", BlogSolutionVersions.NaosAndOlderVersions),
		};

		public static EntityNodeColumn[] ColumnSet
		{
			get { return (EntityNodeColumn[])columnSet.Clone(); }
		}

		public override string Name { get { return null; } }

		public BlogNode Blog { get; set; }

		public WebRoleNode WebRole { get; set; }

		public BlogToWebRoleNode(EntityReference reference)
			: base(reference, "adx_blog_webrole", "adx_blog_webroleid", null)
		{
		}

		public BlogToWebRoleNode(Entity entity, string alias)
			: base(entity, alias, "adx_blog_webrole", "adx_blog_webroleid", null)
		{
			this.Blog = entity.GetIntersectEntityIdentifier<BlogNode>("adx_blog", "adx_blogid", alias);
			this.WebRole = entity.GetIntersectEntityIdentifier<WebRoleNode>("adx_webrole", "adx_webroleid", alias);
		}
	}
}
