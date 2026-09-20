/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Caching;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Configuration;

namespace Adxstudio.Xrm.Services
{
	/// <summary>
	/// A wrapper class containing a nested <see cref="ObjectCache"/>.
	/// </summary>
	public class CompositeObjectCache : ObjectCache, IInitializable
	{
		private string _name;
		public ObjectCache Cache { get; private set; }

		public CompositeObjectCache()
			: this(null)
		{
		}

		public CompositeObjectCache(ObjectCache cache)
			: this("Default", cache)
		{
		}

		public CompositeObjectCache(string name, ObjectCache cache)
		{
			_name = name;
			Cache = cache;
		}

		/// <summary>
		/// Initializes custom settings.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="config"></param>
		public virtual void Initialize(string name, NameValueCollection config)
		{
			_name = name;

			if (config != null && Cache == null)
			{
				var innerTypeName = config["innerType"];

				if (!string.IsNullOrWhiteSpace(innerTypeName))
				{
					// instantiate by type

					var innerType = TypeExtensions.GetType(innerTypeName);

					if (innerType == typeof(MemoryCache))
					{
						Cache = new MemoryCache(name, config);
					}
					else if (innerType.IsA<MemoryCache>())
					{
						Cache = Activator.CreateInstance(innerType, name, config) as ObjectCache;
					}
					else
					{
						Cache = Activator.CreateInstance(innerType) as ObjectCache;
					}
				}

				var innerObjectCacheName = config["innerObjectCacheName"];

				if (!string.IsNullOrWhiteSpace(innerObjectCacheName))
				{
					// instantiate by config

					Cache = CrmConfigurationManager.CreateObjectCache(innerObjectCacheName);
				}
			}

			if (Cache == null)
			{
				// fall back to MemoryCache

				Cache = new MemoryCache(name, config);
			}
		}

