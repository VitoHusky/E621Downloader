namespace E621_PoolDownloader.Helper
{
    using System.Net;

    public static class WebClientHelper
    {
        public static WebClient GetE621WebClient()
        {
            var wc = new WebClient();
            wc.Headers.Add("user-agent", "E621 PoolDownloader/1.0 (by vito on e621)");
            return wc;
        }
    }
}