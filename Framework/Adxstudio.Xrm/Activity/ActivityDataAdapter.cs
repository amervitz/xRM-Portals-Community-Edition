/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Activity
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.IO;
	using Adxstudio.Xrm.Cms;
	using Adxstudio.Xrm.Security;
	using Adxstudio.Xrm.Services.Query;
	using Adxstudio.Xrm.Notes;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Web.UI;
	using Adxstudio.Xrm.Web.UI.CrmEntityListView;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Metadata;
	using Microsoft.Xrm.Sdk.Query;
	using Adxstudio.Xrm.Text;
	using Filter = Adxstudio.Xrm.Services.Query.Filter;

	/// <summary>
	/// Data Adapter for ActivityPointer related data access.
	/// </summary>
	public class ActivityDataAdapter
	{
		private readonly IDataAdapterDependencies _dependencies;
		private const int DefaultMaxPageSize = 25;
		private const int DefaultPageSize = 15;
		private readonly Guid PortalTimelineSavedQueryId = new Guid("620CDB5E-D7C9-E511-80D1-00155D20B011");

		public ActivityDataAdapter(IDataAdapterDependencies dependencies)
		{
			_dependencies = dependencies;
		}

		/// <summary>
		/// Retrieves Activities filtered by regarding object.
		/// </summary>
		/// <param name="regarding"></param>
		/// <param name="orders"></param>
		/// <param name="page"></param>
		/// <param name="pageSize"></param>
		/// <param name="entityMetadata"></param>
		/// <param name="respectPermissions"></param>
		/// <returns></returns>
		public IActivityCollection GetActivities(EntityReference regarding, List<Order> orders = null, int page = 1,
			int pageSize = DefaultMaxPageSize, EntityMetadata entityMetadata = null, bool respectPermissions = true)
		{
			if (pageSize < 0)
			{
				pageSize = DefaultPageSize;
			}

			if (pageSize > DefaultMaxPageSize)
			{
                ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format(
                    "pageSize={0} is greater than the allowed maximum page size of {1}. Page size has been constrained to {1}.",
					pageSize, DefaultMaxPageSize));
				pageSize = DefaultMaxPageSize;
			}
			
			return FetchActivities(regarding, page, pageSize, entityMetadata);
		}

		/// <summary>
		/// Retrieves Attachments filtered by regarding object.
		/// </summary>
		/// <param name="regarding"></param>
		/// <returns></returns>
		public IEnumerable<IAttachment> GetAttachments(EntityReference regarding)
		{
			if (regarding != null)
			{
				switch (regarding.LogicalName)
				{
					case "adx_portalcomment":
						// If can read portal comment, bypass assertion check on attachments and assume read permission.
						return GetAnnotationsAsAttachmentCollection(regarding, !CanReadPortalComment(regarding));
					case "email":
						var activityMimeAttachmentDataAdapter = new ActivityMimeAttachmentDataAdapter(_dependencies);
						return activityMimeAttachmentDataAdapter.GetAttachments(regarding.Id);
				}
			}
			// If unsupported type, return empty list of attachments
			return new List<Attachment>();
		}

		private bool CanReadPortalComment(EntityReference portalCommentReference)
		{
			var serviceContext = _dependencies.GetServiceContext();

			var response = serviceContext.Execute<RetrieveResponse>(new RetrieveRequest
			{
				Target = portalCommentReference,
				ColumnSet = new ColumnSet("regardingobjectid"),
			});

			var regarding = response.Entity.GetAttributeValue<EntityReference>("regardingobjectid");

			return regarding != null
				&& new CrmEntityPermissionProvider().TryAssert(
					serviceContext,
					CrmEntityPermissionRight.Read,
					portalCommentReference,
					regarding: regarding);
		}

		private IEnumerable<IAttachment> GetAnnotationsAsAttachmentCollection(EntityReference regarding, bool respectPermissions = true)
		{
			// Retrieve attachments
			var annotationDataAdapter = new AnnotationDataAdapter(_dependencies);
			var annotationCollection = annotationDataAdapter.GetAnnotations(regarding, respectPermissions: respectPermissions, privacy: AnnotationPrivacy.Any);

			// Filter and project into Attachment object
			return annotationCollection.Where(annotation => EntityContainsRequiredAnnotationFields(annotation.Entity.Attributes)).Select(annotation =>
			{
				var entity = annotation.Entity;
				var fileName = GetFieldValue(entity, "filename");
				var fileType = GetFieldValue(entity, "mimetype");
				ulong fileSize;
				if (!ulong.TryParse(GetFieldValue(entity, "filesize"), out fileSize))
				{
					fileSize = 0;
				}
				FileSize attachmentSize = new FileSize(fileSize);

				// Create CrmAnnotationFile for URL generation
				var crmAnnotationFile = new CrmAnnotationFile(fileName, fileType, new byte[0]);
				crmAnnotationFile.Annotation = new Entity("annotation")
				{
					Id = Guid.Parse(GetFieldValue(entity, "annotationid"))
				};

				var activityAttachment = new Attachment
				{
					AttachmentContentType = fileType,
					AttachmentFileName = fileName,
					AttachmentIsImage = (new List<string> { "image/jpeg", "image/gif", "image/png" }).Contains(fileType),
					AttachmentSize = attachmentSize,
					AttachmentSizeDisplay = attachmentSize.ToString(),
					AttachmentUrl = crmAnnotationFile.Annotation.GetFileAttachmentUrl(_dependencies.GetWebsite())
				};

				return activityAttachment;
			});
		}

	    private bool EntityContainsRequiredAnnotationFields(AttributeCollection attributeCollection)
	    {
	        return attributeCollection.Contains("annotationid") && attributeCollection.Contains("filename") &&
	               attributeCollection.Contains("mimetype") && attributeCollection.Contains("filesize");
	    }

		private IActivityCollection FetchActivities(EntityReference regarding, int page = 1, int pageSize = DefaultMaxPageSize, EntityMetadata entityMetadata = null)
		{
			var viewConfiguration = new ViewConfiguration("activitypointer", "savedqueryid", PortalTimelineSavedQueryId, pageSize);
			viewConfiguration.EnableEntityPermissions = true;

			var viewAdapter = new ViewDataAdapter(viewConfiguration, _dependencies,
				page, filter: regarding.Id.ToString(), regarding: regarding);

			viewAdapter.FilterCollection.Add(new Filter
			{
				Type = LogicalOperator.And,
				Conditions = new List<Condition>
				{
					new Condition("regardingobjectid", ConditionOperator.Equal, regarding.Id)
				}
			});

			var activityPointerCollection = viewAdapter.FetchEntities();

			var activities = activityPointerCollection.Records.Select(activityPointer => new Activity(activityPointer, regarding));
			return new ActivityCollection(activities, activityPointerCollection.TotalRecordCount);
		}
		
		/// <summary>
		/// This is used to retrieve the field value from the Annotation fields on the entity.
		/// </summary>
		/// <param name="fieldName"></param>
		/// <returns></returns>
		protected string GetFieldValue(Entity entity, string fieldName)
		{
			return entity.Attributes[fieldName].ToString();
		}


		/// <summary>
		/// Retrieves an entity reference to the CRM user from the regarding entity and packs it in an ActivtyParty
		/// </summary>
		/// <param name="regarding">The regarding entity for the activity..</param>
		/// <param name="field">The field in the regarding entity containing the CRM user's contact reference</param>
		/// <returns></returns>
		public Entity GetCRMUserActivityParty(EntityReference regarding, string field)
		{
			var serviceContext = _dependencies.GetServiceContext();

			var ownerResponse = serviceContext.RetrieveSingle(regarding, new ColumnSet(field));

			var owner = ownerResponse?.GetAttributeValue<EntityReference>(field);

			if (owner == null)
			{
				return null;
			}

			var ownerActivityParty = new Entity("activityparty");
			ownerActivityParty["partyid"] = owner;
			return ownerActivityParty;
		}

		/// <summary>
		/// Creates a Portal Comment entity, as well Annotation entities for any attachments
		/// </summary>
		/// <param name="portalComment"></param>
		/// <returns></returns>
		public PortalCommentCreateResult CreatePortalComment(PortalComment portalComment)
		{
			var serviceContext = _dependencies.GetServiceContext();
			var serviceContextForWrite = _dependencies.GetServiceContextForWrite();

			PortalCommentCreateResult result = null;

			var entityPermissionProvider = new CrmEntityPermissionProvider();
			result = new PortalCommentCreateResult(entityPermissionProvider, serviceContext, portalComment.Regarding);

			if (result.PermissionsExist && result.PermissionGranted)
			{
				var acceptMimeTypes = AnnotationDataAdapter.GetAcceptRegex(portalComment.AttachmentSettings.AcceptMimeTypes);
				var acceptExtensionTypes = AnnotationDataAdapter.GetAcceptRegex(portalComment.AttachmentSettings.AcceptExtensionTypes);
				if (portalComment.FileAttachments != null)
				{
					foreach (var attachment in portalComment.FileAttachments)
					{
						if (!(acceptExtensionTypes.IsMatch(Path.GetExtension(attachment.FileName).ToLower()) ||
								acceptMimeTypes.IsMatch(attachment.MimeType)))
						{
							throw new AnnotationException(portalComment.AttachmentSettings.RestrictMimeTypesErrorMessage);
						}
					}
				}

				var owner = portalComment.To?.GetAttributeValue<EntityReference>("partyid");

				var entity = new Entity("adx_portalcomment");

				entity.SetAttributeValue("description", portalComment.Description);
				entity.SetAttributeValue("regardingobjectid", portalComment.Regarding);
				entity.SetAttributeValue("regardingobjecttypecode", portalComment.Regarding.LogicalName);
				entity.SetAttributeValue("from", new Entity[] { portalComment.From });
				entity.SetAttributeValue("adx_portalcommentdirectioncode", new OptionSetValue((int)portalComment.DirectionCode));
				
				if (owner != null)
				{
					entity.SetAttributeValue("ownerid", owner);

					if (!string.Equals(owner.LogicalName, "team", StringComparison.OrdinalIgnoreCase))
					{
						entity.SetAttributeValue("to", new Entity[] { portalComment.To });
					}
				}

				// Create adx_portalcomment but skip cache invalidation.
				var id = (serviceContext as IOrganizationService).ExecuteCreate(entity, RequestFlag.ByPassCacheInvalidation);

				portalComment.ActivityId = entity.Id = id;
				portalComment.Entity = entity;

				// Can only change state code value after entity creation
				entity.SetAttributeValue("statecode", new OptionSetValue((int)portalComment.StateCode));
				entity.SetAttributeValue("statuscode", new OptionSetValue((int)portalComment.StatusCode));

				// Explicitly include the activityid, this way the Cache will know to add dependency on "activitypointer" for this "adx_portalcomment" entity.
				entity.SetAttributeValue("activityid", id);

				(serviceContext as IOrganizationService).ExecuteUpdate(entity);
				
				if (portalComment.FileAttachments != null)
				{
					// permission for Portal Comment implies permission for attachment to Portal Comment
					portalComment.AttachmentSettings.RespectPermissions = false;

					foreach (IAnnotationFile attachment in portalComment.FileAttachments)
					{
						IAnnotation annotation = new Annotation
						{
							Subject = string.Empty,
							NoteText = string.Empty,
							Regarding = portalComment.Entity.ToEntityReference(),
							FileAttachment = attachment
						};

						IAnnotationDataAdapter da = new AnnotationDataAdapter(_dependencies);
						da.CreateAnnotation(annotation, portalComment.AttachmentSettings);
					}
				}
			}

			return result;
		}
	}
}