		/// <summary>
		/// When overridden in a derived class, creates a <see cref="T:System.Runtime.Caching.CacheEntryChangeMonitor"/> object that can trigger events in response to changes to specified cache entries. 
		/// </summary>
		/// <returns>
		/// A change monitor that monitors cache entries in the cache. 
		/// </returns>
		/// <param name="keys">The unique identifiers for cache entries to monitor. </param><param name="regionName">Optional. A named region in the cache where the cache keys in the <paramref name="keys"/> parameter exist, if regions are implemented. The default value for the optional parameter is null.</param>
		public override CacheEntryChangeMonitor CreateCacheEntryChangeMonitor(IEnumerable<string> keys, string regionName = null)
		{
			return Cache.CreateCacheEntryChangeMonitor(keys, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, creates an enumerator that can be used to iterate through a collection of cache entries. 
		/// </summary>
		/// <returns>
		/// The enumerator object that provides access to the cache entries in the cache.
		/// </returns>
		protected override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
		{
			return (Cache as IEnumerable<KeyValuePair<string, object>>).GetEnumerator();
		}

		/// <summary>
		/// When overridden in a derived class, checks whether the cache entry already exists in the cache.
		/// </summary>
		/// <returns>
		/// true if the cache contains a cache entry with the same key value as <paramref name="key"/>; otherwise, false. 
		/// </returns>
		/// <param name="key">A unique identifier for the cache entry. </param><param name="regionName">Optional. A named region in the cache where the cache can be found, if regions are implemented. The default value for the optional parameter is null.</param>
		public override bool Contains(string key, string regionName = null)
		{
			return Cache.Contains(key, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, tries to insert a cache entry into the cache as a CacheItem instance, and adds details about how the entry should be evicted.
		/// </summary>
		/// <param name="item">The object to add.</param>
		/// <param name="policy">An object that contains eviction details for the cache entry. This object provides more options for eviction than a simple absolute expiration.</param>
		/// <returns>true if insertion succeeded, or false if there is an already an entry in the cache that has the same key as item.</returns>
		public override bool Add(CacheItem item, CacheItemPolicy policy)
		{
			return Cache.Add(item, policy);
		}

		/// <summary>
		/// When overridden in a derived class, inserts a cache entry into the cache without overwriting any existing cache entry. 
		/// </summary>
		/// <param name="key">A unique identifier for the cache entry.</param>
		/// <param name="value">The object to insert. </param>
		/// <param name="absoluteExpiration">The fixed date and time at which the cache entry will expire. This parameter is required when the Add method is called.</param>
		/// <param name="regionName">Optional. A named region in the cache to which the cache entry can be added, if regions are implemented. Because regions are not implemented in .NET Framework 4, the default value is a null reference (Nothing in Visual Basic).</param>
		/// <returns>true if insertion succeeded, or false if there is an already an entry in the cache that has the same key as key.</returns>
		public override bool Add(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
		{
			return Cache.Add(key, value, absoluteExpiration, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, inserts a cache entry into the cache, specifying information about how the entry will be evicted.
		/// </summary>
		/// <param name="key">A unique identifier for the cache entry.</param>
		/// <param name="value">The object to insert.</param>
		/// <param name="policy">An object that contains eviction details for the cache entry. This object provides more options for eviction than a simple absolute expiration.</param>
		/// <param name="regionName">Optional. A named region in the cache to which the cache entry can be added, if regions are implemented. The default value for the optional parameter is a null reference (Nothing in Visual Basic).</param>
		/// <returns>true if insertion succeeded, or false if there is an already an entry in the cache that has the same key as item.</returns>
		public override bool Add(string key, object value, CacheItemPolicy policy, string regionName = null)
		{
			return Cache.Add(key, value, policy, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, inserts a cache entry into the cache, by using a key, an object for the cache entry, an absolute expiration value, and an optional region to add the cache into.
		/// </summary>
		/// <returns>
		/// If a cache entry with the same key exists, the specified cache entry's value; otherwise, null.
		/// </returns>
		/// <param name="key">A unique identifier for the cache entry. </param><param name="value">The object to insert. </param><param name="absoluteExpiration">The fixed date and time at which the cache entry will expire. </param><param name="regionName">Optional. A named region in the cache to which the cache entry can be added, if regions are implemented. The default value for the optional parameter is null.</param>
		public override object AddOrGetExisting(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
		{
			return Cache.AddOrGetExisting(key, value, absoluteExpiration, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, inserts the specified <see cref="T:System.Runtime.Caching.CacheItem"/> object into the cache, specifying information about how the entry will be evicted.
		/// </summary>
		/// <returns>
		/// If a cache entry with the same key exists, the specified cache entry; otherwise, null.
		/// </returns>
		/// <param name="value">The object to insert. </param><param name="policy">An object that contains eviction details for the cache entry. This object provides more options for eviction than a simple absolute expiration.</param>
		public override CacheItem AddOrGetExisting(CacheItem value, CacheItemPolicy policy)
		{
			return Cache.AddOrGetExisting(value, policy);
		}

		/// <summary>
		/// When overridden in a derived class, inserts a cache entry into the cache, specifying a key and a value for the cache entry, and information about how the entry will be evicted.
		/// </summary>
		/// <returns>
		/// If a cache entry with the same key exists, the specified cache entry's value; otherwise, null.
		/// </returns>
		/// <param name="key">A unique identifier for the cache entry. </param><param name="value">The object to insert.</param><param name="policy">An object that contains eviction details for the cache entry. This object provides more options for eviction than a simple absolute expiration. </param><param name="regionName">Optional. A named region in the cache to which the cache entry can be added, if regions are implemented. The default value for the optional parameter is null.</param>
		public override object AddOrGetExisting(string key, object value, CacheItemPolicy policy, string regionName = null)
		{
			return Cache.AddOrGetExisting(key, value, policy, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, gets the specified cache entry from the cache as an object.
		/// </summary>
		/// <returns>
		/// The cache entry that is identified by <paramref name="key"/>. 
		/// </returns>
		/// <param name="key">A unique identifier for the cache entry to get. </param><param name="regionName">Optional. A named region in the cache to which the cache entry was added, if regions are implemented. The default value for the optional parameter is null.</param>
		public override object Get(string key, string regionName = null)
		{
			return Cache.Get(key, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, gets the specified cache entry from the cache as a <see cref="T:System.Runtime.Caching.CacheItem"/> instance.
		/// </summary>
		/// <returns>
		/// The cache entry that is identified by <paramref name="key"/>.
		/// </returns>
		/// <param name="key">A unique identifier for the cache entry to get. </param><param name="regionName">Optional. A named region in the cache to which the cache was added, if regions are implemented. Because regions are not implemented in .NET Framework 4, the default is null.</param>
		public override CacheItem GetCacheItem(string key, string regionName = null)
		{
			return Cache.GetCacheItem(key, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, inserts a cache entry into the cache, specifying time-based expiration details. 
		/// </summary>
		/// <param name="key">A unique identifier for the cache entry. </param><param name="value">The object to insert.</param><param name="absoluteExpiration">The fixed date and time at which the cache entry will expire.</param><param name="regionName">Optional. A named region in the cache to which the cache entry can be added, if regions are implemented. The default value for the optional parameter is null.</param>
		public override void Set(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
		{
			Cache.Set(key, value, absoluteExpiration, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, inserts the cache entry into the cache as a <see cref="T:System.Runtime.Caching.CacheItem"/> instance, specifying information about how the entry will be evicted.
		/// </summary>
		/// <param name="item">The cache item to add.</param><param name="policy">An object that contains eviction details for the cache entry. This object provides more options for eviction than a simple absolute expiration.</param>
		public override void Set(CacheItem item, CacheItemPolicy policy)
		{
			Cache.Set(item, policy);
		}

		/// <summary>
		/// When overridden in a derived class, inserts a cache entry into the cache. 
		/// </summary>
		/// <param name="key">A unique identifier for the cache entry. </param><param name="value">The object to insert.</param><param name="policy">An object that contains eviction details for the cache entry. This object provides more options for eviction than a simple absolute expiration.</param><param name="regionName">Optional. A named region in the cache to which the cache entry can be added, if regions are implemented. The default value for the optional parameter is null.</param>
		public override void Set(string key, object value, CacheItemPolicy policy, string regionName = null)
		{
			Cache.Set(key, value, policy, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, gets a set of cache entries that correspond to the specified keys.
		/// </summary>
		/// <returns>
		/// A dictionary of key/value pairs that represent cache entries. 
		/// </returns>
		/// <param name="keys">A collection of unique identifiers for the cache entries to get. </param><param name="regionName">Optional. A named region in the cache to which the cache entry or entries were added, if regions are implemented. The default value for the optional parameter is null.</param>
		public override IDictionary<string, object> GetValues(IEnumerable<string> keys, string regionName = null)
		{
			return Cache.GetValues(keys, regionName);
		}

		/// <summary>
		/// Gets a set of cache entries that correspond to the specified keys.
		/// </summary>
		/// <param name="regionName">Optional. A named region in the cache to which the cache entry or entries were added, if regions are implemented. Because regions are not implemented in .NET Framework 4, the default is a null reference (Nothing in Visual Basic).</param>
		/// <param name="keys">A collection of unique identifiers for the cache entries to get.</param>
		/// <returns>A dictionary of key/value pairs that represent cache entries.</returns>
		public override IDictionary<string, object> GetValues(string regionName, params string[] keys)
		{
			return Cache.GetValues(regionName, keys);
		}

		/// <summary>
		/// When overridden in a derived class, removes the cache entry from the cache.
		/// </summary>
		/// <returns>
		/// An object that represents the value of the removed cache entry that was specified by the key, or null if the specified entry was not found.
		/// </returns>
		/// <param name="key">A unique identifier for the cache entry. </param><param name="regionName">Optional. A named region in the cache to which the cache entry was added, if regions are implemented. The default value for the optional parameter is null.</param>
		public override object Remove(string key, string regionName = null)
		{
			return Cache.Remove(key, regionName);
		}

		/// <summary>
		/// When overridden in a derived class, gets the total number of cache entries in the cache. 
		/// </summary>
		/// <returns>
		/// The number of cache entries in the cache. If <paramref name="regionName"/> is not null, the count indicates the number of entries that are in the specified cache region. 
		/// </returns>
		/// <param name="regionName">Optional. A named region in the cache for which the cache entry count should be computed, if regions are implemented. The default value for the optional parameter is null.</param>
		public override long GetCount(string regionName = null)
		{
			return Cache.GetCount(regionName);
		}

		/// <summary>
		/// When overridden in a derived class, gets a description of the features that a cache implementation provides.
		/// </summary>
		/// <returns>
		/// A bitwise combination of flags that indicate the default capabilities of a cache implementation.
		/// </returns>
		public override DefaultCacheCapabilities DefaultCacheCapabilities
		{
			get { return Cache.DefaultCacheCapabilities; }
		}

		/// <summary>
		/// Gets the name of a specific <see cref="T:System.Runtime.Caching.ObjectCache"/> instance. 
		/// </summary>
		/// <returns>
		/// The name of a specific cache instance.
		/// </returns>
		public override string Name
		{
			get { return _name; }
		}

		/// <summary>
		/// Gets or sets the default indexer for the <see cref="T:System.Runtime.Caching.ObjectCache"/> class.
		/// </summary>
		/// <returns>
		/// A key that serves as an indexer into the cache instance.
		/// </returns>
		/// <param name="key">A unique identifier for a cache entry in the cache. </param>
		public override object this[string key]
		{
			get { return Cache[key]; }
			set { Cache[key] = value; }
		}
	}
}
