using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Data;
using System.Collections;

using CMS.Blogs;
using CMS.EventLog;
using CMS.Helpers;
using CMS.IO;
using CMS.Base;
using CMS.SiteProvider;
using CMS.Membership;
using CMS.DocumentEngine;
using CMS.WorkflowEngine;

using CookComputing.XmlRpc;

using TimeZoneInfo = CMS.Globalization.TimeZoneInfo;
using CMS.Globalization;
using CMS.Protection;
using CMS.Taxonomy;
using CMS.DataEngine;

namespace CMS.MetaWeblogProvider
{
    /// <summary>
    /// Class serving MetaWeblogAPI requests.
    /// </summary>
    [XmlRpcService]
    public class MetaWeblogAPI : XmlRpcService, IMetaWeblogAPI
    {
        #region "Private variables"

        private const string ATT_GUID_REGEX = "(?:GetFile.aspx\\?guid=)(?<guid>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})";
        private static Regex mAttachmentGuidRegex;

        private static readonly Hashtable blogGuid = new Hashtable();

        private string mSiteName = "";
        private string mBlogCulture = TreeProvider.ALL_CULTURES;
        private string mBlogPath = "";
        private TreeNode mBlogNode;
        private TreeProvider mTreeProvider;
        private WorkflowManager mWorkflowManager;
        private VersionManager mVersionManager;

        #endregion


        #region "Private properties"

        /// <summary>
        /// Regular expression used to match all attachment GUIDs in the blog post text.
        /// </summary>
        private static Regex AttachmentGuidRegex
        {
            get
            {
                return mAttachmentGuidRegex ?? (mAttachmentGuidRegex = RegexHelper.GetRegex(ATT_GUID_REGEX, RegexOptions.Multiline | RegexOptions.IgnoreCase));
            }
        }


        /// <summary>
        /// Indicates whether the unused attachments should be removed.
        /// </summary>
        private static bool DeleteUnusedAttachments
        {
            get
            {
                return SettingsKeyInfoProvider.GetBoolValue(SiteContext.CurrentSiteName + ".CMSMetaWeblogDeleteAttachments");
            }
        }


        /// <summary>
        /// Indicates whether the summary should be generated.
        /// </summary>
        private static bool BlogPostSummaryEnabled
        {
            get
            {
                return SettingsKeyInfoProvider.GetBoolValue(SiteContext.CurrentSiteName + ".CMSMetaWeblogGenerateSummary");
            }
        }


        /// <summary>
        /// Gets number of characters used for automatic summary.
        /// </summary>
        private static int BlogPostSummaryLength
        {
            get
            {
                return SettingsKeyInfoProvider.GetIntValue(SiteContext.CurrentSiteName + ".CMSMetaWeblogSummaryLength");
            }
        }


        /// <summary>
        /// User related tree provider object.
        /// </summary>
        private TreeProvider TreeProvider
        {
            get
            {
                if (mTreeProvider == null)
                {
                    mTreeProvider = new TreeProvider();
                }
                if (User != null)
                {
                    mTreeProvider.UserInfo = User;
                }
                return mTreeProvider;
            }
        }


        /// <summary>
        /// Manager for workflow operations.
        /// </summary>
        private WorkflowManager WorkflowManager
        {
            get
            {
                return mWorkflowManager ?? (mWorkflowManager = WorkflowManager.GetInstance(TreeProvider));
            }
        }


        /// <summary>
        /// Manager for version operations.
        /// </summary>
        private VersionManager VersionManager
        {
            get
            {
                return mVersionManager ?? (mVersionManager = VersionManager.GetInstance(TreeProvider));
            }
        }


        /// <summary>
        /// Alias path to the blog node.
        /// </summary>
        private string BlogPath
        {
            get
            {
                if (mBlogNode != null)
                {
                    mBlogPath = BlogNode.NodeAliasPath;
                }
                return mBlogPath;
            }
        }


        /// <summary>
        /// Current blog culture.
        /// </summary>
        private string BlogCulture
        {
            get
            {
                if (mBlogNode != null)
                {
                    mBlogCulture = BlogNode.DocumentCulture;
                }
                return mBlogCulture;
            }
        }


        /// <summary>
        /// Node representing blog.
        /// </summary>
        private TreeNode BlogNode
        {
            get
            {
                return mBlogNode;
            }
            set
            {
                mBlogNode = value;
            }
        }


        /// <summary>
        /// Gets current blog's site name.
        /// </summary>
        private string SiteName
        {
            get
            {
                if (mBlogNode != null)
                {
                    mSiteName = SiteInfoProvider.GetSiteInfo(BlogNode.NodeSiteID).SiteName;
                }
                else
                {
                    mSiteName = SiteContext.CurrentSiteName;
                }

                return mSiteName;
            }
        }


        /// <summary>
        /// Current user information.
        /// </summary>
        private UserInfo User
        {
            get;
            set;
        }

        #endregion


        #region "Error messages"

