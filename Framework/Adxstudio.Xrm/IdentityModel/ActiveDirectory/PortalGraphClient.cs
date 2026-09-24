namespace Adxstudio.Xrm.IdentityModel.ActiveDirectory
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading;
	using System.Threading.Tasks;
	using Newtonsoft.Json;

	/// <summary>
	/// Reads the signed-in user from Microsoft Graph.
	/// </summary>
	/// <remarks>
	/// This replaces the client for the retired Azure AD Graph API. The portal only reads the signed-in user,
	/// so it calls the one Microsoft Graph endpoint it needs rather than taking on the Microsoft Graph SDK.
	/// </remarks>
	public class PortalGraphClient
	{
		/// <summary>
		/// The user properties the portal reads. Microsoft Graph returns only a default subset unless they are selected.
		/// </summary>
		private const string UserProperties = "id,givenName,surname,displayName,mail,otherMails,userPrincipalName,assignedPlans";

		/// <summary>
		/// Shared across clients so that connections are pooled rather than opened per sign-in.
		/// </summary>
		private static readonly HttpClient HttpClient = new HttpClient();

		private readonly Uri meUri;
		private readonly Func<Task<string>> acquireToken;

		/// <summary>
		/// Initializes a new instance of the <see cref="PortalGraphClient" /> class.
		/// </summary>
		/// <param name="graphRoot">The Microsoft Graph root URL, such as https://graph.microsoft.com.</param>
		/// <param name="acquireToken">Acquires the access token that each request is sent with.</param>
		public PortalGraphClient(string graphRoot, Func<Task<string>> acquireToken)
		{
			if (string.IsNullOrWhiteSpace(graphRoot)) throw new ArgumentException("A Microsoft Graph root URL is required.", nameof(graphRoot));
			if (acquireToken == null) throw new ArgumentNullException(nameof(acquireToken));

			var root = new Uri(graphRoot.Trim().TrimEnd('/') + "/");

			// The access token is sent with every request, so it must not travel in the clear.
			if (root.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Microsoft Graph requires HTTPS.", nameof(graphRoot));

			this.meUri = new Uri(root, "v1.0/me?$select=" + UserProperties);
			this.acquireToken = acquireToken;
		}

		/// <summary>
		/// Retrieves the user that the access token was issued for.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The user.</returns>
		/// <exception cref="GraphServiceException">Microsoft Graph rejected the request.</exception>
		public async Task<GraphUser> GetMeAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var token = await this.acquireToken().ConfigureAwait(false);

			using (var request = new HttpRequestMessage(HttpMethod.Get, this.meUri))
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				using (var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
				{
					var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

					if (!response.IsSuccessStatusCode)
					{
						throw new GraphServiceException(response.StatusCode, body);
					}

					return JsonConvert.DeserializeObject<GraphUser>(body);
				}
			}
		}
	}

	/// <summary>
	/// The properties of a Microsoft Graph user that the portal reads.
	/// </summary>
	public class GraphUser
	{
		/// <summary>
		/// The user's object identifier.
		/// </summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>
		/// The user's given name.
		/// </summary>
		[JsonProperty("givenName")]
		public string GivenName { get; set; }

		/// <summary>
		/// The user's surname.
		/// </summary>
		[JsonProperty("surname")]
		public string Surname { get; set; }

		/// <summary>
		/// The user's display name.
		/// </summary>
		[JsonProperty("displayName")]
		public string DisplayName { get; set; }

		/// <summary>
		/// The user's primary email address.
		/// </summary>
		[JsonProperty("mail")]
		public string Mail { get; set; }

		/// <summary>
		/// The user's additional email addresses.
		/// </summary>
		[JsonProperty("otherMails")]
		public IList<string> OtherMails { get; set; } = new List<string>();

		/// <summary>
		/// The user principal name.
		/// </summary>
		[JsonProperty("userPrincipalName")]
		public string UserPrincipalName { get; set; }

		/// <summary>
		/// The service plans assigned to the user.
		/// </summary>
		[JsonProperty("assignedPlans")]
		public IList<GraphAssignedPlan> AssignedPlans { get; set; } = new List<GraphAssignedPlan>();
	}

	/// <summary>
	/// A service plan assigned to a Microsoft Graph user.
	/// </summary>
	public class GraphAssignedPlan
	{
		/// <summary>
		/// The identifier of the service plan.
		/// </summary>
		[JsonProperty("servicePlanId")]
		public Guid? ServicePlanId { get; set; }

		/// <summary>
		/// The name of the service the plan belongs to.
		/// </summary>
		[JsonProperty("service")]
		public string Service { get; set; }

		/// <summary>
		/// Whether the plan is enabled, such as Enabled or Suspended.
		/// </summary>
		[JsonProperty("capabilityStatus")]
		public string CapabilityStatus { get; set; }

		/// <summary>
		/// When the plan was assigned.
		/// </summary>
		[JsonProperty("assignedDateTime")]
		public DateTimeOffset? AssignedDateTime { get; set; }
	}

	/// <summary>
	/// Microsoft Graph rejected a request.
	/// </summary>
	public class GraphServiceException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="GraphServiceException" /> class.
		/// </summary>
		/// <param name="statusCode">The HTTP status code of the response.</param>
		/// <param name="responseBody">The body of the response, which describes the error.</param>
		public GraphServiceException(HttpStatusCode statusCode, string responseBody)
			: base(string.Format("Microsoft Graph returned {0} ({1}): {2}", (int)statusCode, statusCode, responseBody))
		{
			this.StatusCode = statusCode;
		}

		/// <summary>
		/// The HTTP status code of the response.
		/// </summary>
		public HttpStatusCode StatusCode { get; private set; }
	}
}
