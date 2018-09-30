namespace E621_PoolDownloader.Models
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Documents;
    using System.Xml.Linq;
    using Core;

    public class Post
    {
        private static List<Post> Cache { get; } = new List<Post>();

        public int Id { get; }

        public string FileUrl { get; private set; }

        public XElement Data { get; private set; }

        private E621Api Api { get; }

        public static Post Get(E621Api api, int id)
        {
            return Get(api, id, null);
        }

        public static Post Get(E621Api api, int id, XElement data)
        {
            var cache = Cache.FirstOrDefault(x => x.Id == id);
            if (cache != null)
            {
                return cache;
            }

            return new Post(api, id, data);
        }

        private Post(E621Api api, int id, XElement data)
        {
            this.Api = api;
            this.Id = id;
            if (data == null)
            {
                this.Data = this.Api.GetPostData(this.Id);
                this.FileUrl = this.Data.Element("file_url").Value;
            }
            else
            {
                this.Data = data;
                this.FileUrl = this.Data.Element("file_url").Value;
            }
        }
    }
}