        private const string MSG_ERR_BANNEDIP = "Your IP is banned in the system.";
        private const string MSG_ERR_USERNOTAUTH = "User could not be verified. Please check your user name and password.";
        private const string MSG_ERR_BLOGCHECKEDOUT = "The blog post is exclusively checked out by another user.";
        private const string MSG_ERR_BLOGUNAVAILABLE = "The blog couldn't be found or obtained.";
        private const string MSG_ERR_BLOGPOSTUNAVAILABLE = "The blog post isn't present on server any more.";
        private const string MSG_ERR_USERNOTAUTHFORBLOGPERM = "You're not authorized to {0} the page '{1}'.";
        private const string MSG_ERR_USERNOTAUTHFORWORKFLOWSTEP = "You're not authorized to approve the document. Workflow step: {0}.";
        private const string MSG_ERR_WORKFLOWPATHNOTFOUND = "The page has not been published. You are either not authorized to approve the page through all the workflow steps or there are multiple or no approval paths.";
        private const string MSG_ERR_WORKFLOWLOOP = "The page has not been published. Possible loop in the page workflow detected.";

        #endregion


        #region IMetaWeblogAPI Members

        /// <summary>
        /// Inserts the new blog post within specified blog.
        /// </summary>
        /// <param name="blogid">ID of the blog post is added to</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="post">Blog post info</param>
        /// <param name="publish">Indicates whether the blog post should be published. If false it is a draft post</param>
        public string AddPost(string blogid, string username, string password, MetaWeblogAPIObjects.Post post, bool publish)
        {
            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                // Ensure parent blog node
                EnsureBlogInfo(blogid);

                if (IsUserAllowedForDocument(BlogNode, NodePermissionsEnum.Create))
                {
                    try
                    {
                        // Get publish date
                        DateTime postCreated = GetBlogPostPublishDate(post.dateCreated);

                        #region "Post creating"

                        // Create new blog post object
                        TreeNode newPostNode = TreeNode.New("CMS.blogpost", TreeProvider);

                        // Keep max title length due to the database field limitations
                        string postTitle = HttpUtility.HtmlDecode(post.title.Trim());
                        postTitle = TextHelper.LimitLength(postTitle, 100);

                        // Keep max length for database field
                        newPostNode.DocumentName = postTitle;
                        newPostNode.DocumentCulture = BlogNode.DocumentCulture;

                        // Postpone publish date if required, otherwise publish immediately
                        if (postCreated != DateTime.MinValue)
                        {
                            newPostNode.SetValue("DocumentPublishFrom", postCreated);
                        }
                        else
                        {
                            postCreated = DateTime.Now;
                        }

                        string summary = GetPostSummary(post.description);
                        newPostNode.SetValue("BlogPostSummary", summary);
                        newPostNode.SetValue("BlogPostTitle", postTitle);
                        newPostNode.SetValue("BlogPostDate", postCreated);
                        newPostNode.SetValue("BlogPostBody", HTMLHelper.UnResolveUrls(post.description, URLHelper.GetApplicationUrl()));
                        newPostNode.SetValue("BlogPostAllowComments", true);

                        // Assign tags if specified
                        if ((post.categories != null) && (post.categories.Length > 0))
                        {
                            string tagsString = GetTagsString(post.categories);
                            newPostNode.DocumentTags = tagsString;
                        }

                        bool useParentNodeGroupID = TreeProvider.UseParentNodeGroupID;
                        try
                        {
                            TreeProvider.UseParentNodeGroupID = true;
                            // Ensure blog month
                            var parent = DocumentHelper.EnsureBlogPostHierarchy(newPostNode, BlogNode, TreeProvider);

                            // Save new blog post
                            DocumentHelper.InsertDocument(newPostNode, parent, TreeProvider);
                        }
                        finally
                        {
                            TreeProvider.UseParentNodeGroupID = useParentNodeGroupID;
                        }

                        // Get workflow info
                        WorkflowInfo wi = WorkflowManager.GetNodeWorkflow(newPostNode);

                        // Check-in after insert when workflow is used
                        if ((wi != null) && wi.UseCheckInCheckOut(SiteName))
                        {
                            // Check-in node after editing
                            VersionManager.CheckIn(newPostNode, null);
                        }

                        // Save temporary attachments
                        PublishAttachments(newPostNode, DeleteUnusedAttachments);

                        // Publish blog post if required, otherwise save as a draft
                        if (publish)
                        {
                            Publish(newPostNode);
                        }
                        else
                        {
                            Unpublish(newPostNode);
                        }

                        #endregion

                        // Return info on new post's ID
                        return ValidationHelper.GetString(newPostNode.GetValue("BlogPostID"), "0");
                    }
                    catch (Exception ex)
                    {
                        throw new XmlRpcException(ex.Message);
                    }
                }
            }

            return "";
        }


