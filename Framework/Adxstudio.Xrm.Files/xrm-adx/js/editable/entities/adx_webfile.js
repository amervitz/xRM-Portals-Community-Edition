/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/


XRM.onActivate(function() {
  var ns = XRM.namespace('editable.Entity.handlers');

  XRM.localizations['entity.delete.adx_webfile.tooltip'] = window.ResourceManager['Entity_Delete_ADX_Webfile_Tooltip'];
  
  var handler = ns.adx_webfile;
  
  if (!handler) {
    XRM.log('XRM.editable.Entity.handlers.adx_webfile not found, unable to extend.', 'warn');
  }
  
  handler.formPrototype = {
    uri: null,
    urlServiceUri: null,
    urlServiceUriTemplate: null,
    title: null,
    entityName: 'adx_webfile',
    fields: [
      { name: 'adx_name', label: window.ResourceManager['Name_Label'], type: 'text', required: true  },
      { name: 'adx_partialurl', label: window.ResourceManager['Partial_URL_Label'], type: 'text', required: true, slugify: 'adx_name' },
      { name: 'adx_webfile-attachment', label: window.ResourceManager['Upload_File_Label'], labelOnUpdate: window.ResourceManager['Update_Upload_File_Label'], type: 'file', requiredOnUpdate: false, fileUploadUriTemplate: null, copyFilenameTo: 'adx_name', copyFilenameSlugTo: 'adx_partialurl' },
      { name: 'adx_publishingstateid', label: window.ResourceManager['Publishing_State_Label'], type: 'select', excludeEmptyData: true, required: true, uri: null, optionEntityName: 'adx_publishingstate', optionText: 'adx_name', optionValue: 'adx_publishingstateid'  },
      { name: 'adx_displaydate', label: window.ResourceManager['Display_Date_Label'], type: 'datetime', excludeEmptyData: true  },
      { name: 'adx_releasedate', label: window.ResourceManager['Release_Date_Label'], type: 'datetime', excludeEmptyData: true  },
      { name: 'adx_expirationdate', label: window.ResourceManager['Expiration_Date_Label'], type: 'datetime', excludeEmptyData: true  },
      { name: 'adx_hiddenfromsitemap', label: window.ResourceManager['Hidden_From_Sitemap_Label'], type: 'checkbox'  },
      { name: 'adx_summary', label: window.ResourceManager['Summary_Label'], type: 'html', ckeditorSettings: { height: 240 }  },
      { name: 'adx_cloudblobaddress', label: window.ResourceManager['Cloud_Blob_Address_Label'], type: 'text'  },
      { name: 'adx_contentdisposition', label: window.ResourceManager['Content_Disposition_Label'], type: 'picklist'  },
      { name: 'adx_parentpageid', label: window.ResourceManager['Parent_Page_Label'], type: 'parent', excludeEmptyData: true, required: true, uri: null, optionEntityName: 'adx_webpage', disableAtRoot: true, defaultToCurrent: true, defaultToRoot: true }
    ],
    layout: {
      cssClass: 'xrm-dialog-expanded',
      full: true,
      columns: [
        { cssClass: 'xrm-dialog-column-main', fields: ['adx_name', 'adx_webfile-attachment', 'adx_summary', 'adx_cloudblobaddress'] },
        { cssClass: 'xrm-dialog-column-side', fields: ['adx_parentpageid', 'adx_partialurl', 'adx_publishingstateid', 'adx_displaydate', 'adx_releasedate', 'adx_expirationdate', 'adx_contentdisposition', 'adx_hiddenfromsitemap'] }
      ]
    }
  };
});
