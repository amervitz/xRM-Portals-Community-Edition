/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/


XRM.onActivate(function() {
  var ns = XRM.namespace('editable.Entity.handlers');

  XRM.localizations['entity.delete.adx_webpage.tooltip'] = window.ResourceManager['Entity_Delete_ADX_Webpage_Tooltip'];

  var handler = ns.adx_webpage;
  var languageCode = $('html').data('lang');

  function renderGrid(container, label) {
    if (!(this.value && this.value.Id)) {
      $(label).parent().remove();
      return;
    }

    var currentUrl = $('.xrm-entity-current a.xrm-entity-ref').attr("href");
    var baseUrl = (function (urlParts) {
      urlParts.splice(urlParts.length - 2, 2);
      return urlParts.join("/");
    })(currentUrl.split("/"));

    var fromBaseUrl = function (appendParts) {
      return baseUrl.split("/").concat(appendParts).join("/");
    };

    $
      .when(
        $.getJSON(fromBaseUrl(["adx_websitelanguage"])),
        $.getJSON(fromBaseUrl(["adx_webpage", this.value.Id, "__related", "adx_webpage_webpage_rootwebpageid.Referenced"])),
        $.getJSON(fromBaseUrl(["adx_publishingstate"]))
      )
      .done(function (languages_resp, contentPages_resp, publishingStates_resp) {
        var langs = languages_resp[0].d;
        var pages = contentPages_resp[0].d;
        var states = publishingStates_resp[0].d;
        var getColor = function(stateId) {
          var val = null;
          for (var i = 0; i < states.length; i++) {
            var state = states[i];
            if (state.Id === stateId) {
              val = state.adx_isvisible;
            }
          }
          if (val === true) return "state-published";
          if (val === false) return "state-draft";
          return "state-not-created";
        };

        var control = $("<div/>", { "class": "xrm-language-grid" })
          .append($("<table/>", { "class": "language-grid" }).append());

        var tbody = $("<tbody/>");
        for (var i = 0; i < langs.length; i++) {
          var lang = langs[i];
          var page = _.find(pages, function (page) {
            return page.adx_webpagelanguageid && page.adx_webpagelanguageid.Id === lang.Id;
          });
          var publishingState = page && page.adx_publishingstateid || {};
          var tr = $("<tr/>")
            .append([
              $("<td/>").append($("<div/>", { text: lang.Name })),
              $("<td/>",
              {
                text: publishingState.Name || window.ResourceManager['Adx_Webpage_Not_Yet_Created'],
                "class": getColor(publishingState.Id)
              })
            ]);
          tbody.append(tr);
        }

        tbody.appendTo($(control).find("table"));
        control.appendTo(container);
      });
  }

  if (!handler) {
    XRM.log('XRM.editable.Entity.handlers.adx_webpage not found, unable to extend.', 'warn');
  }

  var generalTabCentaurus =
    {
      id: 'general',
      label: window.ResourceManager['General_Label'],
      icon: 'adx-icon adx-icon-file-text-o',
      columns: [{
        cssClass: 'xrm-dialog-simple-fields-one-third',
        fields: ['adx_name', 'adx_parentpageid', 'adx_partialurl', 'adx_pagetemplateid', 'adx_webform', 'adx_entityform', 'adx_entitylist', 'adx_rootwebpageid']
      }]
    };

  var generalTabNaos =
    {
      id: 'general',
      label: window.ResourceManager['General_Label'],
      icon: 'adx-icon adx-icon-file-text-o',
      columns: [
        { cssClass: 'xrm-dialog-column-main', fields: ['adx_name', 'adx_title', 'adx_copy', 'adx_summary'] },
        { cssClass: 'xrm-dialog-column-side', fields: ['adx_parentpageid', 'adx_partialurl', 'adx_pagetemplateid', 'adx_publishingstateid', 'adx_webform', 'adx_entityform', 'adx_entitylist'] }
      ]
    }

  var contentTab =
    {
        id: 'content',
        label: window.ResourceManager['Content_Language_Label'],
        icon: 'adx-icon adx-icon-file-text-o',
        columns: [{
          cssClass: 'xrm-dialog-simple-fields-one-third',
          fields: ['adx_webpagelanguageid', 'adx_publishingstateid', 'adx_title', 'adx_copy', 'adx_summary']
        }]
    }

  var optionsTab =
    {
        id: 'options',
        label: window.ResourceManager['Options_Label'],
        icon: 'adx-icon adx-icon-cog',
        columns: [
          { cssClass: 'xrm-dialog-column-main', fields: ['adx_meta_description', 'adx_customjavascript', 'adx_customcss'] },
          { cssClass: 'xrm-dialog-column-side', fields: ['adx_displaydate', 'adx_subjectid', 'adx_feedbackpolicy', 'adx_enablerating', 'adx_hiddenfromsitemap', 'adx_excludefromsearch'] }
        ]
    }

  var publishingTab =
    {
        id: 'publishing',
        label: window.ResourceManager['Publishing_Label'],
        icon: 'adx-icon adx-icon-share-alt',
        columns: [
          { cssClass: 'xrm-dialog-column-main', fields: ['adx_editorialcomments'] },
          { cssClass: 'xrm-dialog-column-side', fields: ['adx_releasedate', 'adx_expirationdate'] }
        ]
    }

  var editorTabs = [];
  if (languageCode) {
      editorTabs.push(generalTabCentaurus);
      editorTabs.push(contentTab);
  } else {
      editorTabs.push(generalTabNaos);
  }

  editorTabs.push(optionsTab);
  editorTabs.push(publishingTab);

  handler.formPrototype = {
    uri: null,
    urlServiceUri: null,
    urlServiceUriTemplate: null,
    title: null,
    entityName: 'adx_webpage',
    fields: [
      { name: 'adx_name', label: window.ResourceManager['Name_Label'], type: 'text', required: true, maxlength: 100  },
      { name: 'adx_title', label: window.ResourceManager['Title_Label'], type: 'text', maxlength: 512  },
      { name: 'adx_partialurl', label: window.ResourceManager['Partial_URL_Label'], type: 'text', required: true, slugify: 'adx_name' },
      { name: 'adx_pagetemplateid', label: window.ResourceManager['Page_Template_Label'], type: 'select', excludeEmptyData: true, required: true, uri: null, optionEntityName: 'adx_pagetemplate', optionText: 'adx_name', optionValue: 'adx_pagetemplateid', optionDescription: 'adx_description', sortby: 'adx_name', filter: 'adx_webpage'  },
      { name: 'adx_publishingstateid', label: window.ResourceManager['Publishing_State_Label'], type: 'select', excludeEmptyData: true, required: true, uri: null, optionEntityName: 'adx_publishingstate', optionText: 'adx_name', optionValue: 'adx_publishingstateid'   },
      { name: 'adx_displaydate', label: window.ResourceManager['Display_Date_Label'], type: 'datetime', excludeEmptyData: true  },
      { name: 'adx_releasedate', label: window.ResourceManager['Release_Date_Label'], type: 'datetime', excludeEmptyData: true  },
      { name: 'adx_expirationdate', label: window.ResourceManager['Expiration_Date_Label'], type: 'datetime', excludeEmptyData: true  },
      { name: 'adx_hiddenfromsitemap', label: window.ResourceManager['Hidden_From_Sitemap_Label'], type: 'checkbox'  },
      { name: 'adx_copy', label: window.ResourceManager['Copy_Label'], type: 'html', ckeditorSettings: { height: 240 }  },
      { name: 'adx_summary', label: window.ResourceManager['Summary_Label'], type: 'html', ckeditorSettings: { height: 80 } },
      { name: 'adx_subjectid', label: window.ResourceManager['Subject_Label'], type: 'select', excludeEmptyData: false, required: false, uri: null, optionEntityName: 'subject', optionText: 'title', optionValue: 'subjectid'  },
      { name: 'adx_feedbackpolicy', label: window.ResourceManager['Comment_Policy_Label'], type: 'picklist', required: true  },
      { name: 'adx_enablerating', label: window.ResourceManager['Enable_Ratings_Label'], type: 'checkbox', checkedByDefault: false  },
      { name: 'adx_entityform', label: window.ResourceManager['Entity_Form_Label'], type: 'select', excludeEmptyData: false, required: false, uri: null, optionEntityName: 'adx_entityform', optionText: 'adx_name', optionValue: 'adx_entityformid', expansion: true  },
      { name: 'adx_entitylist', label: window.ResourceManager['Entity_List_Label'], type: 'select', excludeEmptyData: false, required: false, uri: null, optionEntityName: 'adx_entitylist', optionText: 'adx_name', optionValue: 'adx_entitylistid', expansion: true  },
      { name: 'adx_webform', label: window.ResourceManager['Web_Form_Label'], type: 'select', excludeEmptyData: false, required: false, uri: null, optionEntityName: 'adx_webform', optionText: 'adx_name', optionValue: 'adx_webformid', expansion: true  },
      { name: 'adx_editorialcomments', label: window.ResourceManager['Editorial_Comments_Label'], type: 'textarea', height: 200, maxlength: 2000 },
      { name: 'adx_excludefromsearch', label: window.ResourceManager['Exclude_From_Search_Label'], type: 'checkbox', checkedByDefault: false  },
      { name: 'adx_meta_description', label: window.ResourceManager['Description_Label'], type: 'text', maxlength: 255 },
      { name: 'adx_customjavascript', label: window.ResourceManager['Custom_JavaScript_Label'], type: 'iframe', xrmsrc: 'js/editable/source_js.html', height: 240  },
      { name: 'adx_customcss', label: window.ResourceManager['Custom_CSS_Label'], type: 'iframe', xrmsrc: 'js/editable/source_css.html', height: 240 },
      { name: 'adx_webpagelanguageid', label: window.ResourceManager['Language_Label'], type: 'text', excludeEmptyData: false, disabled: true, formatter: function (val) { return val["Name"] }, defaultToCurrentLang: true },
      { name: 'adx_rootwebpageid', label: window.ResourceManager['Language_Label'], type: 'customrender', excludeEmptyData: false, disabled: true, render: renderGrid },
      { name: 'adx_parentpageid', label: window.ResourceManager['Parent_Page_Label'], type: 'parent', excludeEmptyData: true, required: true, uri: null, optionEntityName: 'adx_webpage', disableAtRoot: true, defaultToCurrent: true, defaultToRoot: true }
    ],
    layout: {
      cssClass: 'xrm-dialog-expanded',
      full: true,
      tabs: editorTabs
    }
  };
  
  handler.createOptions.push({
    entityName: 'adx_event', relationship: 'adx_webpage_event', label: 'entity.create.adx_event.label', title: 'entity.create.adx_event.tooltip', redirect: true
  });
  
  handler.createOptions.push({
    entityName: 'adx_communityforum', relationship: 'adx_webpage_communityforum', label: 'entity.create.adx_communityforum.label', title: 'entity.create.adx_communityforum.tooltip', redirect: true
  });

  handler.createOptions.push({
    entityName: 'adx_shortcut', relationship: 'adx_parentwebpage_shortcut', label: 'entity.create.adx_shortcut.label', title: 'entity.create.adx_shortcut.tooltip', redirect: false
  });

});