        /// <summary>
        /// Updates information on specified blog post.
        /// </summary>
        /// <param name="postid">ID of the post to update</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="post">Blog post info</param>
        /// <param name="publish">Indicates whether the blog post should be published. If false it is a draft post</param>
        public bool UpdatePost(string postid, string username, string password, MetaWeblogAPIObjects.Post post, bool publish)
        {
            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                try
                {
                    // Get post info
                    TreeNode editPost = GetBlogPostById(postid);
                    if ((editPost != null) && IsUserAllowedForDocument(editPost, NodePermissionsEnum.Modify, true))
                    {
                        // Get parent blog's ID and initialize blog info                        
                        BlogNode = GetBlogNodeByPost(editPost);


                        #region "Post editing"

                        bool checkedOut = false;

                        // Check-out for editing
                        bool workflowUsed = (WorkflowManager.GetNodeWorkflow(editPost) != null);
                        if (workflowUsed)
                        {
                            // Check if post is already checked-out
                            int docCheckedOutByUserId = editPost.DocumentCheckedOutByUserID;
                            if ((docCheckedOutByUserId == 0) || ((docCheckedOutByUserId > 0) && (docCheckedOutByUserId == User.UserID)))
                            {
                                if (docCheckedOutByUserId == 0)
                                {
                                    // Post not checked-out yet
                                    VersionManager.CheckOut(editPost, editPost.IsPublished, true);
                                    checkedOut = true;
                                }
                            }
                            else
                            {
                                // Different user has checked-out post
                                throw new XmlRpcException(MSG_ERR_BLOGCHECKEDOUT);
                            }
                        }

                        string postTitle = HttpUtility.HtmlDecode(post.title.Trim());
                        postTitle = TextHelper.LimitLength(postTitle, 100);

                        // Edit blog post info         
                        editPost.DocumentName = postTitle;
                        editPost.DocumentCulture = BlogNode.DocumentCulture;

                        // Postpone publish date if required, otherwise publish immediately
                        DateTime postCreated = GetBlogPostPublishDate(post.dateCreated);
                        if (postCreated != DateTime.MinValue)
                        {
                            editPost.SetValue("DocumentPublishFrom", postCreated);
                        }
                        else
                        {
                            postCreated = DateTime.Now;
                        }

                        if (publish && workflowUsed)
                        {
                            editPost.SetValue("DocumentPublishTo", null);
                        }

                        string summary = GetPostSummary(post.description);
                        editPost.SetValue("BlogPostSummary", summary);
                        editPost.SetValue("BlogPostTitle", postTitle);
                        editPost.SetValue("BlogPostDate", postCreated);
                        editPost.SetValue("BlogPostBody", HTMLHelper.UnResolveUrls(post.description, URLHelper.GetApplicationUrl()));

                        // Assign tags if specified
                        if ((post.categories != null) && (post.categories.Length > 0))
                        {
                            string tagsString = GetTagsString(post.categories);
                            editPost.DocumentTags = tagsString;
                        }

                        // Update new blog post
                        DocumentHelper.UpdateDocument(editPost, TreeProvider);

                        // Check-in node after editing
                        if (checkedOut)
                        {
                            VersionManager.CheckIn(editPost, null);
                        }

                        // Save temporary attachments
                        PublishAttachments(editPost, DeleteUnusedAttachments);

                        // Publish blog post if required, otherwise save as a draft
                        if (publish)
                        {
                            Publish(editPost);
                        }
                        else
                        {
                            Unpublish(editPost);
                        }

                        #endregion


                        return true;
                    }
                }
                catch (Exception ex)
                {
                    throw new XmlRpcException(ex.Message);
                }
            }

