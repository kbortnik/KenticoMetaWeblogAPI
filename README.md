# Meta Weblog API for Kentico

## About
The Meta Weblog API allows publishing of blog entries using a program such as [Open Live Writer] (formerly Windows Live Writer).

The module was present in Kentico 8, but removed in Kentico 9.

This package adds the Meta Weblog API back into Kentico.

## Requirements
You need to be running Kentico 9 or above for the Meta Weblog API to function.

## Installation instructions

 1. Copy the files `CMS.MetaWeblogProvider.dll` and `CookComputing.XmlRpcV2.dll` to the `CMS/bin` folder of your website
 2. Copy the file `MetaWeblog.ashx` into the folder `CMS/CMSModules/Blogs/CMSPages` of your Kentico project (you may need to create that folder)
 3. In Kentico, open the Sites application
 4. Click "Import site or objects"
 5. In the import dialog, click "Upload package"
 6. Select the `MetaWeblogSettings.zip` file
 7. When the file uploads and appears in the packages list, select it and click "Next"
 8. On the Objects Selection screen, leave the default settings selected
 9. Click "Next"
 10. On the "Import Progress" screen, click "Finish". You can now close the Administration area.

## Setting up Open Live Writer
Download [Open Live Writer], then follow the [instructions to set up Open Live Writer for use with Kentico].

For instructions on how to publish and manage blog posts, read the following documentation:
 * [MetaWeblogAPI - Publishing first blog post]
 * [MetaWeblogAPI - Managing blog posts]

## Kentico settings
There are 3 settings in Kentico relating to the Meta Weblog API (these are imported into Kentico when you import the `MetaWeblogSettings.zip` file).

The settings are found in the admin interface, in the Settings application under Custom Settings:

|Setting|Description|
|----------------------------------|-----------|
|CMS Meta Weblog Generate Summary  |Specifies whether the Meta Weblog API should automatically generate a summary for a blog article|
|CMS Meta Weblog Summary Length    |Length of the summary to generate|
|CMS Meta Weblog Delete Attachments|Whether blog attachments that are not used in post text or have not been uploaded via Open Live Writer are deleted when a blog post is modified and published using Open Live Writer|

(for more information on these settings, see the [documentation])

[Open Live Writer]: http://openlivewriter.org/
[instructions to set up Open Live Writer for use with Kentico]: https://devnet.kentico.com/docs/7_0/devguide/index.html?metaweblog_api_registering_blog_account.htm
[MetaWeblogAPI - Publishing first blog post]: https://devnet.kentico.com/docs/7_0/devguide/index.html?metaweblog_api_publishing_first_blog_post.htm
[MetaWeblogAPI - Managing blog posts]: https://devnet.kentico.com/docs/7_0/devguide/index.html?metaweblog_api_managing_blog_posts.htm
[documentation]: http://devnet.kentico.com/docs/contexthelp/index.html?settings_metaweblogapi.htm
