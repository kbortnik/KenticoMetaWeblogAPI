using System;

using CookComputing.XmlRpc;

namespace CMS.MetaWeblogProvider
{
    /// <summary>
    /// MetaWeblog API objects class.
    /// </summary>
    public class MetaWeblogAPIObjects
    {
        #region Structs

        /// <summary>
        /// Blog detail info.
        /// </summary>
        public struct BlogInfo
        {
            /// <summary>
            /// Blog identifier.
            /// </summary>
            public string blogid;

            /// <summary>
            /// Blog URL.
            /// </summary>
            public string url;

            /// <summary>
            /// Blog code name.
            /// </summary>
            public string blogName;
        }


        /// <summary>
        /// Category detail info.
        /// </summary>
        public struct Category
        {
            /// <summary>
            /// Category identifier.
            /// </summary>
            public string categoryId;

            /// <summary>
            /// Category code name.
            /// </summary>
            public string categoryName;
        }


        /// <summary>
        /// Complete category information.
        /// </summary>
        public struct CategoryInfo
        {
            /// <summary>
            /// Category description.
            /// </summary>
            public string description;

            /// <summary>
            /// HTML URL.
            /// </summary>
            public string htmlUrl;

            /// <summary>
            /// RSS URL.
            /// </summary>
            public string rssUrl;

            /// <summary>
            /// Title.
            /// </summary>
            public string title;

            /// <summary>
            /// Category identifier.
            /// </summary>
            public string categoryid;
        }


        /// <summary>
        /// Enclosure detail info.
        /// </summary>
        [XmlRpcMissingMapping(MappingAction.Ignore)]
        public struct Enclosure
        {
            /// <summary>
            /// Length.
            /// </summary>
            public int length;

            /// <summary>
            /// Type.
            /// </summary>
            public string type;

            /// <summary>
            /// URL.
            /// </summary>
            public string url;
        }


        /// <summary>
        /// Post complete info.
        /// </summary>
        [XmlRpcMissingMapping(MappingAction.Ignore)]
        public struct Post
        {
            /// <summary>
            /// Date and time post created.
            /// </summary>
            [XmlRpcMissingMapping(MappingAction.Error)]
            public DateTime dateCreated;


            /// <summary>
            /// Post description.
            /// </summary>
            [XmlRpcMissingMapping(MappingAction.Error)]
            public string description;


            /// <summary>
            /// Post title.
            /// </summary>
            [XmlRpcMissingMapping(MappingAction.Error)]
            public string title;


            /// <summary>
            /// Post categories.
            /// </summary>
            public string[] categories;


            /// <summary>
            /// Post permanent link.
            /// </summary>
            public string permalink;


            /// <summary>
            /// Post identifier.
            /// </summary>
            public object postid;


            /// <summary>
            /// Post user identifier.
            /// </summary>
            public string userid;


            /// <summary>
            /// WorldPress slug.
            /// </summary>
            public string wp_slug;
        }


        /// <summary>
        /// Source information.
        /// </summary>
        [XmlRpcMissingMapping(MappingAction.Ignore)]
        public struct Source
        {
            /// <summary>
            /// Source name.
            /// </summary>
            public string name;


            /// <summary>
            /// Source URL.
            /// </summary>
            public string url;
        }


        /// <summary>
        /// Blog related user info.
        /// </summary>
        public struct UserInfo
        {
            /// <summary>
            /// User identifier.
            /// </summary>
            public string userid;


            /// <summary>
            /// User first name.
            /// </summary>
            public string firstname;


            /// <summary>
            /// User last name.
            /// </summary>
            public string lastname;


            /// <summary>
            /// User nick name.
            /// </summary>
            public string nickname;


            /// <summary>
            /// User email.
            /// </summary>
            public string email;


            /// <summary>
            /// User URL.
            /// </summary>
            public string url;
        }


        /// <summary>
        /// Media object structure (image, video, audio, etc.).
        /// </summary>
        [XmlRpcMissingMapping(MappingAction.Ignore)]
        public struct MediaObject
        {
            /// <summary>
            /// Object name.
            /// </summary>
            public string name;


            /// <summary>
            /// Object type.
            /// </summary>
            public string type;


            /// <summary>
            /// Object data.
            /// </summary>
            public byte[] bits;
        }


        /// <summary>
        /// Media object information.
        /// </summary>
        public struct MediaObjectInfo
        {
            /// <summary>
            /// Media object URL.
            /// </summary>
            public string url;
        }

        #endregion
    }
}