            return false;
        }


        /// <summary>
        /// Gets a specific post of the specified blog.
        /// </summary>
        /// <param name="postid">ID of the post to retrieve</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        public MetaWeblogAPIObjects.Post GetPost(string postid, string username, string password)
        {
            MetaWeblogAPIObjects.Post result = new MetaWeblogAPIObjects.Post();

            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                // Get info on requested blog post and fill-in the post structure
                TreeNode blogPost = GetBlogPostById(postid);
                if ((blogPost != null) && IsUserAllowedForDocument(blogPost, NodePermissionsEnum.Read, true))
                {
                    result.categories = GetBlogPostTags(blogPost.DocumentID);
                    result.dateCreated = ValidationHelper.GetDateTime(blogPost.GetValue("BlogPostDate"), DateTimeHelper.ZERO_TIME);
                    result.description = URLHelper.MakeLinksAbsolute(ValidationHelper.GetString(blogPost.GetValue("BlogPostBody"), ""));
                    result.postid = ValidationHelper.GetInteger(blogPost.GetValue("BlogPostID"), 0);
                    result.title = ValidationHelper.GetString(blogPost.GetValue("BlogPostTitle"), "");
                    result.userid = ValidationHelper.GetString(User.UserID, "0");
                    result.permalink = DocumentURLProvider.GetPermanentDocUrl(blogPost.NodeGUID, blogPost.NodeAlias, SiteName);

                    result.wp_slug = "";
                }
            }

            return result;
        }


        /// <summary>
        /// Gets a list of tags used in the particular blog.
        /// </summary>
        /// <param name="blogid">ID of the blog tags are retrieved for</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        public MetaWeblogAPIObjects.CategoryInfo[] GetCategories(string blogid, string username, string password)
        {
            MetaWeblogAPIObjects.CategoryInfo[] blogTags = null;

            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                // Update information on current blog
                EnsureBlogInfo(blogid);

                if (IsUserAllowedForDocument(BlogNode, NodePermissionsEnum.Read))
                {
                    // Get tag group ID either from node or parent node when inherited
                    int tagGroupId = BlogNode.DocumentTagGroupID;
                    if (tagGroupId == 0)
                    {
                        tagGroupId = ValidationHelper.GetInteger(BlogNode.GetInheritedValue("DocumentTagGroupID", false), 0);
                    }

                    // Tags are retrieved from blog node tag group
                    string where = "TagGroupID=" + tagGroupId;

                    // Get all tags
                    DataSet tags = TagInfoProvider.GetTags(where, "TagName ASC");
                    if (!DataHelper.DataSourceIsEmpty(tags))
                    {
                        int currTagIndex = 0;

                        blogTags = new MetaWeblogAPIObjects.CategoryInfo[tags.Tables[0].Rows.Count];

                        foreach (DataRow dr in tags.Tables[0].Rows)
                        {
                            // Create new category info
                            MetaWeblogAPIObjects.CategoryInfo tag = new MetaWeblogAPIObjects.CategoryInfo();

                            tag.categoryid = dr["TagID"].ToString();
                            tag.description = dr["TagName"].ToString();
                            tag.title = dr["TagName"].ToString();

                            tag.htmlUrl = String.Empty;
                            tag.rssUrl = String.Empty;

                            // Add it to the list
                            blogTags[currTagIndex] = tag;
                            currTagIndex++;
                        }
                    }
                }
            }

            return blogTags;
        }


        /// <summary>
        /// Gets a set of the most recent blog posts in descending order by publish date.
        /// </summary>
        /// <param name="blogid">ID of the blog</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="numberOfPosts">Indicates the number of returned posts</param>
        public MetaWeblogAPIObjects.Post[] GetRecentPosts(string blogid, string username, string password, int numberOfPosts)
        {
            MetaWeblogAPIObjects.Post[] posts = null;

            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                // Update information on current blog
                EnsureBlogInfo(blogid);

                if (IsUserAllowedForDocument(BlogNode, NodePermissionsEnum.Read))
                {
                    // Get all posts for the current blog
                    DataSet result = DocumentHelper.GetDocuments(SiteName, BlogPath + "/%", BlogCulture, false, "cms.blogpost", null, "BlogPostDate DESC", -1, false, numberOfPosts, TreeProvider);

                    // Filter out posts for which the user has no read permission
                    result = TreeSecurityProvider.FilterDataSetByPermissions(result, NodePermissionsEnum.Read, User, true);

                    if (!DataHelper.DataSourceIsEmpty(result))
                    {
                        int currPostIndex = 0;
                        posts = new MetaWeblogAPIObjects.Post[result.Tables[0].Rows.Count];

                        // Go through all posts and create new post object info for each record
                        foreach (DataRow dr in result.Tables[0].Rows)
                        {
                            TreeNode postNode = TreeNode.New("cms.blogpost", dr);
                            if (postNode != null)
                            {
                                // Create new post object and fill it with information
                                MetaWeblogAPIObjects.Post post = new MetaWeblogAPIObjects.Post();

                                post.categories = GetBlogPostTags(postNode.DocumentID);
                                post.dateCreated = ValidationHelper.GetDateTime(postNode.GetValue("BlogPostDate"), DateTimeHelper.ZERO_TIME);
                                post.description = URLHelper.MakeLinksAbsolute(ValidationHelper.GetString(postNode.GetValue("BlogPostBody"), String.Empty));
                                post.postid = ValidationHelper.GetString(postNode.GetValue("BlogPostID"), "0");
                                post.title = ValidationHelper.GetString(postNode.GetValue("BlogPostTitle"), String.Empty);
                                post.userid = ValidationHelper.GetString(User.UserID, "0");
                                post.permalink = DocumentURLProvider.GetPermanentDocUrl(postNode.NodeGUID, postNode.NodeAlias, SiteName);

                                post.wp_slug = String.Empty;

                                // Insert record on post to the resulting array
                                posts[currPostIndex] = post;
                                currPostIndex++;
                            }
                        }
                    }
                }
            }

            return posts;
        }


        /// <summary>
        /// Creates a new media object on the server side for later use in the blog.
        /// </summary>
        /// <param name="blogid">ID of the blog</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="mediaObject">Media object info</param>
        public MetaWeblogAPIObjects.MediaObjectInfo NewMediaObject(string blogid, string username, string password, MetaWeblogAPIObjects.MediaObject mediaObject)
        {
            MetaWeblogAPIObjects.MediaObjectInfo mediaObj = new MetaWeblogAPIObjects.MediaObjectInfo();

            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                // Update information on current blog
                EnsureBlogInfo(blogid);

                if (IsUserAllowedForDocument(BlogNode, NodePermissionsEnum.Modify))
                {
                    try
                    {
                        // Get media object name
                        string mediaObjName = Path.GetFileName(mediaObject.name);

                        // Create new attachment for file document
                        AttachmentInfo ai = new AttachmentInfo();

                        ai.AttachmentBinary = mediaObject.bits;
                        ai.AttachmentExtension = Path.GetExtension(mediaObjName);
                        ai.AttachmentGUID = Guid.NewGuid();

                        // Get image dimensions if applicable
                        if (ImageHelper.IsImage(ai.AttachmentExtension))
                        {
                            ImageHelper ih = new ImageHelper(ai.AttachmentBinary);
                            ai.AttachmentImageHeight = ih.ImageHeight;
                            ai.AttachmentImageWidth = ih.ImageWidth;
                        }

                        ai.AttachmentLastModified = TimeZoneHelper.ConvertToServerDateTime(DateTime.Now, User);
                        ai.AttachmentMimeType = mediaObject.type;
                        ai.AttachmentName = mediaObjName;
                        ai.AttachmentSiteID = BlogNode.NodeSiteID;
                        ai.AttachmentSize = mediaObject.bits.Length;

                        // Save new attachment as temporary attachment
                        if (blogGuid[BlogNode.NodeID] == null)
                        {
                            blogGuid[BlogNode.NodeID] = Guid.NewGuid();
                        }
                        AttachmentInfoProvider.AddTemporaryAttachment((Guid)blogGuid[BlogNode.NodeID], null, Guid.Empty, Guid.Empty, ai, BlogNode.NodeSiteID, 0, 0, 0);

                        // Fill in information on newly uploaded file and return it as required
                        mediaObj.url = SystemContext.ApplicationPath.TrimEnd('/') + DocumentHelper.GetAttachmentUrl(ai, 0).Substring(1);
                    }
                    catch (Exception ex)
                    {
                        throw new XmlRpcException(ex.Message);
                    }
                }
            }

            return mediaObj;
        }


        /// <summary>
        /// Deletes a specific blog post on the server side.
        /// </summary>
        /// <param name="key">This value is ignored. Required to keep blog compatibility</param>
        /// <param name="postid">ID of the post to delete</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="publish">This value is ignored. Required to keep blog compatibility</param>
        public bool DeletePost(string key, string postid, string username, string password, bool publish)
        {
            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                TreeNode post = GetBlogPostById(postid);
                if ((post != null) && IsUserAllowedForDocument(post, NodePermissionsEnum.Delete))
                {
                    // Remove specified post record
                    DocumentHelper.DeleteDocument(post, null, false, false); // K9 Change
                    //DocumentHelper.DeleteDocument(post, TreeProvider, false, false, true); K9 Change

                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Gets a list of user's blog posts.
        /// </summary>
        /// <param name="key">This value is ignored. Required to keep blog applications compatibility</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        public MetaWeblogAPIObjects.BlogInfo[] GetUsersBlogs(string key, string username, string password)
        {
            MetaWeblogAPIObjects.BlogInfo[] blogs = null;

            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password))
            {
                // Get all blogs of the current user
                DataSet userBlogs;
                if (User.IsGlobalAdministrator)
                {
                    userBlogs = BlogHelper.GetBlogs(SiteName);
                }
                else
                {
                    userBlogs = BlogHelper.GetBlogs(SiteName, User.UserID);
                }
                if (!DataHelper.DataSourceIsEmpty(userBlogs))
                {
                    int currBlogIndex = 0;

                    blogs = new MetaWeblogAPIObjects.BlogInfo[userBlogs.Tables[0].Rows.Count];

                    // For each blog create a reord in the resulting array
                    foreach (DataRow dr in userBlogs.Tables[0].Rows)
                    {
                        MetaWeblogAPIObjects.BlogInfo blog = new MetaWeblogAPIObjects.BlogInfo();

                        blog.blogid = dr["BlogID"].ToString();
                        blog.blogName = dr["BlogName"] + " (" + dr["DocumentCulture"] + ")";
                        blog.url = URLHelper.ResolveUrl(DocumentURLProvider.GetUrl(dr["NodeAliasPath"].ToString(), dr["DocumentUrlPath"].ToString(), SiteName));

                        // Insert record on user's blog
                        blogs[currBlogIndex] = blog;
                        currBlogIndex++;
                    }
                }
            }

            return blogs;
        }


        /// <summary>
        /// Gets an info on specific blog user.
        /// </summary>
        /// <param name="key">This value is ignored. Required to keep blog applications compatibility</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        public MetaWeblogAPIObjects.UserInfo GetUserInfo(string key, string username, string password)
        {
            MetaWeblogAPIObjects.UserInfo userObj = new MetaWeblogAPIObjects.UserInfo();

            // Check if the use is authorized per service and node to create new blog posts
            if (ValidateUser(username, password) && (User != null))
            {
                userObj.email = User.Email;
                userObj.firstname = User.FirstName;
                userObj.lastname = User.LastName;
                userObj.nickname = User.UserNickName;
                userObj.url = User.UserURLReferrer;
                userObj.userid = User.UserID.ToString();
            }

            return userObj;
        }

        #endregion


        #region "Security methods"

        /// <summary>
        /// Indicates whether the user is allowed for MetaWeblogAPI service.
        /// </summary>
        /// <param name="userName">User name</param>
        /// <param name="password">Password</param>
        private bool ValidateUser(string userName, string password)
        {
            // Get MetaWeblog supported site name            
            if (SiteName != String.Empty)
            {
                // Check Banned IPs
                if (!BannedIPInfoProvider.IsAllowed(SiteName, BanControlEnum.Complete) ||
                    !BannedIPInfoProvider.IsAllowed(SiteName, BanControlEnum.Login) ||
                    !BannedIPInfoProvider.IsAllowed(SiteName, BanControlEnum.AllNonComplete))
                {
                    EventLogProvider.LogEvent(EventType.ERROR, "MetaWeblog API", "EXCEPTION", MSG_ERR_BANNEDIP, String.Empty, 0, userName);
                    throw new XmlRpcException(MSG_ERR_BANNEDIP);
                }

                // Validate user
                User = AuthenticationHelper.AuthenticateUser(userName, password, SiteName, false, AuthenticationSourceEnum.ExternalOrAPI); //K9 Change
            }

            if (User == null)
            {
                // User couldn't be authenticated
                EventLogProvider.LogEvent(EventType.ERROR, "MetaWeblog API", "EXCEPTION", MSG_ERR_USERNOTAUTH, String.Empty, 0, userName);
                throw new XmlRpcException(MSG_ERR_USERNOTAUTH);
            }

            return true;
        }


        /// <summary>
        /// Checks if the user is owner of the given document or global administrator.
        /// </summary>
        /// <param name="doc">Tree node representing blog</param>
        private bool IsUserOwner(TreeNode doc)
        {
            if (doc != null)
            {
                return (doc.NodeOwner == User.UserID) || User.IsGlobalAdministrator;
            }
            return false;
        }


        /// <summary>
        /// Checks if the user is allowed for specific document.
        /// </summary>
        /// <param name="doc">Tree node representing blog</param>
        /// <param name="permission">Indicates whether to check create permission</param>
        /// <param name="checkWorkflow">Indicates whether to check workflow</param>
        private bool IsUserAllowedForDocument(TreeNode doc, NodePermissionsEnum permission, bool checkWorkflow = false)
        {
            // If the user is owner of the document he can manage it.
            bool documentPermissionsOK = IsUserOwner(doc);

            var cui = new CurrentUserInfo(User, true);
            string errorMessage = null;

            if (!documentPermissionsOK && (cui.IsAuthorizedPerDocument(doc, permission) == AuthorizationResultEnum.Denied))
            {
                errorMessage = String.Format(MSG_ERR_USERNOTAUTHFORBLOGPERM, permission.ToString().ToLowerCSafe(), doc.NodeAliasPath);
            }

            // Check for workflow step authorization
            if (checkWorkflow && (WorkflowManager.GetNodeWorkflow(doc) != null))
            {
                WorkflowStepInfo wsi = WorkflowManager.GetStepInfo(doc);
                if (wsi != null)
                {
                    if (!wsi.StepIsDefault && !WorkflowManager.CheckStepPermissions(doc, User, WorkflowActionEnum.Approve))
                    {
                        errorMessage = String.Format(MSG_ERR_USERNOTAUTHFORWORKFLOWSTEP, wsi.StepName);
                    }
                }
            }

            if (!String.IsNullOrEmpty(errorMessage))
            {
                EventLogProvider.LogEvent(EventType.ERROR, "MetaWeblog API", "EXCEPTION", errorMessage, String.Empty, User.UserID, User.UserName, 0, doc.GetDocumentName(), String.Empty, doc.NodeSiteID);
                throw new XmlRpcException(errorMessage);
            }

            return true;
        }

        #endregion


        #region "Private methods"

        /// <summary>
        /// Gets a node representing blog according specified blog ID.
        /// </summary>
        /// <param name="blogid">ID of the blog to retrieve</param>
        private TreeNode GetBlogNodeById(string blogid)
        {
            int blogId = ValidationHelper.GetInteger(blogid, 0);

            // Get information about related blog node alias path and culture
            if ((blogId > 0) && (User != null) && (SiteName != String.Empty) && (TreeProvider != null))
            {
                // Search for the requested blog node
                DataSet result = DocumentHelper.GetDocuments(SiteName, "/%", TreeProvider.ALL_CULTURES, true, "cms.blog", "DocumentForeignKeyValue=" + blogId, null, -1, false, TreeProvider);
                if (!DataHelper.DataSourceIsEmpty(result))
                {
                    return TreeNode.New("cms.blog", result.Tables[0].Rows[0], TreeProvider);
                }
            }

            throw new XmlRpcException(MSG_ERR_BLOGUNAVAILABLE);
        }


        /// <summary>
        /// Gets blog post according given ID.
        /// </summary>
        /// <param name="postid">ID of the blog post to retrieve</param>
        private TreeNode GetBlogPostById(string postid)
        {
            int postId = ValidationHelper.GetInteger(postid, 0);
            if (postId > 0)
            {
                DataSet result = DocumentHelper.GetDocuments(SiteName, "/%", TreeProvider.ALL_CULTURES, true, "cms.blogpost", "BlogPostID=" + postId, null, -1, false, TreeProvider);
                if (!DataHelper.DataSourceIsEmpty(result))
                {
                    return TreeNode.New("cms.blogpost", result.Tables[0].Rows[0], TreeProvider);
                }
            }

            // Blog post doesn't exist in system
            throw new XmlRpcException(MSG_ERR_BLOGPOSTUNAVAILABLE);
        }


        /// <summary>
        /// Gets all categories related to the specified post.
        /// </summary>
        /// <param name="postDocId">ID of the blog post document categories should be related to</param>
        private static string[] GetBlogPostTags(int postDocId)
        {
            string[] postTags = null;

            if (postDocId > 0)
            {
                // Get post tags
                DataSet tags = TagInfoProvider.GetTags(postDocId, null, "TagName ASC");
                if (!DataHelper.DataSourceIsEmpty(tags))
                {
                    int currTagIndex = 0;

                    postTags = new string[tags.Tables[0].Rows.Count];

                    foreach (DataRow dr in tags.Tables[0].Rows)
                    {
                        postTags[currTagIndex] = dr["TagName"].ToString();
                        currTagIndex++;
                    }
                }
            }

            return postTags;
        }


        /// <summary>
        /// Gets a publish date of specified blog post. Value is returned in server time zone.
        /// </summary>
        /// <param name="postCreated">Post create date received from client</param>
        private static DateTime GetBlogPostPublishDate(DateTime postCreated)
        {
            // Get publish date and convert it into server time zone
            if (postCreated != DateTime.MinValue)
            {
                var utc = new TimeZoneInfo
                {
                    TimeZoneDaylight = false,
                    TimeZoneGMT = 0
                };

                if (TimeZoneHelper.ServerTimeZone != null)
                {
                    return TimeZoneHelper.ConvertTimeZoneDateTime(postCreated, utc, TimeZoneHelper.ServerTimeZone, false);
                }
                return postCreated;
            }

            return DateTime.MinValue;
        }


        /// <summary>
        /// Initializes current blog information according specified ID.
        /// </summary>
        /// <param name="blogId">ID of the current blog</param>
        private void EnsureBlogInfo(string blogId)
        {
            // Ensure removal of previous blog node
            BlogNode = null;
            BlogNode = GetBlogNodeById(blogId);
        }


        /// <summary>
        /// Returns node representing blog that post belongs to.
        /// </summary>
        /// <param name="post">Post the blog node is retrieved for</param>
        private TreeNode GetBlogNodeByPost(TreeNode post)
        {
            if (post != null)
            {
                // Get parent path
                string where = TreeProvider.GetNodesOnPathWhereCondition(post.NodeAliasPath, false, false);

                // Get blog nodes matching path
                DataSet ds = DocumentHelper.GetDocuments(SiteName, "/%", post.DocumentCulture, true, "cms.blog", where, "NodeLevel DESC", -1, false, 1, TreeProvider);

                // If found, return the blog node
                if (!DataHelper.DataSourceIsEmpty(ds))
                {
                    return TreeNode.New("cms.blog", ds.Tables[0].Rows[0]);
                }
            }

            return null;
        }


        /// <summary>
        /// Unpublishes blog post by workflow, or "published to" value.
        /// </summary>
        /// <param name="post">Blog post node</param>
        private void Unpublish(TreeNode post)
        {
            EnsureWorkflowScope(post);
            if (post.DocumentWorkflowStepID == 0)
            {
                post.SetValue("DocumentPublishTo", DateTime.Now);
            }
            else
            {
                post.MoveToFirstStep();
            }
            DocumentHelper.UpdateDocument(post, TreeProvider);
        }


        /// <summary>
        /// Publishes blog post.
        /// </summary>
        /// <param name="post">Blog post node</param>
        private void Publish(TreeNode post)
        {
            EnsureWorkflowScope(post);
            if (post.DocumentWorkflowStepID == 0)
            {
                PublishWithoutWorkflow(post);
            }
            else
            {
                PublishWithWorkflow(post);
            }
            DocumentHelper.UpdateDocument(post, TreeProvider);
        }


        /// <summary>
        /// Publishes blog post through workflow.
        /// </summary>
        /// <param name="post">Blog post node</param>
        private void PublishWithWorkflow(TreeNode post)
        {
            var step = post.Publish();
            if (step == null)
            {
                throw new XmlRpcException(MSG_ERR_WORKFLOWLOOP);
            }
            if (!step.StepIsPublished)
            {
                throw new XmlRpcException(MSG_ERR_WORKFLOWPATHNOTFOUND);
            }
            DocumentHelper.UpdateDocument(post, TreeProvider);
        }


        /// <summary>
        /// Publishes node through "publish from", "publish to" values.
        /// </summary>
        /// <param name="post">Blog post node</param>
        private void PublishWithoutWorkflow(TreeNode post)
        {
            if (post.DocumentPublishTo < DateTime.Now)
            {
                post.SetValue("DocumentPublishTo", null);
            }
            DocumentHelper.UpdateDocument(post, TreeProvider);
        }


        /// <summary>
        /// Cleans workflow information if document should not have one.
        /// </summary>
        /// <param name="post"></param>
        private void EnsureWorkflowScope(TreeNode post)
        {
            WorkflowScopeInfo wsi = WorkflowManager.GetNodeWorkflowScope(post);
            if (wsi == null)
            {
                VersionManager vm = VersionManager.GetInstance(post.TreeProvider);
                vm.RemoveWorkflow(post);
            }
        }


        /// <summary>
        /// Saves the temporary attachments to the blog post.
        /// </summary>
        /// <param name="postNode">Blog post node</param>
        /// <param name="deleteExisting">Determines whether to delete existing attachments first</param>
        private void PublishAttachments(TreeNode postNode, bool deleteExisting)
        {
            if (deleteExisting && IsUserAllowedForDocument(postNode, NodePermissionsEnum.Modify))
            {
                DataSet ds = DocumentHelper.GetAttachments(postNode, "AttachmentIsUnsorted = 1", null, false, TreeProvider);
                if (!DataHelper.DataSourceIsEmpty(ds))
                {
                    // Get list of attachment GUIDs from post text (both summary as well as body)
                    ArrayList attGuids = GetAttachmentsGuid(postNode);
                    bool listEmpty = (attGuids == null);

                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        Guid attGuid = ValidationHelper.GetGuid(dr["AttachmentGUID"], Guid.Empty);

                        // Skip the attachment when still being used by the post
                        if (!listEmpty && attGuids.Contains(attGuid))
                        {
                            continue;
                        }

                        // Delete unused attachment
                        DocumentHelper.DeleteAttachment(postNode, attGuid, TreeProvider);
                    }
                }
            }
            if ((blogGuid[BlogNode.NodeID] != null) && (postNode != null))
            {
                DocumentHelper.SaveTemporaryAttachments(postNode, (Guid)blogGuid[BlogNode.NodeID], SiteName, TreeProvider);
            }
        }


        /// <summary>
        /// Gets a list of attachment GUIDs found in the post summary and body fields.
        /// </summary>
        /// <param name="postNode">Blog post node</param>
        private ArrayList GetAttachmentsGuid(TreeNode postNode)
        {
            ArrayList result = null;

            if (postNode != null)
            {
                string postSummary = ValidationHelper.GetString(postNode.GetValue("BlogPostSummary"), String.Empty);
                string postBody = ValidationHelper.GetString(postNode.GetValue("BlogPostBody"), String.Empty);

                // Summary
                result = GetGuidsList(result, postSummary);
                // Body
                result = GetGuidsList(result, postBody);
            }

            return result;
        }


        /// <summary>
        /// Fills given list by attachments GUIDs.
        /// </summary>
        /// <param name="list">List to be filled by GUIDs</param>
        /// <param name="input">Input text to be searched for attachments GUIDs</param>
        private ArrayList GetGuidsList(ArrayList list, string input)
        {
            if (String.IsNullOrEmpty(input))
            {
                return list;
            }

            // Match all GUIDs in the input
            MatchCollection mc = AttachmentGuidRegex.Matches(input);
            if (mc.Count <= 0)
            {
                return list;
            }

            if (list == null)
            {
                list = new ArrayList();
            }

            // Fill GUIDs list
            foreach (Match m in mc)
            {
                Guid attGuid = ValidationHelper.GetGuid(m.Groups["guid"].Value, Guid.Empty);
                list.Add(attGuid);
            }

            return list;
        }


        /// <summary>
        /// Returns summary text if automatic summary is enabled.
        /// </summary>
        /// <param name="text">Text summary should comes from</param>
        private string GetPostSummary(string text)
        {
            string result = String.Empty;

            if (BlogPostSummaryEnabled)
            {
                // Strip tags                
                result = HTMLHelper.StripTags(text, false);

                // Trim length
                result = TextHelper.LimitLength(result, BlogPostSummaryLength);
            }

            return result;
        }


        /// <summary>
        /// Gets string representation of given tags array.
        /// </summary>
        /// <param name="tags">Array containing post tags</param>
        private static string GetTagsString(IEnumerable<string> tags)
        {
            StringBuilder str = new StringBuilder(null);

            // Create string containing tags wrapped by '"' and separated by ','
            foreach (string tagName in tags)
            {
                str.Append("\"");
                str.Append(tagName);
                str.Append("\"");
                str.Append(",");
            }

            return str.ToString().TrimEnd(',');
        }

        #endregion
    }
}