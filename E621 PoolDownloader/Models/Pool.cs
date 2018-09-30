namespace E621_PoolDownloader.Models
{
    using System.Xml.Linq;
    using Core;

    public class Pool
    {
        public int Id { get; }

        public string Name { get; private set; }

        public XElement Data { get; private set; }

        private E621Api Api { get; }

        public Pool(E621Api api, int id, bool load = true)
        {
            this.Api = api;
            this.Id = id;
            if (load)
            {
                this.LoadData();
            }
        }

        public void LoadData()
        {
            this.Data = this.Api.GetPoolData(this.Id);
            this.Name = this.Data.Attribute("name").Value;
        }
    }
}