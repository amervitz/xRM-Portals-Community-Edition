/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Configuration;
using Adxstudio.Xrm.Caching;
using Adxstudio.Xrm.Search.Configuration;
using Adxstudio.Xrm.Web.Handlers;
using Microsoft.Xrm.Client.Configuration;

namespace Adxstudio.Xrm.Configuration
{
	/// <summary>
	/// Represents a collection of site configuration nodes and settings for all sites in general.
	/// </summary>
	public class CrmSection : ConfigurationSection
	{
		private const string _defaultConfigurationProviderType = "Adxstudio.Xrm.Configuration.AdxstudioCrmConfigurationProvider, Adxstudio.Xrm";

		/// <summary>
		/// The element name of the section.
		/// </summary>
		public const string SectionName = "adxstudio.xrm";
		private const string _defaultOutputObjectCacheName = SectionName;

		private static readonly ConfigurationProperty _propConfigurationProviderType;
		private static readonly ConfigurationProperty _propContentLicensesEnabled;
		private static readonly ConfigurationProperty _propCacheFeed;
		private static readonly ConfigurationProperty _propEvents;
		private static readonly ConfigurationProperty _propSearch;
		private static readonly ConfigurationProperty _propOutputObjectCacheName;
		private static readonly ConfigurationProperty _propCdnEnabled;
		private static readonly ConfigurationProperty _propContentMap;
		private static readonly ConfigurationPropertyCollection _properties;

		static CrmSection()
		{
			_propConfigurationProviderType = new ConfigurationProperty("sectionProviderType", typeof(string), _defaultConfigurationProviderType, ConfigurationPropertyOptions.None);
			_propContentLicensesEnabled = new ConfigurationProperty("contentLicensesEnabled", typeof(bool), true, ConfigurationPropertyOptions.None);
			_propCacheFeed = new ConfigurationProperty("cacheFeed", typeof(CacheFeedElement), new CacheFeedElement(), ConfigurationPropertyOptions.None);
			_propEvents = new ConfigurationProperty(OrganizationServiceEventElementCollection.Name, typeof(OrganizationServiceEventElementCollection), new OrganizationServiceEventElementCollection(), ConfigurationPropertyOptions.None);
			_propSearch = new ConfigurationProperty("search", typeof(SearchElement), new SearchElement(), ConfigurationPropertyOptions.None);
			_propOutputObjectCacheName = new ConfigurationProperty("outputObjectCacheName", typeof(string), _defaultOutputObjectCacheName, ConfigurationPropertyOptions.None);
			_propCdnEnabled = new ConfigurationProperty("cdnEnabled", typeof(bool), true, ConfigurationPropertyOptions.None);
			_propContentMap = new ConfigurationProperty("contentMap", typeof(ContentMapElement), new ContentMapElement(), ConfigurationPropertyOptions.None);

			_properties = new ConfigurationPropertyCollection
			{
				_propConfigurationProviderType,
				_propContentLicensesEnabled,
				_propCacheFeed,
				_propEvents,
				_propSearch,
				_propOutputObjectCacheName,
				_propCdnEnabled,
				_propContentMap,
			};
		}

		protected override ConfigurationPropertyCollection Properties
		{
			get { return _properties; }
		}

		public override bool IsReadOnly()
		{
			return false;
		}

		protected override void Reset(ConfigurationElement parentElement)
		{
			base.Reset(parentElement);
			AdxstudioCrmConfigurationManager.Reset();
		}

		/// <summary>
		/// The configuration provider type name.
		/// </summary>
		[ConfigurationProperty("sectionProviderType", DefaultValue = _defaultConfigurationProviderType)]
		public string ConfigurationProviderType
		{
			get { return (string)base[_propConfigurationProviderType]; }
			set { base[_propConfigurationProviderType] = value; }
		}

		/// <summary>
		/// Enables or disables loading of licenses that are stored in the CRM.
		/// </summary>
		[ConfigurationProperty("contentLicensesEnabled", DefaultValue = true)]
		public bool ContentLicensesEnabled
		{
			get { return (bool)base[_propContentLicensesEnabled]; }
			set { base[_propContentLicensesEnabled] = value; }
		}

		/// <summary>
		/// The <see cref="CacheFeedHandler"/> configuration element.
		/// </summary>
		[ConfigurationProperty("cacheFeed")]
		public CacheFeedElement CacheFeed
		{
			get { return (CacheFeedElement)base[_propCacheFeed]; }
			set { base[_propCacheFeed] = value; }
		}

		/// <summary>
		/// A collection of <see cref="OrganizationServiceEventElement"/>.
		/// </summary>
		[ConfigurationProperty(OrganizationServiceEventElementCollection.Name, IsDefaultCollection = false)]
		public OrganizationServiceEventElementCollection Events
		{
			get { return (OrganizationServiceEventElementCollection)base[_propEvents]; }
			set { base[_propEvents] = value; }
		}

		/// <summary>
		/// The search configuration element.
		/// </summary>
		[ConfigurationProperty("search", IsDefaultCollection = false)]
		public SearchElement Search
		{
			get { return (SearchElement)base[_propSearch]; }
			set { base[_propSearch] = value; }
		}

		/// <summary>
		/// The name of the <see cref="OutputObjectCache"/> (specified by an <see cref="ObjectCacheElement"/>) that the
		/// <see cref="ObjectCacheOutputCacheProvider"/> should manage. Leave as the default value in order to manage all available
		/// <see cref="OutputObjectCache"/> objects.
		/// </summary>
		[ConfigurationProperty("outputObjectCacheName", DefaultValue = _defaultOutputObjectCacheName)]
		public string OutputObjectCacheName
		{
			get { return (string)base[_propOutputObjectCacheName]; }
			set { base[_propOutputObjectCacheName] = value; }
		}

		/// <summary>
		/// Enables or disables Windows Azure CDN URL routing rules.
		/// </summary>
		[ConfigurationProperty("cdnEnabled", DefaultValue = true)]
		public bool CdnEnabled
		{
			get { return (bool)base[_propCdnEnabled]; }
			set { base[_propCdnEnabled] = value; }
		}

		/// <summary>
		/// The content map configuration element.
		/// </summary>
		[ConfigurationProperty("contentMap")]
		public ContentMapElement ContentMap
		{
			get { return (ContentMapElement)base[_propContentMap]; }
			set { base[_propContentMap] = value; }
		}
	}
}
