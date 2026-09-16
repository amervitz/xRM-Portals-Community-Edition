/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Threading;
using System.Web.UI.WebControls;
using Adxstudio.Xrm;
using Adxstudio.Xrm.Blogs;
using Adxstudio.Xrm.Web.Mvc;
using Microsoft.Xrm.Portal;
using Microsoft.Xrm.Portal.Configuration;
using Site.Pages;

namespace Site.Areas.Blogs.Pages
{
	public partial class BlogPost : PortalPage
	{
		private readonly Lazy<IPortalContext> _portal = new Lazy<IPortalContext>(() => PortalCrmConfigurationManager.CreatePortalContext(), LazyThreadSafetyMode.None);

		protected void Page_Load(object sender, EventArgs e)
		{
			if (IsPostBack)
			{
			    Post.DataBind();
			}
		}

		protected void CreateBlogPostDataAdapter(object sender, ObjectDataSourceEventArgs e)
		{
			e.ObjectInstance = new BlogPostDataAdapter(_portal.Value.Entity, new PortalContextDataAdapterDependencies(_portal.Value, requestContext: Request.RequestContext));
		}
	}
}
