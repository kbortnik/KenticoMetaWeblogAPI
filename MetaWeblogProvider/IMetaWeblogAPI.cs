using System;

using CookComputing.XmlRpc;

namespace CMS.MetaWeblogProvider
{
    /// <summary>
    /// MetaWeblog API interface.
    /// </summary>
    public interface IMetaWeblogAPI
    {
        #region "MetaWeblog API methods"

        /// <summary>
        /// Inserts the new blog post within specified blog.
        /// </summary>
        /// <param name="blogid">ID of the blog post is added to</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="post">Blog post info</param>
        /// <param name="publish">Indicates whether the blog post should be published. If false it is a draft post</param>
        [XmlRpcMethod("metaWeblog.newPost")]
        string AddPost(string blogid, string username, string password, MetaWeblogAPIObjects.Post post, bool publish);


        /// <summary>
        /// Updates information on specified blog post.
        /// </summary>
        /// <param name="postid">ID of the post to update</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="post">Blog post info</param>
        /// <param name="publish">Indicates whether the blog post should be published. If false it is a draft post</param>
        [XmlRpcMethod("metaWeblog.editPost")]
        bool UpdatePost(string postid, string username, string password, MetaWeblogAPIObjects.Post post, bool publish);


        /// <summary>
        /// Gets a specific post of the specified blog.
        /// </summary>
        /// <param name="postid">ID of the post to retrieve</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        [XmlRpcMethod("metaWeblog.getPost")]
        MetaWeblogAPIObjects.Post GetPost(string postid, string username, string password);


        /// <summary>
        /// Gets a list of tags used in the particular blog.
        /// </summary>
        /// <param name="blogid">ID of the blog tags are retrieved for</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        [XmlRpcMethod("metaWeblog.getCategories")]
        MetaWeblogAPIObjects.CategoryInfo[] GetCategories(string blogid, string username, string password);


        /// <summary>
        /// Gets a set of the most recent blog posts in descending order by publish date.
        /// </summary>
        /// <param name="blogid">ID of the blog</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="numberOfPosts">Indicates the number of returned posts</param>
        [XmlRpcMethod("metaWeblog.getRecentPosts")]
        MetaWeblogAPIObjects.Post[] GetRecentPosts(string blogid, string username, string password, int numberOfPosts);


        /// <summary>
        /// Creates a new media object on the server side for later use in the blog.
        /// </summary>
        /// <param name="blogid">ID of the blog</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="mediaObject">Media object info</param>
        [XmlRpcMethod("metaWeblog.newMediaObject")]
        MetaWeblogAPIObjects.MediaObjectInfo NewMediaObject(string blogid, string username, string password, MetaWeblogAPIObjects.MediaObject mediaObject);

        #endregion


        #region "Blogger API (Meta Weblog 2) methods"

        /// <summary>
        /// Removes blog post.
        /// </summary>
        /// <param name="key">This value is ignored. Required to keep blog compatibility</param>
        /// <param name="postid">ID of the post to delete</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        /// <param name="publish">This value is ignored. Required to keep blog compatibility</param>
        /// <returns></returns>
        [XmlRpcMethod("blogger.deletePost")]
        [return: XmlRpcReturnValue(Description = "Returns true.")]
        bool DeletePost(string key, string postid, string username, string password, bool publish);


        /// <summary>
        /// Gets a list of user's blog posts.
        /// </summary>
        /// <param name="key">This value is ignored. Required to keep blog applications compatibility</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        [XmlRpcMethod("blogger.getUsersBlogs")]
        MetaWeblogAPIObjects.BlogInfo[] GetUsersBlogs(string key, string username, string password);


        /// <summary>
        /// Gets an info on specific blog user.
        /// </summary>
        /// <param name="key">This value is ignored. Required to keep blog applications compatibility</param>
        /// <param name="username">Name of the user performing action</param>
        /// <param name="password">Password to access user's account</param>
        [XmlRpcMethod("blogger.getUserInfo")]
        MetaWeblogAPIObjects.UserInfo GetUserInfo(string key, string username, string password);

        #endregion
    }
}