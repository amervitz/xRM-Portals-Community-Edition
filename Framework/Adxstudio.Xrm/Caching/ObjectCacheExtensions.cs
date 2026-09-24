/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Caching
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Runtime.Caching;
	using System.ServiceModel.Syndication;
	using System.Xml;
	using System.Xml.Linq;
	using Adxstudio.Xrm.Collections.Generic;
	using Adxstudio.Xrm.Json;
	using Adxstudio.Xrm.ServiceModel;
	using Adxstudio.Xrm.Services;
	using Microsoft.Xrm.Client.Caching;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Client.Runtime.Serialization;
	using Microsoft.Xrm.Client.Services;
	using Microsoft.Xrm.Sdk.Messages;
	using Newtonsoft.Json.Linq;

	/// <summary>
	/// Helper methods on the <see cref="ObjectCache"/> class.
	/// </summary>
	public static class ObjectCacheExtensions
	{
		/// <summary>
		/// Increment cache item access count.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="cacheKey">The cache item key.</param>
		/// <param name="regionName">The cache region.</param>
		public static void IncrementAccessCount(this ObjectCache cache, string cacheKey, string regionName = null)
		{
			var item = GetCacheItemTelemetry(cache, cacheKey, regionName);

			if (item != null)
			{
				item.IncrementAccessCount();
			}
		}

		/// <summary>
		/// Increment cache item stale access count.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="cacheKey">The cache item key.</param>
		/// <param name="regionName">The cache region.</param>
		public static void IncrementStaleAccessCount(this ObjectCache cache, string cacheKey, string regionName = null)
		{
			var item = GetCacheItemTelemetry(cache, cacheKey, regionName);

			item?.IncrementStaleAccessCount();
		}

		/// <summary>
		/// Insert a new cache item telemetry cache item for an organization request.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="cacheKey">The cache item key.</param>
		/// <param name="telemetry">The telemetry.</param>
		/// <param name="regionName">The cache region.</param>
		internal static void AddCacheItemTelemetry(this ObjectCache cache, string cacheKey, CacheItemTelemetry telemetry, string regionName = null)
		{
			var key = cacheKey.ToLower();
			var policy = new CacheItemPolicy();
			policy.ChangeMonitors.Add(cache.CreateCacheEntryChangeMonitor(new[] { key }, regionName));
			var itemTelemetryCacheKey = GetItemTelemetryCacheKey(key);
			cache.Add(itemTelemetryCacheKey, telemetry, policy, regionName);
		}

		private static string GetItemTelemetryCacheKey(string cacheKey)
		{
			return string.Format("{0}:{1}", typeof(CacheItemTelemetry).ToString().ToLower(), cacheKey.ToLower());
		}

		private static CacheItemTelemetry GetCacheItemTelemetry(ObjectCache cache, string cacheKey, string regionName)
		{
			var itemTelemetryCacheKey = GetItemTelemetryCacheKey(cacheKey);
			return cache.Get(itemTelemetryCacheKey, regionName) as CacheItemTelemetry;
		}

		#region JSON

		/// <summary>
		/// Retrieves a feed of cache items.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="title"></param>
		/// <param name="description"></param>
		/// <param name="feedAlternateLink"></param>
		/// <param name="itemAlternateLink"></param>
		/// <param name="showAll"></param>
		/// <param name="regionName"></param>
		/// <param name="expanded"></param>
		/// <returns></returns>
		public static JObject GetJson(
			this ObjectCache cache,
			string title = null,
			string description = null,
			Uri feedAlternateLink = null,
			Uri itemAlternateLink = null,
			bool showAll = false,
			string regionName = null,
			bool expanded = false)
		{
			Func<string, object, bool> filter = (key, value) => showAll || !(value is CacheItemDetail || value is CacheItemTelemetry);
			return cache.GetJson(filter, detail => detail.CacheKey, title, description, feedAlternateLink, itemAlternateLink, regionName, expanded);
		}

		/// <summary>
		/// Retrieves a feed of cache items.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="cacheKey"></param>
		/// <param name="title"></param>
		/// <param name="description"></param>
		/// <param name="feedAlternateLink"></param>
		/// <param name="itemAlternateLink"></param>
		/// <param name="showAll"></param>
		/// <param name="regionName"></param>
		/// <param name="expanded"></param>
		/// <returns></returns>
		public static JObject GetJson(
			this ObjectCache cache,
			string cacheKey,
			string title = null,
			string description = null,
			Uri feedAlternateLink = null,
			Uri itemAlternateLink = null,
			bool showAll = false,
			string regionName = null,
			bool expanded = false)
		{
			Func<string, object, bool> filter = (key, value) => key == cacheKey;
			return cache.GetJson(filter, detail => detail.CacheKey, title, description, feedAlternateLink, itemAlternateLink, regionName, expanded);
		}

		/// <summary>
		/// Retrieves a feed of cache items in the form of a serializable object.
		/// </summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="cache"></param>
		/// <param name="filter"></param>
		/// <param name="orderBy"></param>
		/// <param name="title"></param>
		/// <param name="description"></param>
		/// <param name="feedAlternateLink"></param>
		/// <param name="itemAlternateLink"></param>
		/// <param name="regionName"></param>
		/// <param name="expanded"></param>
		/// <returns></returns>
		public static JObject GetJson<TKey>(
			this ObjectCache cache,
			Func<string, object, bool> filter,
			Func<CacheItemDetail, TKey> orderBy,
			string title = null,
			string description = null,
			Uri feedAlternateLink = null,
			Uri itemAlternateLink = null,
			string regionName = null,
			bool expanded = false)
		{
			var objectCacheElement = new JObject
			{
				{ "type", cache.ToString() },
				{ "count", cache.GetCount(regionName).ToString() },
				{ "defaultCacheCapabilities", cache.DefaultCacheCapabilities.ToString() }
			};

			var compositeCache = cache as CompositeObjectCache;

			while (compositeCache != null && compositeCache.Cache is CompositeObjectCache)
			{
				compositeCache = compositeCache.Cache as CompositeObjectCache;
			}

			var memoryCache = compositeCache != null ? compositeCache.Cache as MemoryCache : cache as MemoryCache;

			var memoryCacheElement = memoryCache != null
				? new JObject
				{
					{ "cacheMemoryLimit", memoryCache.CacheMemoryLimit.ToString() },
					{ "physicalMemoryLimit", memoryCache.PhysicalMemoryLimit.ToString() },
					{ "pollingInterval", memoryCache.PollingInterval.ToString() }
				}
				: null;

			var items = GetJsonItems(cache, filter, orderBy, itemAlternateLink, regionName, expanded);

			var retval = CreateSerializableFeed(title ?? cache.Name, description, feedAlternateLink, objectCacheElement, memoryCacheElement, items);

			return retval;
		}

		public static JObject CreateSerializableFeed(string title, string description, Uri link, JObject objectCache, JObject memoryCache, JArray items)
		{
			var retval = new JObject
			{
				{ "title", title ?? string.Empty },
				{ "description", description ?? string.Empty },
				{ "link", link == null ? string.Empty : link.AbsoluteUri },
				{ "objectCache", objectCache },
				{ "memoryCache", memoryCache },
				{ "items", items }
			};

			return retval;
		}

		/// <summary>
		/// Get the cache footprint in the form of a JSON-serializable object.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="expanded">Whether to show details of each entity item.</param>
		/// <param name="requestUrl">The request URL.</param>
		/// <returns></returns>
		public static Dictionary<string, object> GetCacheFootprintJson(this ObjectCache cache, bool expanded, Uri requestUrl)
		{
			var alternateLink = new Uri(requestUrl.GetLeftPart(UriPartial.Path));

			var entities = new List<Dictionary<string, object>>();
			
			var footprint = GetCacheFootprint(cache, alternateLink);
			foreach (var entityType in footprint)
			{
				var entity = new Dictionary<string, object>
				{
					{ "Name", entityType.Name },
					{ "Count", entityType.GetCount() },
					{ "Size", entityType.GetSize() }
				};

				if (expanded)
				{
					var records = new Collection<Dictionary<string, object>>();
					foreach (var item in entityType.Items)
					{
						var record = new Dictionary<string, object>
						{
							{ "LogicalName", item.Entity.LogicalName },
							{ "Name", item.Entity.GetAttributeValueOrDefault("adx_name", string.Empty) },
							{ "Id", item.Entity.Id }
						};

						var recordCache = new Dictionary<string, object>
						{
							{ "Id", item.CacheItemKey },
							{ "Type", item.CacheItemType },
							{ "Link", item.Link.ToString() },
							{ "Size", GetEntitySizeInMemory(item.Entity) }
						};

						record.Add("Cache", recordCache);
						records.Add(record);
					}
					entity.Add("Items", records);
				}

				entities.Add(entity);
			}

			var entitiesWrapper = new Dictionary<string, object>();
			if (!expanded)
			{
				// Add link to the Expanded view with entity record details
				var query = System.Web.HttpUtility.ParseQueryString(requestUrl.Query);
				query[Web.Handlers.CacheFeedHandler.QueryKeys.Expanded] = bool.TrueString;
				var uriBuilder = new UriBuilder(requestUrl.ToString()) { Query = query.ToString() };
				entitiesWrapper.Add("ExpandedView", uriBuilder.ToString());
			}
			entitiesWrapper.Add("Entities", entities.OrderByDescending(e => e["Size"]));
			var retval = new Dictionary<string, object> { { "CacheFootprint", entitiesWrapper } };
			return retval;
		}

		private static JArray GetJsonItems<TKey>(
			ObjectCache cache,
			Func<string, object, bool> filter,
			Func<CacheItemDetail, TKey> orderBy,
			Uri itemAlternateLink = null,
			string regionName = null,
			bool expanded = false)
		{
			var rawItems = 
				from item in cache
				where filter(item.Key, item.Value)
				let detail = cache.GetCacheItemDetail(item.Key, regionName)
				let defaultDetail = detail ?? new CacheItemDetail(item.Key, new CacheItemPolicy()) { UpdatedOn = default(DateTimeOffset) }
				let telemetry = GetCacheItemTelemetry(cache, item.Key, regionName)
				let updatedOn = defaultDetail.UpdatedOn
				let link = ApplyKey(itemAlternateLink, "key", item.Key, cache.Name)
				let remove = ApplyKey(itemAlternateLink, "remove", item.Key, cache.Name)
				orderby orderBy(defaultDetail)
				select new { item, detail, telemetry, updatedOn, link, remove };

			var content = rawItems.Select(
				rawItem => new JObject
				{
					{ "id", rawItem.item.Key },
					{ "title", rawItem.item.Key },
					{ "updated", rawItem.updatedOn },
					{ "link", rawItem.link.AbsoluteUri },
					{ "content", GetJsonContent(cache.Name, rawItem.detail, rawItem.telemetry, rawItem.item.Value, rawItem.remove, expanded) }
				});

			var items = new JArray(content);

			return items;
		}

		private static JObject GetJsonContent(string name, CacheItemDetail detail, CacheItemTelemetry telemetry, object value, Uri removeLink, bool expanded)
		{
			var policyContent = GetJsonPolicyContent(detail);
			var telemetryContent = telemetry != null
				? new JObject
				{
					{ "memberName", telemetry.Caller.MemberName },
					{ "sourceFilePath", telemetry.Caller.SourceFilePath },
					{ "sourceLineNumber", telemetry.Caller.SourceLineNumber },
					{ "duration", telemetry.Duration },
					{ "accessCount", telemetry.AccessCount },
					{ "cacheItemStatus", detail.CacheItemStatus.ToString() },
					{ "staleAccessCount", telemetry.StaleAccessCount },
					{ "lastAccessedOn", telemetry.LastAccessedOn },
					{ "isStartup", telemetry.IsStartup },
					{ "isAllColumns", telemetry.IsAllColumns },
					{ "attributes", new JArray(telemetry.Attributes) }
				}
				: null;

			var properties = new JObject
			{
				{ "name", name },
				{ "type", value != null ? value.GetType().ToString() : null },
				{ "remove", removeLink.AbsoluteUri }
			};

			if (policyContent != null)
			{
				properties.Add("policy", policyContent);
			}

			if (telemetryContent != null)
			{
				properties.Add("telemetry", telemetryContent);
			}

			if (expanded)
			{
				if (value != null)
				{
					properties.Add("value", JToken.FromObject(value, CrmJsonConvert.CreateJsonSerializer()));
				}

				if (telemetry != null && telemetry.Request != null)
				{
					properties.Add("request", JToken.FromObject(telemetry.Request, CrmJsonConvert.CreateJsonSerializer()));
				}
			}

			var content = new JObject
			{
				{ "properties", properties }
			};

			return content;
		}
		
		private static JObject GetJsonPolicyContent(CacheItemDetail detail)
		{
			if (detail != null)
			{
				var policyDetail = detail.Policy;

				if (policyDetail != null)
				{
					var policy = new JObject
					{
						{ "isStaleDataAllowed", detail.IsStaleDataAllowed },
						{ "absoluteExpiration", policyDetail.AbsoluteExpiration.UtcDateTime },
						{ "slidingExpiration", policyDetail.SlidingExpiration },
						{ "priority", policyDetail.Priority.ToString() },
						{ "changeMonitors", new JArray(policyDetail.ChangeMonitors.SelectMany(cm => cm.CacheKeys)) }
					};

					return policy;
				}
			}

			return null;
		}

		#endregion

		#region SyndicationFeed

		private static readonly string _prefix = "adx";
		private static readonly XNamespace _namespace = Namespaces.Default;

		/// <summary>
		/// Retrieves a feed of cache items.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="title"></param>
		/// <param name="description"></param>
		/// <param name="feedAlternateLink"></param>
		/// <param name="itemAlternateLink"></param>
		/// <param name="showAll"></param>
		/// <param name="regionName"></param>
		/// <param name="expanded"></param>
		/// <returns></returns>
		public static SyndicationFeed GetFeed(
			this ObjectCache cache,
			string title = null,
			string description = null,
			Uri feedAlternateLink = null,
			Uri itemAlternateLink = null,
			bool showAll = false,
			string regionName = null,
			bool expanded = false)
		{
			Func<string, object, bool> filter = (key, value) => showAll || !(value is CacheItemDetail || value is CacheItemTelemetry);

			return cache.GetFeed(filter, detail => detail.CacheKey, title, description, feedAlternateLink, itemAlternateLink, regionName, expanded);
		}

		/// <summary>
		/// Retrieves a feed of cache items.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="cacheKey"></param>
		/// <param name="title"></param>
		/// <param name="description"></param>
		/// <param name="feedAlternateLink"></param>
		/// <param name="itemAlternateLink"></param>
		/// <param name="showAll"></param>
		/// <param name="regionName"></param>
		/// <param name="expanded"></param>
		/// <returns></returns>
		public static SyndicationFeed GetFeed(
			this ObjectCache cache,
			string cacheKey,
			string title = null,
			string description = null,
			Uri feedAlternateLink = null,
			Uri itemAlternateLink = null,
			bool showAll = false,
			string regionName = null,
			bool expanded = false)
		{
			Func<string, object, bool> filter = (key, value) => key == cacheKey;

			return cache.GetFeed(filter, detail => detail.CacheKey, title, description, feedAlternateLink, itemAlternateLink, regionName, expanded);
		}

		/// <summary>
		/// Retrieves a feed of cache items.
		/// </summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="cache"></param>
		/// <param name="filter"></param>
		/// <param name="orderBy"></param>
		/// <param name="title"></param>
		/// <param name="description"></param>
		/// <param name="feedAlternateLink"></param>
		/// <param name="itemAlternateLink"></param>
		/// <param name="regionName"></param>
		/// <param name="expanded"></param>
		/// <returns></returns>
		public static SyndicationFeed GetFeed<TKey>(
			this ObjectCache cache,
			Func<string, object, bool> filter,
			Func<CacheItemDetail, TKey> orderBy,
			string title = null,
			string description = null,
			Uri feedAlternateLink = null,
			Uri itemAlternateLink = null,
			string regionName = null,
			bool expanded = false)
		{
			var feed = new SyndicationFeed(
				title ?? cache.Name,
				description,
				feedAlternateLink,
				GetFeedItems(cache, filter, orderBy, itemAlternateLink, regionName, expanded));

			feed.AttributeExtensions.Add(new XmlQualifiedName(_prefix, XNamespace.Xmlns.ToString()), _namespace.ToString());

			feed.ElementExtensions.Add(new XElement(_namespace + "objectCache",
				new XAttribute("type", cache.ToString()),
				new XAttribute("count", cache.GetCount(regionName)),
				new XAttribute("defaultCacheCapabilities", cache.DefaultCacheCapabilities)));

			var compositeCache = cache as CompositeObjectCache;

			while (compositeCache != null && compositeCache.Cache is CompositeObjectCache)
			{
				compositeCache = compositeCache.Cache as CompositeObjectCache;
			}

			var memoryCache = compositeCache != null ? compositeCache.Cache as MemoryCache : cache as MemoryCache;

			if (memoryCache != null)
			{
				feed.ElementExtensions.Add(new XElement(_namespace + "memoryCache",
					new XAttribute("cacheMemoryLimit", memoryCache.CacheMemoryLimit),
					new XAttribute("physicalMemoryLimit", memoryCache.PhysicalMemoryLimit),
					new XAttribute("pollingInterval", memoryCache.PollingInterval.ToString())));
			}

			return feed;
		}

		/// <summary>
		/// Get the cache footprint in XML.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="expanded">Whether to show details of each entity item.</param>
		/// <param name="requestUrl">The request URL.</param>
		/// <returns></returns>
		public static XmlDocument GetCacheFootprintXml(this ObjectCache cache, bool expanded, Uri requestUrl)
		{
			var alternateLink = new Uri(requestUrl.GetLeftPart(UriPartial.Path));

			var doc = new XmlDocument();
			var rootElement = doc.CreateElement("CacheFootprint");
			var entityElements = new List<XmlElement>();
			var footprint = GetCacheFootprint(cache, alternateLink);
			foreach (var entityType in footprint)
			{
				var entityElement = doc.CreateElement("Entity");
				entityElement.SetAttribute("Name", entityType.Name);
				entityElement.SetAttribute("Count", entityType.GetCount().ToString());
				entityElement.SetAttribute("Size", entityType.GetSize().ToString());

				if (expanded)
				{
					foreach (var item in entityType.Items)
					{
						var itemElement = doc.CreateElement("Item");
						itemElement.SetAttribute("LogicalName", item.Entity.LogicalName);
						itemElement.SetAttribute("Name", item.Entity.GetAttributeValueOrDefault("adx_name", string.Empty));
						itemElement.SetAttribute("Id", item.Entity.Id.ToString());

						var cacheElement = doc.CreateElement("Cache");
						cacheElement.SetAttribute("Id", item.CacheItemKey);
						cacheElement.SetAttribute("Type", item.CacheItemType.ToString());
						cacheElement.SetAttribute("Link", item.Link.ToString());
						cacheElement.SetAttribute("Size", GetEntitySizeInMemory(item.Entity).ToString());

						itemElement.AppendChild(cacheElement);
						entityElement.AppendChild(itemElement);
					}
				}

				entityElements.Add(entityElement);
			}

			// Sort the entities by descending size
			entityElements = entityElements.OrderByDescending(el => int.Parse(el.GetAttribute("Size"))).ToList();

			var entitiesElement = doc.CreateElement("Entities");
			foreach (var entityElement in entityElements)
			{
				entitiesElement.AppendChild(entityElement);
			}
			
			if (!expanded)
			{
				// Add link to the Expanded view with entity record details
				var query = System.Web.HttpUtility.ParseQueryString(requestUrl.Query);
				query[Web.Handlers.CacheFeedHandler.QueryKeys.Expanded] = bool.TrueString;
				var uriBuilder = new UriBuilder(requestUrl.ToString()) { Query = query.ToString() };
				var expandedView = doc.CreateElement("expandedView");
				expandedView.InnerText = uriBuilder.ToString();
				rootElement.AppendChild(expandedView);
			}
			rootElement.AppendChild(entitiesElement);
			doc.AppendChild(rootElement);
			return doc;
		}

		/// <summary>
		/// Gets the cache footprint per entity.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="alternateLink"></param>
		/// <returns></returns>
		private static List<FootprintEntity> GetCacheFootprint(this ObjectCache cache, Uri alternateLink)
		{
			var retval = new List<FootprintEntity>();
			
			var entitiesDictionary = new Dictionary<string, FootprintEntity>();
			foreach (var item in cache)
			{
				var retrieveResponse = item.Value as RetrieveResponse;
				if (retrieveResponse != null)
				{
					if (retrieveResponse.Entity != null)
					{
						var entityItem = new FootprintEntityItem(item, retrieveResponse.Entity, alternateLink, cache.Name);
						if (!entitiesDictionary.ContainsKey(retrieveResponse.Entity.LogicalName))
						{
							var entity = new FootprintEntity(retrieveResponse.Entity.LogicalName);
							entity.Items.Add(entityItem);
							entitiesDictionary.Add(retrieveResponse.Entity.LogicalName, entity);
						}
						else
						{
							FootprintEntity entity;
							entitiesDictionary.TryGetValue(retrieveResponse.Entity.LogicalName, out entity);
							if (entity != null)
							{
								entity.Items.Add(entityItem);
							}
						}
					}
				}

				var retrieveMultipleResponse = item.Value as RetrieveMultipleResponse;
				if (retrieveMultipleResponse != null)
				{
					var entities = retrieveMultipleResponse.EntityCollection.Entities
						.Select(entity => new FootprintEntityItem(item, entity, alternateLink, cache.Name))
						.ToList();

					if (!entitiesDictionary.ContainsKey(retrieveMultipleResponse.EntityCollection.EntityName))
					{
						var entity = new FootprintEntity(retrieveMultipleResponse.EntityCollection.EntityName);
						entity.Items.AddRange(entities);
						entitiesDictionary.Add(retrieveMultipleResponse.EntityCollection.EntityName, entity);
					}
					else
					{
						FootprintEntity entity;
						entitiesDictionary.TryGetValue(retrieveMultipleResponse.EntityCollection.EntityName, out entity);
						if (entity != null)
						{
							entity.Items.AddRange(entities);
						}
					}
				}
			}

			retval.AddRange(entitiesDictionary.Values);
			return retval;
		}

		private static long GetEntitySizeInMemory(Entity entity)
		{
			////TODO: analyze all fields and culculate size seperately
			return entity.SerializeByJson(KnownTypesProvider.QueryExpressionKnownTypes).Length;
		}

		private static IEnumerable<SyndicationItem> GetFeedItems<TKey>(
			ObjectCache cache,
			Func<string, object, bool> filter,
			Func<CacheItemDetail, TKey> orderBy,
			Uri itemAlternateLink = null,
			string regionName = null,
			bool expanded = false)
		{
			return
				from item in cache
				where filter(item.Key, item.Value)
				let detail = cache.GetCacheItemDetail(item.Key, regionName)
				let defaultDetail = detail ?? new CacheItemDetail(item.Key, new CacheItemPolicy()) { UpdatedOn = default(DateTimeOffset) }
				let telemetry = GetCacheItemTelemetry(cache, item.Key, regionName)
				let updatedOn = defaultDetail.UpdatedOn
				let link = ApplyKey(itemAlternateLink, "key", item.Key, cache.Name)
				let remove = ApplyKey(itemAlternateLink, "remove", item.Key, cache.Name)
				orderby orderBy(defaultDetail)
				select new SyndicationItem(item.Key, GetContent(cache.Name, detail, telemetry, item.Value, remove, expanded), link, item.Key, updatedOn);
		}

		private static Uri ApplyKey(Uri uri, string keyName, string key, string cacheName)
		{
			if (uri == null || string.IsNullOrWhiteSpace(key)) return null;

			var url = uri.OriginalString.AppendQueryString(new Dictionary<string, string> { { keyName, key }, { "objectCacheName", cacheName } });
			return new Uri(url);
		}

		private static SyndicationContent GetContent(string name, CacheItemDetail detail, CacheItemTelemetry telemetry, object value, Uri removeLink, bool expanded)
		{
			var defaultProps = new[]
			{
				new XElement("name", name),
				new XElement("type", value.GetType().ToString()),
				new XElement("remove", new XAttribute("href", removeLink))
			};

			var properties = defaultProps.Concat(GetContent(detail, telemetry)).Concat(GetContentValues(value, expanded));

			var content = new XElement("properties",  properties);
			
			var sc = SyndicationContent.CreateXmlContent(content);
			return sc;
		}

		private static IEnumerable<XElement> GetContent(CacheItemDetail detail, CacheItemTelemetry telemetry)
		{
			if (detail != null && detail.Policy != null)
			{
				yield return new XElement("policy", GetPolicyContent(detail));
			}

			if (telemetry != null)
			{
				yield return new XElement("telemetry", GetTelemetryContent(detail, telemetry));
			}
		}

		private static IEnumerable<XElement> GetPolicyContent(CacheItemDetail detail)
		{
			yield return new XElement("isStaleDataAllowed", detail.IsStaleDataAllowed);
			yield return new XElement("absoluteExpiration", detail.Policy.AbsoluteExpiration);
			yield return new XElement("slidingExpiration", detail.Policy.SlidingExpiration);
			yield return new XElement("priority", detail.Policy.Priority);

			if (detail.Policy.ChangeMonitors != null)
			{
				yield return new XElement("changeMonitors", GetContent(detail.Policy.ChangeMonitors));
			}
		}

		private static IEnumerable<XElement> GetTelemetryContent(CacheItemDetail detail, CacheItemTelemetry telemetry)
		{

			yield return new XElement("memberName", telemetry.Caller.MemberName);
			yield return new XElement("sourceFilePath", telemetry.Caller.SourceFilePath);
			yield return new XElement("sourceLineNumber", telemetry.Caller.SourceLineNumber);
			yield return new XElement("duration", telemetry.Duration);
			yield return new XElement("accessCount", telemetry.AccessCount);
			yield return new XElement("cacheItemStatus", detail.CacheItemStatus.ToString());
			yield return new XElement("staleAccessCount", telemetry.StaleAccessCount);
			yield return new XElement("lastAccessedOn", telemetry.LastAccessedOn);
			yield return new XElement("isStartup", telemetry.IsStartup);
			yield return new XElement("isAllColumns", telemetry.IsAllColumns);
			yield return new XElement("attributes", telemetry.Attributes);

		}

		private static IEnumerable<XElement> GetContent(IEnumerable<ChangeMonitorDetail> monitors)
		{
			var keys = monitors.SelectMany(cm => cm.CacheKeys).Select(key => new XElement("cacheEntry", key));

			return keys;
		}

		private static IEnumerable<XElement> GetContentValues(object value, bool expanded)
		{
			if (!expanded || value == null) yield break;

			var response = value as OrganizationResponse;
			var entity = value as Entity;
			var entities = value as EntityCollection;

			if (response != null)
			{
				yield return new XElement("value", GetContentValues(response));
			}
			else if (entity != null)
			{
				yield return new XElement("value", GetContentValue(entity, new[] { entity }));
			}
			else if (entities != null)
			{
				yield return new XElement("value", GetContentValue(entities, new Entity[] { }));
			}
			else
			{
				yield return new XElement("value", value.ToString());
			}
		}

		private static IEnumerable<XElement> GetContentValues(OrganizationResponse response)
		{
			yield return new XElement(response.ResponseName, GetContentValues(response.Results));
		}

		private static IEnumerable<XElement> GetContentValues(ParameterCollection parameters)
		{
			return parameters.Select(p => new XElement("parameter", GetContentValues(p.Key, p.Value)));
		}

		private static IEnumerable<XObject> GetContentValues(string key, object value)
		{
			yield return new XAttribute("key", key);

			if (value != null)
			{
				yield return new XAttribute("type", value);

				var entity = value as Entity;

				if (entity != null)
				{
					yield return GetContentValue(entity, new[] { entity });
				}

				var entities = value as EntityCollection;

				if (entities != null)
				{
					yield return GetContentValue(entities, new Entity[] { });
				}
			}
		}

		private static IEnumerable<XObject> GetContentValues(Entity entity, IEnumerable<Entity> path)
		{
			yield return new XAttribute("Id", entity.Id);

			if (entity.EntityState != null)
			{
				yield return new XAttribute("EntityState", entity.EntityState);
			}

			foreach (var attribute in GetContentValues(entity.Attributes))
			{
				yield return attribute;
			}

			foreach (var attribute in GetContentValues(entity.FormattedValues))
			{
				yield return attribute;
			}

			foreach (var relationship in GetContentValues(entity.RelatedEntities, path))
			{
				yield return relationship;
			}
		}

		private static IEnumerable<XElement> GetContentValues(AttributeCollection attributes)
		{
			return attributes.OrderBy(a => a.Key).Select(a => new XElement("attribute", new XAttribute("name", a.Key), GetContentValue(a.Value)));
		}

		private static XObject GetContentValue(object value)
		{
			var reference = value as EntityReference;

			if (reference != null)
			{
				return new XElement("entityReference",
					new XAttribute("id", reference.Id),
					new XAttribute("logicalName", reference.LogicalName ?? string.Empty),
					new XAttribute("name", reference.Name ?? string.Empty));
			}

			var option = value as OptionSetValue;

			if (option != null)
			{
				return new XText(option.Value.ToString());
			}

			var money = value as Money;

			if (money != null)
			{
				return new XText(money.Value.ToString());
			}

			return new XText(value != null ? value.ToString() : string.Empty);
		}

		private static IEnumerable<XElement> GetContentValues(FormattedValueCollection attributes)
		{
			return attributes.OrderBy(a => a.Key).Select(a => new XElement("label", new XAttribute("name", a.Key), new XText(a.Value.ToString())));
		}

		private static IEnumerable<XElement> GetContentValues(EntityCollection entities, IEnumerable<Entity> path)
		{
			foreach (var entity in entities.Entities)
			{
				// cycle detection

				if (path.Contains(entity))
				{
					// cycle found

					yield return new XElement(entity.LogicalName, new XAttribute("Id", entity.Id));
				}
				else
				{
					yield return GetContentValue(entity, path.Concat(new[] { entity }));
				}
			}
		}

		private static IEnumerable<XElement> GetContentValues(RelatedEntityCollection relationships, IEnumerable<Entity> path)
		{
			return relationships.OrderBy(r => r.Key.SchemaName).Select(r => new XElement("relationship", new XAttribute("name", r.Key.SchemaName), GetContentValue(r.Value, path)));
		}

		private static XElement GetContentValue(Entity entity, IEnumerable<Entity> path)
		{
			return new XElement(entity.LogicalName, GetContentValues(entity, path));
		}

		private static XElement GetContentValue(EntityCollection entities, IEnumerable<Entity> path)
		{
			return new XElement("entities", GetContentValues(entities, path));
		}

		/// <summary>
		/// Class to hold information for a single entity record for purposes of rendering the cache footprint.
		/// </summary>
		private class FootprintEntityItem
		{
			public Entity Entity { get; private set; }
			public string CacheItemKey { get; private set; }
			public Type CacheItemType { get; private set; }
			public Uri Link { get; private set; }

			public FootprintEntityItem(KeyValuePair<string, object> cacheItem, Entity entity, Uri alternateLink, string cacheName)
			{
				this.CacheItemKey = cacheItem.Key;
				this.CacheItemType = cacheItem.Value.GetType();
				this.Entity = entity;
				this.Link = ApplyKey(alternateLink, "key", cacheItem.Key, cacheName);
			}
		}

		/// <summary>
		/// Class to hold information for an entire entity (containing many individual entity records) for purposes of rendering the cache footprint.
		/// </summary>
		private class FootprintEntity
		{
			public string Name { get; private set; }
			public List<FootprintEntityItem> Items { get; private set; }

			public FootprintEntity(string name)
			{
				this.Name = name;
				this.Items = new List<FootprintEntityItem>();	
			}

			/// <summary>
			/// Gets the size of all the cache items of this entity in units of characters.
			/// </summary>
			/// <returns></returns>
			public long GetSize()
			{
				long size = 0;
				foreach (var entity in this.Items)
				{
					size = size + GetEntitySizeInMemory(entity.Entity);
				}
				return size;
			}

			/// <summary>
			/// Gets the number of items of this entity type.
			/// </summary>
			/// <returns></returns>
			public int GetCount()
			{
				return this.Items.Count;
			}
		}

		#endregion
	}
}
