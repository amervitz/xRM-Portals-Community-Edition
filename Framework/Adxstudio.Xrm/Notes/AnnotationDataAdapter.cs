/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Notes
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Net;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Web;
	using System.Web.Mvc;
	using System.IO;
	using Microsoft.Crm.Sdk.Messages;
	using Azure;
	using Azure.Storage.Blobs;
	using Azure.Storage.Blobs.Models;
	using Azure.Storage.Sas;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Portal.Configuration;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Client;
	using Microsoft.Xrm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Metadata;
	using Microsoft.Xrm.Sdk.Query;
	using Newtonsoft.Json;
	using Filter = Adxstudio.Xrm.Services.Query.Filter;
	using Adxstudio.Xrm.Cms;
	using Adxstudio.Xrm.Cms.Replication;
	using Adxstudio.Xrm.Metadata;
	using Adxstudio.Xrm.Security;
	using Adxstudio.Xrm.Services.Query;
	using Adxstudio.Xrm.Text;
	using Adxstudio.Xrm.Web.Handlers;
	using Adxstudio.Xrm.ContentAccess;
	using Adxstudio.Xrm.Core.Flighting;
	using Adxstudio.Xrm.Services;

	public class AnnotationDataAdapter : IAnnotationDataAdapter
	{
		public const string StorageContainerSetting = "FileStorage/CloudStorageContainerName";
		public const string StorageAccountSetting = "FileStorage/CloudStorageAccount";

		private const int DefaultPageSize = 10;
		private const int DefaultMaxPageSize = 50;

		private readonly string _containerName;
		private readonly IDataAdapterDependencies _dependencies;

		private static readonly string[] _allowDisplayInlineContentTypes = {
			"image/gif",			//.gif
			"image/jpeg",			//.jpeg, .jpg
			"image/png",			//.png
			"image/tiff",			//.tiff ???
			"image/bmp",			//.bmp
			"image/x-icon"			//.ico
		};

		public AnnotationDataAdapter(IDataAdapterDependencies dependencies)
		{
			_dependencies = dependencies;
			_containerName = GetStorageContainerName(dependencies.GetServiceContext());
		}

		public static bool TryCreateStorageClient(string connectionString, out BlobServiceClient client)
		{
			client = null;
			if (string.IsNullOrWhiteSpace(connectionString)) return false;
			try
			{
				client = new BlobServiceClient(connectionString);
				return true;
			}
			catch (ArgumentException) { return false; }
			catch (FormatException) { return false; }
		}

		public static BlobServiceClient GetStorageAccount(OrganizationServiceContext context)
		{
			var cloudStorageDetails = context.GetSettingValueByName(StorageAccountSetting);
			BlobServiceClient storageAccount;
			TryCreateStorageClient(cloudStorageDetails, out storageAccount);
			return storageAccount;
		}

		public static string GetStorageContainerName(OrganizationServiceContext context)
		{
			var containerName = context.GetSettingValueByName(StorageContainerSetting);
			return (string.IsNullOrEmpty(containerName)
				? ((WhoAmIResponse)context.Execute(new WhoAmIRequest())).OrganizationId.ToString("N")
				: containerName).ToLowerInvariant();
		}

		public IAnnotation GetAnnotation(Guid id)
		{
			var context = _dependencies.GetServiceContext();
			var fetch = new Fetch();
			var fetchEntity = new FetchEntity("annotation")
			{
				Attributes = new List<FetchAttribute>
				{
					new FetchAttribute("annotationid"),
					new FetchAttribute("notetext"),
					new FetchAttribute("isdocument"),
					new FetchAttribute("subject"),
					new FetchAttribute("createdon"),
					new FetchAttribute("createdby"),
					new FetchAttribute("modifiedon"),
					new FetchAttribute("filename"),
					new FetchAttribute("filesize"),
					new FetchAttribute("mimetype"),
					new FetchAttribute("objectid"),
					new FetchAttribute("objecttypecode")
				},
				Orders = new List<Order> { new Order("createdon") },
				Filters = new List<Filter>
				{
					new Filter
					{
						Type = LogicalOperator.And,
						Conditions = new List<Condition>
						{
							new Condition("annotationid", ConditionOperator.Equal, id)
						}
					}
				}
			};
			fetch.Entity = fetchEntity;

			var response = (RetrieveMultipleResponse)context.Execute(fetch.ToRetrieveMultipleRequest());
			var note = response.EntityCollection.Entities.FirstOrDefault();

			if (note == null) return null;

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Note, HttpContext.Current, "read_annotation", 1, note.ToEntityReference(), "read");
			}

			return GetAnnotation(note);
		}

		public IAnnotation GetAnnotation(Entity entity)
		{
			return new Annotation(entity, entity.GetAttributeValue<EntityReference>("objectid"),
				() => GetAnnotationFile(entity, entity.GetAttributeValue<Guid>("annotationid")));
		}

		public void Download(HttpContextBase context, Entity entity, Entity webfile = null)
		{
			var note = GetAnnotation(entity);
			var storageAccount = GetStorageAccount(_dependencies.GetServiceContext());
			if (storageAccount != null && note.FileAttachment is AzureAnnotationFile)
			{
				DownloadFromAzure(context, note);
			}
			else
			{
				DownloadFromCRM(context, note, webfile);
			}
		}

		public ActionResult DownloadAction(HttpResponseBase response, Entity entity)
		{
			var note = GetAnnotation(entity);
			var storageAccount = GetStorageAccount(_dependencies.GetServiceContext());
			if (storageAccount != null && note.FileAttachment is AzureAnnotationFile)
			{
				return DownloadFromAzureAction(note);
			}
			return DownloadFromCRMAction(response, note);
		}

		public IAnnotationCollection GetAnnotations(EntityReference regarding, List<Order> orders = null, int page = 1,
			int pageSize = DefaultPageSize, AnnotationPrivacy privacy = AnnotationPrivacy.Private | AnnotationPrivacy.Web, 
			EntityMetadata entityMetadata = null, bool respectPermissions = true)
		{
			if (pageSize < 0)
			{
				pageSize = DefaultPageSize;
			}

			if (pageSize > DefaultMaxPageSize)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("pageSize={0} is greater than the allowed maximum page size of {1}. Page size has been constrained to {1}.", pageSize, DefaultMaxPageSize));
				pageSize = DefaultMaxPageSize;
			}

			var fetch = BuildAnnotationsQuery(regarding, orders, privacy, entityMetadata);

			AddPaginationToFetch(fetch, fetch.PagingCookie, page, pageSize, true);

			if (respectPermissions) AddPermissionFilterToFetch(fetch, _dependencies.GetServiceContext(), CrmEntityPermissionRight.Read, regarding);

			var notes = FetchAnnotations(fetch, regarding);

			return notes;
		}

		public IAnnotationCollection GetDocuments(EntityReference regarding, bool respectPermissions = true, string webPrefix = null)
		{
			var fetch = BuildAnnotationsQuery(regarding, privacy: AnnotationPrivacy.Any, webPrefix: webPrefix);
			fetch.Entity.Filters.First().Conditions.Add(new Condition("isdocument", ConditionOperator.Equal, true));
			if (respectPermissions) AddPermissionFilterToFetch(fetch, _dependencies.GetServiceContext(), CrmEntityPermissionRight.Read, regarding);
			var notes = FetchAnnotations(fetch, regarding);

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Note, HttpContext.Current, "GetDocuments", "Exposing annotations", notes.Count(), "annotation", "read");
			}
			return notes;
		}

		public IAnnotationResult CreateAnnotation(IAnnotation note, IAnnotationSettings settings = null)
		{
			var serviceContext = _dependencies.GetServiceContext();

			if (settings == null)
			{
				settings = new AnnotationSettings(serviceContext);
			}
			
			var storageAccount = GetStorageAccount(serviceContext);
			if (settings.StorageLocation == StorageLocation.AzureBlobStorage && storageAccount == null)
			{
				settings.StorageLocation = StorageLocation.CrmDocument;
			}

			AnnotationCreateResult result = null;

			if (settings.RespectPermissions)
			{
				var entityPermissionProvider = new CrmEntityPermissionProvider();
				result = new AnnotationCreateResult(entityPermissionProvider, serviceContext, note.Regarding);
			}

			// ReSharper disable once PossibleNullReferenceException
			if (!settings.RespectPermissions ||
				(result.PermissionsExist && result.PermissionGranted))
			{
				var entity = new Entity("annotation");

				if (note.Owner != null)
				{
					entity.SetAttributeValue("ownerid", note.Owner);
				}

				entity.SetAttributeValue("subject", note.Subject);
				entity.SetAttributeValue("notetext", note.NoteText);
				entity.SetAttributeValue("objectid", note.Regarding);
				entity.SetAttributeValue("objecttypecode", note.Regarding.LogicalName);

				if (note.FileAttachment != null)
				{
					var acceptMimeTypes = AnnotationDataAdapter.GetAcceptRegex(settings.AcceptMimeTypes);
					var acceptExtensionTypes = AnnotationDataAdapter.GetAcceptRegex(settings.AcceptExtensionTypes);
					if (!(acceptExtensionTypes.IsMatch(Path.GetExtension(note.FileAttachment.FileName).ToLower()) ||
							acceptMimeTypes.IsMatch(note.FileAttachment.MimeType)))
					{
						throw new AnnotationException(settings.RestrictMimeTypesErrorMessage);
					}

					if (settings.MaxFileSize.HasValue && note.FileAttachment.FileSize > settings.MaxFileSize)
					{
						throw new AnnotationException(settings.MaxFileSizeErrorMessage);
					}

					note.FileAttachment.Annotation = entity;

					switch (settings.StorageLocation)
					{
					case StorageLocation.CrmDocument:
						var crmFile = note.FileAttachment as CrmAnnotationFile;
						if (crmFile == null)
						{
							break;
						}

						if (!string.IsNullOrEmpty(settings.RestrictedFileExtensions))
						{
							var blocked = new Regex(@"\.({0})$".FormatWith(settings.RestrictedFileExtensions.Replace(";", "|")));
							if (blocked.IsMatch(crmFile.FileName))
							{
								throw new AnnotationException(settings.RestrictedFileExtensionsErrorMessage);
							}
						}

						entity.SetAttributeValue("filename", crmFile.FileName);
						entity.SetAttributeValue("mimetype", crmFile.MimeType);
						entity.SetAttributeValue("documentbody", Convert.ToBase64String(crmFile.Document));
						break;
					case StorageLocation.AzureBlobStorage:
						entity.SetAttributeValue("filename", note.FileAttachment.FileName + ".azure.txt");
						entity.SetAttributeValue("mimetype", "text/plain");
						var fileMetadata = new
						{
							Name = note.FileAttachment.FileName,
							Type = note.FileAttachment.MimeType,
							Size = (ulong)note.FileAttachment.FileSize,
							Url = string.Empty
						};
						entity.SetAttributeValue("documentbody",
							Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileMetadata, Formatting.Indented))));
						break;
					}
				}

				// Create annotaion but skip cache invalidation.
				var id = (serviceContext as IOrganizationService).ExecuteCreate(entity, RequestFlag.ByPassCacheInvalidation);

				if (result != null) result.Annotation = note;

				note.AnnotationId = entity.Id = id;
				note.Entity = entity;

				if (note.FileAttachment is AzureAnnotationFile && settings.StorageLocation == StorageLocation.AzureBlobStorage)
				{
					var container = GetBlobContainer(storageAccount, _containerName);

					var azureFile = (AzureAnnotationFile)note.FileAttachment;

					azureFile.BlockBlob = UploadBlob(azureFile, container, note.AnnotationId);

					var fileMetadata = new
					{
						Name = azureFile.FileName,
						Type = azureFile.MimeType,
						Size = (ulong)azureFile.FileSize,
						Url = azureFile.BlockBlob.Uri.AbsoluteUri
					};

					var updatedNote = new Entity("annotation")
					{
						Id = entity.Id,
						["documentbody"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileMetadata, Formatting.Indented)))
					};

					(serviceContext as IOrganizationService).ExecuteUpdate(updatedNote, RequestFlag.ByPassCacheInvalidation);

					// NB: This is basically a hack to support replication. Keys are gathered up and stored during replication, and the
					// actual blob replication is handled here.
					var key = note.AnnotationId.ToString("N");
					if (HttpContext.Current.Application.AllKeys.Contains(NoteReplication.BlobReplicationKey))
					{
						var replication =
							HttpContext.Current.Application[NoteReplication.BlobReplicationKey] as Dictionary<string, Tuple<Guid, Guid>[]>;
						if (replication != null && replication.ContainsKey(key))
						{
							CopyBlob(note, replication[key]);
							replication.Remove(key);
						}
					}
				} 
			}

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Note, HttpContext.Current, "create_note", 1, note.Entity.ToEntityReference(), "create");
			}

			return result;
		}

		public IAnnotationResult CreateAnnotation(EntityReference regarding, string subject, string noteText)
		{
			IAnnotation annotation = new Annotation
			{
				Subject = subject,
				NoteText = noteText,
				Regarding = regarding
			};
			return CreateAnnotation(annotation);
		}

		public IAnnotationResult CreateAnnotation(EntityReference regarding, string subject, string noteText,
			HttpPostedFileBase file)
		{
			IAnnotation annotation = new Annotation
			{
				Subject = subject,
				NoteText = noteText,
				Regarding = regarding,
				FileAttachment = new CrmAnnotationFile(file)
			};
			return CreateAnnotation(annotation);
		}

		public IAnnotationResult CreateAnnotation(EntityReference regarding, string subject, string noteText, string fileName,
			string contentType, byte[] content)
		{
			IAnnotation annotation = new Annotation
			{
				Subject = subject,
				NoteText = noteText,
				Regarding = regarding,
				FileAttachment = new CrmAnnotationFile(fileName, contentType, content)
			};
			return CreateAnnotation(annotation);
		}
		
		public IAnnotationResult UpdateAnnotation(IAnnotation note, IAnnotationSettings settings = null)
		{
			var serviceContext = _dependencies.GetServiceContext();
			var serviceContextForWrite = _dependencies.GetServiceContextForWrite();

			if (settings == null)
			{
				settings = new AnnotationSettings(serviceContext);
			}
			
			var storageAccount = GetStorageAccount(serviceContext);
			if (settings.StorageLocation == StorageLocation.AzureBlobStorage && storageAccount == null)
			{
				settings.StorageLocation = StorageLocation.CrmDocument;
			}

			AnnotationUpdateResult result = null;

			if (settings.RespectPermissions)
			{
				var entityPermissionProvider = new CrmEntityPermissionProvider();
				result = new AnnotationUpdateResult(note, entityPermissionProvider, serviceContext);
			}

			var isPostedByCurrentUser = false;
			var noteContact = AnnotationHelper.GetNoteContact(note.Entity.GetAttributeValue<string>("subject"));
			var currentUser = _dependencies.GetPortalUser();
			if (noteContact != null && currentUser != null && currentUser.LogicalName == "contact" &&
				currentUser.Id == noteContact.Id)
			{
				isPostedByCurrentUser = true;
			}

			// ReSharper disable once PossibleNullReferenceException
			if (!settings.RespectPermissions || (result.PermissionsExist && result.PermissionGranted && isPostedByCurrentUser))
			{
				var entity = new Entity("annotation")
				{
					Id = note.AnnotationId
				};

				entity.SetAttributeValue("notetext", note.NoteText);
				entity.SetAttributeValue("subject", note.Subject);
				entity.SetAttributeValue("isdocument", note.FileAttachment != null);
				
				if (note.FileAttachment != null)
				{
					var accept = GetAcceptRegex(settings.AcceptMimeTypes);
					if (!accept.IsMatch(note.FileAttachment.MimeType))
					{
						throw new AnnotationException(settings.RestrictMimeTypesErrorMessage);
					}

					if (settings.MaxFileSize.HasValue && note.FileAttachment.FileSize > settings.MaxFileSize)
					{
						throw new AnnotationException(settings.MaxFileSizeErrorMessage);
					}

					note.FileAttachment.Annotation = entity;
					
					switch (settings.StorageLocation)
					{
					case StorageLocation.CrmDocument:
						var crmFile = note.FileAttachment as CrmAnnotationFile;
						if (crmFile == null || crmFile.Document == null || crmFile.Document.Length == 0)
						{
							break;
						}

						if (!string.IsNullOrEmpty(settings.RestrictedFileExtensions))
						{
							var blocked = new Regex(@"\.({0})$".FormatWith(settings.RestrictedFileExtensions.Replace(";", "|")));
							if (blocked.IsMatch(crmFile.FileName))
							{
								throw new AnnotationException(settings.RestrictedFileExtensionsErrorMessage);
							}
						}

						entity.SetAttributeValue("filename", crmFile.FileName);
						entity.SetAttributeValue("mimetype", crmFile.MimeType);
						entity.SetAttributeValue("documentbody", Convert.ToBase64String(crmFile.Document));
						break;
					case StorageLocation.AzureBlobStorage:
						entity.SetAttributeValue("filename", note.FileAttachment.FileName + ".azure.txt");
						entity.SetAttributeValue("mimetype", "text/plain");
						var fileMetadata = new
						{
							Name = note.FileAttachment.FileName,
							Type = note.FileAttachment.MimeType,
							Size = (ulong)note.FileAttachment.FileSize,
							Url = string.Empty
						};
						entity.SetAttributeValue("documentbody",
							Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileMetadata, Formatting.Indented))));
						break;
					}
				}

				serviceContextForWrite.Attach(entity);
				serviceContextForWrite.UpdateObject(entity);
				serviceContextForWrite.SaveChanges();

				if (note.FileAttachment is AzureAnnotationFile && settings.StorageLocation == StorageLocation.AzureBlobStorage)
				{
					var azureFile = note.FileAttachment as AzureAnnotationFile;

					if (azureFile.GetFileStream() != null)
					{
						var container = GetBlobContainer(storageAccount, _containerName);

						var storedName = note.Entity.GetAttributeValue<string>("filename");
						var oldName = storedName?.EndsWith(".azure.txt") == true
							? storedName.Substring(0, storedName.Length - ".azure.txt".Length)
							: storedName;
						var oldBlob = container.GetBlobClient("{0:N}/{1}".FormatWith(entity.Id, oldName));
						oldBlob.DeleteIfExists();

						azureFile.BlockBlob = UploadBlob(azureFile, container, note.AnnotationId);

						var fileMetadata = new
						{
							Name = azureFile.FileName,
							Type = azureFile.MimeType,
							Size = (ulong)azureFile.FileSize,
							Url = azureFile.BlockBlob.Uri.AbsoluteUri
						};
						entity.SetAttributeValue("documentbody",
							Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileMetadata, Formatting.Indented))));
						serviceContextForWrite.UpdateObject(entity);
						serviceContextForWrite.SaveChanges();
					}
				}
			}

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Note, HttpContext.Current, "edit_note", 1, new EntityReference("annotation", note.AnnotationId), "edit");
			}

			return result;
		}

		public IAnnotationResult DeleteAnnotation(IAnnotation note, IAnnotationSettings settings = null)
		{
			var serviceContext = _dependencies.GetServiceContext();
			var serviceContextForWrite = _dependencies.GetServiceContextForWrite();

			if (settings == null)
			{
				settings = new AnnotationSettings(serviceContext);
			}

			AnnotationDeleteResult result = null;

			if (settings.RespectPermissions)
			{
				var entityPermissionProvider = new CrmEntityPermissionProvider();
				result = new AnnotationDeleteResult(note, entityPermissionProvider, serviceContext);
			}

			var isPostedByCurrentUser = false;
			var noteContact = AnnotationHelper.GetNoteContact(note.Subject);
			var currentUser = _dependencies.GetPortalUser();
			if (noteContact != null && currentUser != null && currentUser.LogicalName == "contact" &&
				currentUser.Id == noteContact.Id)
			{
				isPostedByCurrentUser = true;
			}

			// ReSharper disable once PossibleNullReferenceException
			if (!settings.RespectPermissions || (result.PermissionGranted && isPostedByCurrentUser))
			{
				var entityToDelete = serviceContextForWrite.RetrieveSingle(
					"annotation",
					"annotationid",
					note.AnnotationId,
					FetchAttribute.All);

				serviceContextForWrite.DeleteObject(entityToDelete);
				serviceContextForWrite.SaveChanges();

				var storageAccount = GetStorageAccount(serviceContext);
				if (storageAccount != null && note.FileAttachment is AzureAnnotationFile)
				{
					var azureFile = note.FileAttachment as AzureAnnotationFile;
					azureFile.BlockBlob.DeleteIfExists();
				}
			}

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Note, HttpContext.Current, "delete_note", 1, new EntityReference("annotation", note.AnnotationId), "delete");
			}

			return result;
		}

		private IAnnotationCollection FetchAnnotations(Fetch fetch, EntityReference regarding, bool permissionDenied = false)
		{
			if (fetch == null || permissionDenied)
			{
				return AnnotationCollection.Empty(permissionDenied);
			}

			var serviceContext = _dependencies.GetServiceContext();
			var response = (RetrieveMultipleResponse)serviceContext.Execute(fetch.ToRetrieveMultipleRequest());

			if (!string.IsNullOrEmpty(response.EntityCollection.PagingCookie))
			{
				fetch.PagingCookie = response.EntityCollection.PagingCookie;
			}

			IEnumerable<Entity> entities = response.EntityCollection.Entities;
			
			var notes = entities.Select(e => new Annotation(e, regarding, () =>
				GetAnnotationFile(e, e.GetAttributeValue<Guid>("annotationid"))));

			return new AnnotationCollection(notes, response.EntityCollection.TotalRecordCount);
		}

		private IAnnotationFile GetAnnotationFile(Entity note, Guid id)
		{
			var isDocument = note.GetAttributeValue<bool>("isdocument");
			if (!isDocument) return null;

			var fileName = note.GetAttributeValue<string>("filename");

			var storageAccount = GetStorageAccount(_dependencies.GetServiceContext());
			var regex = new Regex(@"\.azure\.txt$");
			var azure = regex.IsMatch(fileName);

			var file = azure ? new AzureAnnotationFile() as IAnnotationFile : new CrmAnnotationFile();

			file.SetAnnotation(() => GetAnnotationWithDocument(id));

			if (azure)
			{
				var blobFileName = regex.Replace(fileName, string.Empty);
				var blobFile = file as AzureAnnotationFile;
				if (storageAccount != null)
				{
					BlobProperties properties;
					blobFile.BlockBlob = GetBlockBlob(storageAccount, id, blobFileName, out properties);
					blobFile.BlobProperties = properties;

					// A non-null set of properties is what it means for the blob to exist, so the
					// attributes already fetched answer that without a further request.
					if (properties != null)
					{
						blobFile.FileName = blobFileName;
						blobFile.FileSize = new FileSize(Convert.ToUInt64(properties.ContentLength));
						blobFile.MimeType = properties.ContentType;
					}
				}
			}
			else
			{
				var crmFile = file as CrmAnnotationFile;
				crmFile.FileName = fileName;
				var size = note.GetAttributeValue<int>("filesize");
				crmFile.FileSize = new FileSize(size > 0 ? Convert.ToUInt64(size) : 0);
				crmFile.MimeType = note.GetAttributeValue<string>("mimetype");
				crmFile.SetDocument(() => GetFileDocument(file));
			}


			return file;
		}

		private BlobClient GetBlockBlob(BlobServiceClient storageAccount, Guid id, string fileName, out BlobProperties properties)
		{
			properties = null;
			if (storageAccount != null)
			{
				var container = GetBlobContainer(storageAccount, _containerName);
				var blockBlob = container.GetBlobClient("{0:N}/{1}".FormatWith(id, fileName));

				// Fetching the attributes reports a missing blob as a 404, so this single request
				// establishes both whether the blob exists and its properties.
				try
				{
					properties = blockBlob.GetProperties().Value;
				}
				catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.NotFound)
				{
					properties = null;
				}

				return blockBlob;
			}
			return null;
		}

		private Entity GetAnnotationWithDocument(Guid id)
		{
			var fetch = new Fetch
			{
				Entity =
					new FetchEntity("annotation")
					{
						Filters = new[] { new Filter { Conditions = new[] { new Condition("annotationid", ConditionOperator.Equal, id) } } }
					}
			};

			return _dependencies.GetServiceContext().RetrieveSingle(fetch, enforceFirst: true);
		}

		private static byte[] GetFileDocument(IAnnotationFile file)
		{
			var body = file.Annotation.GetAttributeValue<string>("documentbody");
			return string.IsNullOrWhiteSpace(body) ? new byte[] { } : Convert.FromBase64String(body);
		}

		public static BlobContainerClient GetBlobContainer(BlobServiceClient account, string containerName)
		{
			var container = account.GetBlobContainerClient(containerName);
			container.CreateIfNotExists();
			return container;
		}

		private Fetch BuildAnnotationsQuery(EntityReference regarding, List<Order> orders = null,
			AnnotationPrivacy privacy = AnnotationPrivacy.Private | AnnotationPrivacy.Web, EntityMetadata entityMetadata = null, string webPrefix = null)
		{
			if (entityMetadata == null)
			{
				var serviceContext = _dependencies.GetServiceContext();
				entityMetadata = serviceContext.GetEntityMetadata(regarding.LogicalName, EntityFilters.All);
			}
			var objectTypeCode = entityMetadata.ObjectTypeCode;
			var user = _dependencies.GetPortalUser();
			var currentContactId = user == null || user.LogicalName != "contact" ? Guid.Empty : user.Id;

			var fetch = new Fetch();

			var filters = new List<Filter>();


			if (webPrefix == null && privacy != AnnotationPrivacy.Any)
			{
				if ((privacy & AnnotationPrivacy.Web) == AnnotationPrivacy.Web)
				{
					filters.Add(new Filter
					{
						Type = LogicalOperator.And,
						Conditions = new List<Condition>
						{
							new Condition("notetext", ConditionOperator.Like, string.Format("%{0}%", AnnotationHelper.WebAnnotationPrefix)),
							new Condition("subject", ConditionOperator.NotLike,
								string.Format("%{0}%", AnnotationHelper.PrivateAnnotationPrefix))
						}
					});
				}
				if ((privacy & AnnotationPrivacy.Public) == AnnotationPrivacy.Public)
				{
					filters.Add(new Filter
					{
						Type = LogicalOperator.And,
						Conditions = new List<Condition>
						{
							new Condition("notetext", ConditionOperator.Like, string.Format("%{0}%", AnnotationHelper.PublicAnnotationPrefix)),
							new Condition("subject", ConditionOperator.NotLike,
								string.Format("%{0}%", AnnotationHelper.PrivateAnnotationPrefix))
						}
					});
				}
				if ((privacy & AnnotationPrivacy.Private) == AnnotationPrivacy.Private)
				{
					filters.Add(new Filter
					{
						Type = LogicalOperator.And,
						Conditions = new List<Condition>
						{
							new Condition("subject", ConditionOperator.Like, string.Format("%{0}%", currentContactId.ToString("D"))),
							new Condition("subject", ConditionOperator.Like, string.Format("%{0}%", AnnotationHelper.PrivateAnnotationPrefix))
						}
					});
				}
			}

			if (webPrefix != null)
			{
				filters.Add(new Filter
				{
					Type = LogicalOperator.And,
					Conditions = new List<Condition>
						{
							new Condition("notetext", ConditionOperator.Like, string.Format("%{0}%", webPrefix))
						}
				});
			}

			var fetchEntity = new FetchEntity("annotation")
			{
				Attributes = new List<FetchAttribute>
				{
					new FetchAttribute("annotationid"),
					new FetchAttribute("notetext"),
					new FetchAttribute("isdocument"),
					new FetchAttribute("subject"),
					new FetchAttribute("createdon"),
					new FetchAttribute("createdby"),
					new FetchAttribute("modifiedon"),
					new FetchAttribute("filename"),
					new FetchAttribute("filesize"),
					new FetchAttribute("mimetype"),
					new FetchAttribute("objectid")
				},
				Orders = orders == null || !orders.Any() ? new List<Order> { new Order("createdon") } : orders,
				Filters = new List<Filter>
				{
					new Filter
					{
						Type = LogicalOperator.And,
						Conditions = new List<Condition>
						{
							new Condition("objectid", ConditionOperator.Equal, regarding.Id),
							new Condition("objecttypecode", ConditionOperator.Equal, objectTypeCode)
						},
						Filters = new List<Filter>
						{
							new Filter
							{
								Type = LogicalOperator.Or,
								Filters = filters
							}
						}
					}
				}
			};
			fetch.Entity = fetchEntity;
			return fetch;
		}

		protected void AddPermissionFilterToFetch(Fetch fetch, OrganizationServiceContext serviceContext, CrmEntityPermissionRight right, EntityReference regarding = null)
		{
			var crmEntityPermissionProvider = new CrmEntityPermissionProvider();

			crmEntityPermissionProvider.TryApplyRecordLevelFiltersToFetch(serviceContext, right, fetch, regarding);

			// Apply Content Access Level filtering
			var contentAccessLevelProvider = new ContentAccessLevelProvider();
			contentAccessLevelProvider.TryApplyRecordLevelFiltersToFetch(right, fetch);

			// Apply Product filtering
			var productAccessProvider = new ProductAccessProvider();
			productAccessProvider.TryApplyRecordLevelFiltersToFetch(right, fetch);
		}

		private static void AddPaginationToFetch(Fetch fetch, string cookie, int page, int count, bool returnTotalRecordCount)
		{
			if (cookie != null)
			{
				fetch.PagingCookie = cookie;
			}

			fetch.PageNumber = page;

			fetch.PageSize = count;

			fetch.ReturnTotalRecordCount = returnTotalRecordCount;
		}

		private static void DownloadFromCRM(HttpContextBase context, IAnnotation note, Entity webfile)
		{
			if (note == null)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				return;
			}

			var crmFile = note.FileAttachment as CrmAnnotationFile;

			if (crmFile == null || crmFile.Document == null || crmFile.Document.Length == 0)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NoContent;
				return;
			}

			var data = crmFile.Document;
			var eTag = Utility.ComputeETag(data);

			if (!string.IsNullOrWhiteSpace(eTag))
			{
				context.Response.Cache.SetETag(eTag);
			}

			var defaultCacheability = context.User.Identity.IsAuthenticated ? HttpCacheability.Private : HttpCacheability.Public;

			SetCachePolicy(context.Response, defaultCacheability);

			var modifiedOn = crmFile.Annotation.GetAttributeValue<DateTime?>("modifiedon");
			if (modifiedOn != null)
			{
				context.Response.Cache.SetLastModified(modifiedOn.Value);
			}

			SetResponseParameters(context, crmFile.Annotation, webfile, data);

			var notModified = IsNotModified(context, eTag, modifiedOn);

			if (notModified)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotModified;
			}
			else
			{
				Utility.Write(context.Response, data);
			}
		}

		private static void DownloadFromAzure(HttpContextBase context, IAnnotation note)
		{
			if (note == null)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				return;
			}

			var azureFile = note.FileAttachment as AzureAnnotationFile;

			// An attachment whose attributes were never fetched reports a zero length, so this covers
			// a missing blob as well as an empty one without a further request.
			if (azureFile == null || azureFile.BlockBlob == null || azureFile.BlobProperties.ContentLength <= 0)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NoContent;
				return;
			}

			var downloadUri = azureFile.BlockBlob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(55));

			context.Response.Redirect(downloadUri.AbsoluteUri, true);
		}

		private static bool IsNotModified(HttpContextBase context, string eTag, DateTime? modifiedOn)
		{
			var ifNoneMatch = context.Request.Headers["If-None-Match"];
			DateTime ifModifiedSince;

			// check the etag and last modified

			if (ifNoneMatch != null && ifNoneMatch == eTag)
			{
				return true;
			}

			return modifiedOn != null
				&& DateTime.TryParse(context.Request.Headers["If-Modified-Since"], out ifModifiedSince)
				&& ifModifiedSince.ToUniversalTime() >= modifiedOn.Value.ToUniversalTime();
		}

		private static void SetResponseParameters(HttpContextBase context,
			Entity annotation, Entity webfile, ICollection<byte> data)
		{
			context.Response.StatusCode = (int)HttpStatusCode.OK;
			context.Response.ContentType = annotation.GetAttributeValue<string>("mimetype");

			var contentDispositionText = "attachment";

			if (_allowDisplayInlineContentTypes.Any(contentType => contentType.Equals(context.Response.ContentType, StringComparison.OrdinalIgnoreCase)))
			{
				contentDispositionText = "inline";
			}

			var contentDisposition = new StringBuilder(contentDispositionText);

			AppendFilenameToContentDisposition(annotation, contentDisposition);

			context.Response.AppendHeader("Content-Disposition", contentDisposition.ToString());
			context.Response.AppendHeader("Content-Length", data.Count.ToString(CultureInfo.InvariantCulture));

			if (webfile?.Attributes != null && webfile.Attributes.ContainsKey("adx_alloworigin"))
			{
				var allowOrigin = webfile["adx_alloworigin"] as string;

				Web.Extensions.SetAccessControlAllowOriginHeader(context, allowOrigin);
			}
		}

		private static void SetCachePolicy(HttpResponseBase response, HttpCacheability defaultCacheability)
		{
			var section = PortalCrmConfigurationManager.GetPortalCrmSection();
			var policy = section.CachePolicy.Annotation;

			Utility.SetResponseCachePolicy(policy, response, defaultCacheability);
		}

		private static void AppendFilenameToContentDisposition(Entity annotation, StringBuilder contentDisposition)
		{
			var filename = annotation.GetAttributeValue<string>("filename");

			if (string.IsNullOrEmpty(filename))
			{
				return;
			}

			// Escape any quotes in the filename. (There should rarely if ever be any, but still.)
			var escaped = filename.Replace(@"""", @"\""");
			var encoded = HttpUtility.UrlEncode(escaped, System.Text.Encoding.UTF8);
			// Quote the filename parameter value.
			contentDisposition.AppendFormat(@";filename=""{0}""", encoded);
		}
		
		private static ActionResult DownloadFromCRMAction(HttpResponseBase response, IAnnotation note)
		{
			if (note == null)
			{
				return new HttpStatusCodeResult((int)HttpStatusCode.NotFound);
			}

			var crmFile = note.FileAttachment as CrmAnnotationFile;

			if (crmFile == null)
			{
				return new HttpStatusCodeResult((int)HttpStatusCode.NotFound);
			}

			var contentType = string.IsNullOrEmpty(crmFile.MimeType)
				? "application/octet-stream"
				: crmFile.MimeType;
			var fileName = crmFile.FileName;

			if (!string.IsNullOrEmpty(fileName))
			{
				response.Headers["Content-Disposition"] = "inline; filename={0}".FormatWith(fileName);
			}

			AddCrossOriginAccessHeaders(response);

			return new FileContentResult(crmFile.Document, contentType);
		}

		private static ActionResult DownloadFromAzureAction(IAnnotation note)
		{
			if (note == null)
			{
				return new HttpStatusCodeResult((int)HttpStatusCode.NotFound);
			}

			var azureFile = note.FileAttachment as AzureAnnotationFile;

			// An attachment whose attributes were never fetched reports a zero length, so this covers
			// a missing blob as well as an empty one without a further request.
			if (azureFile == null || azureFile.BlockBlob == null || azureFile.BlobProperties.ContentLength <= 0)
			{
				return new HttpStatusCodeResult((int)HttpStatusCode.NoContent);
			}

			var downloadUri = azureFile.BlockBlob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(55));

			return new RedirectResult(downloadUri.AbsoluteUri, false);
		}

		private static void AddCrossOriginAccessHeaders(HttpResponseBase response)
		{
			response.Headers["Access-Control-Allow-Headers"] = "*";
			response.Headers["Access-Control-Allow-Origin"] = "*";
		}

		private static BlobClient UploadBlob(IAnnotationFile file, BlobContainerClient container, Guid noteId)
		{
			var blob = container.GetBlobClient("{0:N}/{1}".FormatWith(noteId, file.FileName));
			using (var stream = file.GetFileStream())
			{
				blob.Upload(stream, new BlobUploadOptions
				{
					HttpHeaders = new BlobHttpHeaders { ContentType = file.MimeType },

					// An empty set of conditions permits overwriting an existing blob, preserving the
					// replace-in-place behaviour of the previous DeleteIfExists/upload sequence.
					Conditions = new BlobRequestConditions()
				});
			}
			return blob;
		}

		private void CopyBlob(IAnnotation note, IEnumerable<Tuple<Guid, Guid>> newNoteInfos)
		{
			var azureBlob = note.FileAttachment as AzureAnnotationFile;

			if (azureBlob == null)
			{
				return;
			}

			var fileName = azureBlob.FileName;
			var fromBlob = azureBlob.BlockBlob;
			if (fromBlob.Exists())
			{
				var context = _dependencies.GetServiceContextForWrite();
				foreach (var newNoteInfo in newNoteInfos)
				{
					var newNoteId = newNoteInfo.Item2;

					var storageAccount = GetStorageAccount(context);
					var container = GetBlobContainer(storageAccount, _containerName);
					var toBlob = container.GetBlobClient("{0:N}/{1}".FormatWith(newNoteId.ToString("N"), fileName));
					toBlob.DeleteIfExists();

					// Source and destination are in the same account, so the copy is authorized by the
					// account's own credentials without minting a read SAS for the source.
					toBlob.StartCopyFromUri(fromBlob.Uri).WaitForCompletion();
					
					var azureFile = note.FileAttachment as AzureAnnotationFile;

					var entity = new Entity("annotation")
					{
						Id = newNoteId
					};

					var fileMetadata = new
					{
						Name = azureFile.FileName,
						Type = azureFile.MimeType,
						Size = (ulong)azureFile.FileSize,
						Url = toBlob.Uri.AbsoluteUri
					};
					entity.SetAttributeValue("documentbody",
						Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileMetadata, Formatting.Indented))));
					context.Attach(entity);
					context.SaveChanges();
				}
			}
		}

		public static IAnnotationFile CreateFileAttachment(StorageLocation storageLocation = StorageLocation.CrmDocument)
		{
			switch (storageLocation)
			{
			case StorageLocation.AzureBlobStorage:
				return new AzureAnnotationFile();
			default:
				return new CrmAnnotationFile();
			}
		}

		public static IAnnotationFile CreateFileAttachment(HttpPostedFileBase file, StorageLocation storageLocation = StorageLocation.CrmDocument)
		{
			switch (storageLocation)
			{
			case StorageLocation.AzureBlobStorage:
				return new AzureAnnotationFile(file);
			default:
				return new CrmAnnotationFile(file);
			}
		}

		public static IAnnotationFile CreateFileAttachment(string fileName, string contentType, byte[] fileContent, StorageLocation storageLocation = StorageLocation.CrmDocument)
		{
			switch (storageLocation)
			{
			case StorageLocation.AzureBlobStorage:
				return new AzureAnnotationFile(fileName, contentType, fileContent);
			default:
				return new CrmAnnotationFile(fileName, contentType, fileContent);
			}
		}

		public static Regex GetAcceptRegex(string acceptTypes)
		{
			const string acceptRegex =
				@"^(((?<category>[A-Za-z0-9\-]+)/((?<specifictype>[A-Za-z0-9\.\-]+)|(?<anytype>\*)))|(?<any>\*/\*))$";

			if (string.IsNullOrEmpty(acceptTypes))
			{
				return new Regex("$^");
			}

			var acceptInterpreter = new Regex(acceptRegex);
			var acceptSplits = acceptTypes.Split(',');
			var matchers = new List<Regex>();
			foreach (var split in acceptSplits)
			{
				var mimeType = acceptInterpreter.Replace(split.Trim(), match =>
				{
					if (match.Groups["any"].Success)
					{
						return "(.*)";
					}

					if (match.Groups["category"].Success && (match.Groups["anytype"].Success || match.Groups["specifictype"].Success))
					{
						var category = match.Groups["category"].Value.Replace("-", "\\-");
						var specificType = match.Groups["anytype"].Success
							? "(.*)"
							: match.Groups["specifictype"].Value.Replace(".", "\\.").Replace("-", "\\-");
						return string.Format("({0}/{1})", category, specificType);
					}

					return string.Empty;
				});

				if (!string.IsNullOrEmpty(mimeType))
				{
					matchers.Add(new Regex(mimeType));
				}
			}

			if (matchers.Any())
			{
				return new Regex(string.Format("^{0}$", string.Join("|", matchers)));
			}

			throw new Exception("Invalid file types in IAnnotationSettings.AcceptMimeTypes");
		}
	}
}
