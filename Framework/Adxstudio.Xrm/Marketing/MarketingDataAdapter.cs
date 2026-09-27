/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using Adxstudio.Xrm.Cms;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Messages;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Adxstudio.Xrm.Marketing
{
	public class MarketingDataAdapter : IMarketingDataAdapter
	{
		private const string EncryptionKeySetting = "Marketing/EncryptionKey";

		private readonly Lazy<string> _encryptionKey;
		
		private string EncryptionKey
		{
			get { return _encryptionKey.Value; }
		}

		protected IDataAdapterDependencies Dependencies { get; private set; }
		
		public MarketingDataAdapter(IDataAdapterDependencies dependencies)
		{
			if (dependencies == null)
			{
				throw new ArgumentNullException("dependencies");
			}

			Dependencies = dependencies;
			_encryptionKey = new Lazy<string>(GetEncryptionKey);
		}

		private string GetEncryptionKey()
		{
			var context = Dependencies.GetServiceContext();
			var key = context.GetSettingValueByName(EncryptionKeySetting);
			if (string.IsNullOrEmpty(key))
			{
				throw new EncryptionKeyMissingException();
			}
			return key;
		}

		public IEnumerable<IMarketingList> GetMarketingLists(string encodedEmail, string signature)
		{
			Validate(encodedEmail, signature);
			var emailAddress = Decode(encodedEmail);
			return GetMarketingLists(emailAddress);
		}

		private IEnumerable<IMarketingList> GetMarketingLists(string emailAddress)
		{
			var context = Dependencies.GetServiceContext();
			var website = Dependencies.GetWebsite();

			const string fetchXml =
				@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"" >
    <entity name=""list"" >
        <attribute name=""listid"" />
        <attribute name=""listname"" />
        <attribute name=""purpose"" />
        <order attribute=""listname"" />
        <filter type=""and"" >
            <condition attribute=""statecode"" operator=""eq"" value=""0"" />
        </filter>
        <link-entity name=""adx_website_list"" from=""listid"" to=""listid"" visible=""false"" intersect=""true"" >
            <link-entity name=""adx_website"" from=""adx_websiteid"" to=""adx_websiteid"" alias=""af"" >
                <filter type=""and"" >
                    <condition attribute=""adx_websiteid"" operator=""eq"" value=""{1}"" />
                </filter>
            </link-entity>
        </link-entity>
        <link-entity name=""listmember"" from=""listid"" to=""listid"" visible=""false"" intersect=""true"" link-type=""outer"" >
            <link-entity name=""contact"" from=""contactid"" to=""entityid"" alias=""c"" link-type=""outer"" >
                <attribute name=""contactid"" />
                <filter type=""and"" >
                    <filter type=""or"" >
                        <condition attribute=""emailaddress1"" operator=""eq"" value=""{0}"" />
                        <condition attribute=""emailaddress2"" operator=""eq"" value=""{0}"" />
                        <condition attribute=""emailaddress3"" operator=""eq"" value=""{0}"" />
                    </filter>
                </filter>
            </link-entity>
        </link-entity>
        <link-entity name=""listmember"" from=""listid"" to=""listid"" visible=""false"" intersect=""true"" link-type=""outer"" >
            <link-entity name=""lead"" from=""leadid"" to=""entityid"" alias=""l"" link-type=""outer"" >
                <attribute name=""leadid"" />
                <filter type=""and"" >
                    <filter type=""or"" >
                        <condition attribute=""emailaddress1"" operator=""eq"" value=""{0}"" />
                        <condition attribute=""emailaddress2"" operator=""eq"" value=""{0}"" />
                        <condition attribute=""emailaddress3"" operator=""eq"" value=""{0}"" />
                    </filter>
                </filter>
            </link-entity>
        </link-entity>
        <link-entity name=""listmember"" from=""listid"" to=""listid"" visible=""false"" intersect=""true"" link-type=""outer"" >
            <link-entity name=""account"" from=""accountid"" to=""entityid"" alias=""a"" link-type=""outer"" >
                <attribute name=""accountid"" />
                <filter type=""and"" >
                    <filter type=""or"" >
                        <condition attribute=""emailaddress1"" operator=""eq"" value=""{0}"" />
                        <condition attribute=""emailaddress2"" operator=""eq"" value=""{0}"" />
                        <condition attribute=""emailaddress3"" operator=""eq"" value=""{0}"" />
                    </filter>
                </filter>
            </link-entity>
        </link-entity>
        <filter operator=""and""> 
            <filter type=""or""> 
                <condition entityname=""c"" attribute=""contactid"" operator=""not-null"" />
                <condition entityname=""l"" attribute=""leadid"" operator=""not-null"" />
                <condition entityname=""a"" attribute=""accountid"" operator=""not-null"" />
            </filter> 
        </filter>
    </entity>
</fetch>";

			var lists = (RetrieveMultipleResponse)context.Execute(new RetrieveMultipleRequest
			{
				Query = new FetchExpression(fetchXml.FormatWith(emailAddress, website.Id.ToString("B")))
			});

			var groups = lists.EntityCollection.Entities.GroupBy(l => l.GetAttributeValue<Guid>("listid"));
			
			var marketingLists = groups.Select(group => new MarketingList(group));

			return marketingLists;
		}
		
		public IEnumerable<IMarketingList> Unsubscribe(string encodedEmail, string signature)
		{
			Validate(encodedEmail, signature);
			var emailAddress = Decode(encodedEmail);
			return Unsubscribe(emailAddress);
		}

		public IEnumerable<IMarketingList> Unsubscribe(string encodedEmail, string encodedList, string signature)
		{
			Validate(encodedEmail, encodedList, signature);
			var emailAddress = Decode(encodedEmail);
			var listId = Decode(encodedList);
			return Unsubscribe(emailAddress, new[] { listId });
		}

		public IEnumerable<IMarketingList> Unsubscribe(string encodedEmail, IEnumerable<string> listIds, string signature)
		{
			Validate(encodedEmail, signature);
			var emailAddress = Decode(encodedEmail);
			return Unsubscribe(emailAddress, listIds);
		}

		private IEnumerable<IMarketingList> Unsubscribe(string emailAddress, IEnumerable<string> listIds = null)
		{
			var context = Dependencies.GetServiceContextForWrite();

			var lists = GetMarketingLists(emailAddress).Where(l => listIds == null || listIds.Select(Guid.Parse).Contains(l.Id));

			var unsubscribedLists = new List<IMarketingList>();

			foreach (var list in lists)
			{
				unsubscribedLists.Add(list);
				foreach (var subscriber in list.Subscribers)
				{
					context.RemoveMemberList(list.Id, subscriber.Id);
				}
			}

			return unsubscribedLists.Distinct();
		}

		public string ConstructSignature(string encodedEmail, string encodedList = "")
		{
			var emailAddress = Decode(encodedEmail);
			var listId = Decode(encodedList);
			var signature = string.IsNullOrEmpty(listId) ? emailAddress : string.Format("{0}/{1}", emailAddress, listId);
			var bytes = Encoding.UTF8.GetBytes(signature);
			var key = Encoding.UTF8.GetBytes(EncryptionKey);
			using (var crypto = new HMACSHA256(key))
			{
				var hash = crypto.ComputeHash(bytes);
				return Encode(hash);
			}
		}
		
		private void Validate(string encodedEmail, string signature)
		{
			Validate(encodedEmail, string.Empty, signature);
		}
		
		private void Validate(string encodedEmail, string encodedList, string signature)
		{
			var confirmation = ConstructSignature(encodedEmail, encodedList);
			if (signature != confirmation)
			{
				throw new InvalidSignatureException();
			}
		}

		public static string Encode(string str)
		{
			return Encode(Encoding.UTF8.GetBytes(str));
		}

		public static string Encode(byte[] bytes)
		{
			return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').Replace("=", string.Empty);
		}

		public static string Decode(string str)
		{
			while (str.Length % 4 != 0)
			{
				str += '=';
			}
			return Encoding.UTF8.GetString(Convert.FromBase64String(str.Replace('-', '+').Replace('_', '/')));
		}
	}
}
