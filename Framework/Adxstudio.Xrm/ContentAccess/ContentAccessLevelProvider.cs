/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.ContentAccess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Xrm.Portal;
    using Adxstudio.Xrm.Security;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Metadata;
    using Microsoft.Xrm.Sdk.Query;
    using Adxstudio.Xrm.Services.Query;

    /// <summary>
    /// Implementation of <see cref="ContentAccessLevelProvider"/>. Provides filtering based on Content Access Level associations.
    /// </summary>
    public sealed class ContentAccessLevelProvider : ContentAccessProvider
    {
        #region Private Members
        /// <summary>
        /// Entity Metadata for Content Access Level entity
        /// </summary>
        private EntityMetadata calEntityMetadata;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAccessLevelProvider"/> class.
        /// </summary>
        public ContentAccessLevelProvider() : this(ContentAccessConfiguration.DefaultContentAccessLevelConfiguration())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAccessLevelProvider"/> class.
        /// </summary>
        /// <param name="configuration">Configuration for FetchXML attributes</param>
        public ContentAccessLevelProvider(ContentAccessConfiguration configuration) : base(configuration)
        {
			this.CurrentUserRoleNames = new Lazy<string[]>(() => CrmEntityPermissionProvider.GetRolesForUser(this.Portal.ServiceContext, this.Portal.Website.ToEntityReference()));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAccessLevelProvider"/> class.
        /// </summary>
        /// <param name="portalContext">Portal Context</param>
        /// <param name="currentUserRoleNames">Current User Roles Names</param>
        /// <param name="calEntityMetadata">Entity Metadata for Content Access Level entity</param>
        /// <param name="siteSettingDictionary">Site Setting Dictionary</param>
        public ContentAccessLevelProvider(IPortalContext portalContext, string[] currentUserRoleNames, EntityMetadata calEntityMetadata, Dictionary<string, string> siteSettingDictionary)
            : base(ContentAccessConfiguration.DefaultContentAccessLevelConfiguration(), portalContext, siteSettingDictionary)
        {
            this.CurrentUserRoleNames = new Lazy<string[]>(() => currentUserRoleNames);
            this.calEntityMetadata = calEntityMetadata;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAccessLevelProvider"/> class.
        /// </summary>
        /// <param name="portalContext">Portal Context</param>
        public ContentAccessLevelProvider(IPortalContext portalContext)
            : base(ContentAccessConfiguration.DefaultContentAccessLevelConfiguration(), portalContext)
        {
        }
        #endregion

        #region ContentAccessProvider Overrides
        /// <summary>
        /// Applies the CAL filtering to the existing FetchXML query
        /// </summary>
        /// <param name="right">Current Permission Right</param>
        /// <param name="fetchIn">FetchXML to modify</param>
        public override void TryApplyRecordLevelFiltersToFetch(CrmEntityPermissionRight right, Fetch fetchIn)
        {
            // Apply filter only if Entity is "Knowledge Article" and Right is "Read"
            if (!this.IsRightEntityAndPermissionRight(right, fetchIn, this.Config.SourceEntityName, CrmEntityPermissionRight.Read))
            {
                return;
            }

            // If CAL is not enabled
            if (!this.IsEnabled())
            {
                return;
            }

            // Retrieve CAL IDs
            var userCALIDs = this.RetrieveCurrentUserContentAccessLevels();

            // Inject CAL IDs filter into FetchXML
            this.TryRecordLevelFiltersToFetch(fetchIn, userCALIDs);
        }
        #endregion
        
        #region Public Methods
        /// <summary>
        /// Determines whether Content Access Level are enabled or not,
        /// by checking entity existence and config's site setting
        /// </summary>
        /// <returns>True if config's site setting exists and is enabled</returns>
        public override bool IsEnabled()
        {
            if (this.calEntityMetadata == null)
            {
                try
                {
                    if (!Metadata.OrganizationServiceContextExtensions.EntityExists(this.Portal.ServiceContext, "adx_contentaccesslevel"))
                    {
                        return false;
                    }

                    this.calEntityMetadata = ContentAccessProvider.GetEntityMetadata(this.Portal.ServiceContext, "adx_contentaccesslevel", EntityFilters.Entity);
                }
                catch
                {
                    return false;
                }
            }

            return base.IsEnabled();
        }

        /// <summary>
        /// Retrieves the list of Content Access Levels available to the current user
        /// </summary>
        /// <returns>Content Access Level collection</returns>
        public IEnumerable<Entity> GetContentAccessLevels()
        {
            if (!this.IsEnabled())
            {
                return Enumerable.Empty<Entity>();
            }

            const string ContentAccessLevelsFetchXmlFormat = @"
				<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
					<entity name='adx_contentaccesslevel'>
						<attribute name='adx_contentaccesslevelid' />
                        <link-entity name='adx_contactcontentaccesslevel' from='adx_contentaccesslevelid' to='adx_contentaccesslevelid' visible='false' intersect='true' link-type='outer' alias='cal2contact'>
						    <attribute name='contactid' />
                            <filter type='or'>
							    <condition attribute='contactid' operator='eq' value='{0}' />
							    <condition attribute='contactid' operator='eq' value='{1}' />
						    </filter>
						</link-entity>
                        <link-entity name='adx_accountcontentaccesslevel' from='adx_contentaccesslevelid' to='adx_contentaccesslevelid' visible='false' intersect='true' link-type='outer' alias='cal2account'>
						    <attribute name='accountid' />
                            <filter type='and'>
							    <condition attribute='accountid' operator='eq' value='{2}' />
						    </filter>
						</link-entity>
                            {3}
                        <filter type='and'>
                            <condition attribute='statecode' operator='eq' value='0' />
                            <filter type='or'>
							    <condition entityname='cal2contact' attribute='contactid' operator='not-null' />
							    <condition entityname='cal2account' attribute='accountid' operator='not-null' />
							    {4}
						    </filter>
                        </filter>
					</entity>
				</fetch>";

            // Only inject this join if the WebRole for the current user is not empty
            var webRoleFetchXmlFormat = this.CurrentUserRoleNames.Value.Any()
                ? @"<link-entity name='adx_webrolecontentaccesslevel' from='adx_contentaccesslevelid' to='adx_contentaccesslevelid' visible='false' intersect='true' link-type='outer' alias='cal2webroleintersect'>
                        <link-entity name='adx_webrole' from='adx_webroleid' to='adx_webroleid' visible='false' link-type='outer' alias='cal2webrole'>
						    <attribute name='adx_webroleid' />
                            <filter type='and'>
							    <condition attribute='adx_name' operator='in'>
                                    " + this.FormattedCurrentUserRoleNames + @"
                                </condition>
						    </filter>
                        </link-entity>
				    </link-entity>"
                : string.Empty;

            // Only inject this join if the WebRole for the current user is not empty
            var webRoleFilteringFetchXmlFormat = this.CurrentUserRoleNames.Value.Any()
                ? @"<condition entityname='cal2webrole' attribute='adx_webroleid' operator='not-null' />"
                : string.Empty;

            // Inject into FetchXml the User's Id, ParentCustomerId, and associated WebRoles
            var filteredContentAccessLevelsFetchXmlFormat = string.Format(ContentAccessLevelsFetchXmlFormat, this.CurrentUserId, this.ParentCustomerId, this.ParentCustomerId, webRoleFetchXmlFormat, webRoleFilteringFetchXmlFormat);

            var fetchContentAccessLevels = Fetch.Parse(filteredContentAccessLevelsFetchXmlFormat);
            var contentAccessLevelCollection = fetchContentAccessLevels.Execute(this.Portal.ServiceContext as IOrganizationService);

            return contentAccessLevelCollection.Entities;
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// Adds required Content Access Level filter into existing FetchXMl
        /// </summary>
        /// <param name="fetchIn">FetchXML that is constructed through Entity Permissions</param>
        /// <param name="userCALIDs">Collection of Content Access Level IDs</param>
        private void TryRecordLevelFiltersToFetch(Fetch fetchIn, ICollection<object> userCALIDs)
        {
            var link = new Link
            {
                Name = this.Config.IntersectEntityName,
                FromAttribute = this.Config.IntersectFromAttribute,
                ToAttribute = this.Config.IntersectToAttribute,
                Intersect = true,
                Visible = false,
                Filters = new List<Filter>
                {
                    new Filter
                    {
                        Type = LogicalOperator.And,
                        Conditions = new[]
                        {
                            new Condition { Attribute = this.Config.TargetFromAttribute, Operator = ConditionOperator.In, Values = userCALIDs }
                        }
                    }
                }
            };

            if (link != null)
            {
                fetchIn.Entity.Links.Add(link);
            }

			fetchIn.Distinct = true;
        }
        
        /// <summary>
        /// Retrieves the array of Content Access Level IDs available to the current user
        /// </summary>
        /// <returns>Content Access Level IDs</returns>
        private ICollection<object> RetrieveCurrentUserContentAccessLevels()
        {
            var userCALs = this.GetContentAccessLevels().Select(x => x.Id).Cast<object>().ToList();

            var userCALIDs = new List<object>();

            if (!userCALs.Any())
            {
                userCALIDs.Add((object)Guid.Empty);
            }
            else
            {
                userCALIDs.AddRange(userCALs);
            }

            return userCALIDs;
        }

        /// <summary>
        /// String array of current User's associated Webroles
        /// </summary>
        private Lazy<string[]> CurrentUserRoleNames { get; set; }

        /// <summary>
        /// Flattened and formatted representation of <see cref="CurrentUserRoleNames"/> for use in FetchXML
        /// </summary>
        private string FormattedCurrentUserRoleNames
        {
            get
            {
                return string.Format("<value>{0}</value>", string.Join("</value><value>", this.CurrentUserRoleNames.Value));
            }
        }
        #endregion Helper Methods
    }
}
