/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Site.Areas.Chat.Models
{
    using System;

    /// <summary>
    /// The details of the customer
    /// </summary>
    public class ChatUserModel
    {
        /// <summary>
        /// CRM record GUID of the contact logged into the portal.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// In case of ADX portal, it will be “contact”.
        /// </summary>
        public int CustomerType { get; set; }

        /// <summary>
        /// User ID of contact in CRM.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// First Name of the contact logged into the portal.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Last Name of the contact logged into the portal.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Primary Email address of the contact logged in to the portal.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Business Phone number of the contact logged in to the portal.
        /// </summary>
        public string Phone { get; set; }
    }
}
