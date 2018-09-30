namespace E621_PoolDownloader.Helper
{
    using System;
    using System.Xml.Linq;

    public static class XmlHelper
    {
        public static XElement GetXmlFromUrl(string url, int tryCount = 0)
        {
            if (tryCount > 5)
            {
                throw new Exception("Xml could not be retrieved.");
            }
            try
            {
                tryCount++;
                var data = WebClientHelper.GetE621WebClient().DownloadString(url);
                var elem = XElement.Parse(data);
                return elem;
            }
            catch (Exception ex)
            {
                return GetXmlFromUrl(url, tryCount);
            }
        }
    }
}