namespace E621_PoolDownloader.Core
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Helper;
    using Models;

    public class E621Api
    {
        public async Task DownloadPostsByTagsAsync(string tags, string targetDirAll, Action<float?, string> onUpdate)
        {
            var postsFound = 0;
            var postsDownloaded = 0;
            var currentDownloadingPosts = 0;

            void sendUpdate()
            {
                var str = "Downloading posts...\n" +
                          $"Posts: {postsDownloaded} / {postsFound} (Downloading {currentDownloadingPosts})\n";

                var percent = 0f;
                if (postsFound != 0)
                {
                    percent = postsDownloaded * 100 / postsFound;
                }
                onUpdate.Invoke(percent, str);
            }

            await Task.Run(async () =>
            {
                var tasks = new List<Task>();
                foreach (var post in this.GetPostsByTags(tags))
                {
                    postsFound++;
                    sendUpdate();

                    var task = Task.Run(async () =>
                    {
                        await this.PostDownloadSemaphore.WaitAsync().ConfigureAwait(true);
                        currentDownloadingPosts++;
                        sendUpdate();

                        var url = post.FileUrl;
                        var filename = post.Data.Element("id").Value + "." + post.Data.Element("file_ext").Value;
                        var file = Path.Combine(targetDirAll, filename);
                        if (!File.Exists(file))
                        {
                            var postdownloadtry = 0;
                            var downloaded = false;
                            do
                            {
                                postdownloadtry++;
                                try
                                {
                                    Debug.WriteLine($"Downloading Post {post.Id} START. Try: {postdownloadtry}");
                                    await WebClientHelper.GetE621WebClient().DownloadFileTaskAsync(new Uri(url), file);
                                    downloaded = true;
                                }
                                catch
                                {
                                    Debug.WriteLine($"Downloading Post {post.Id} FAILED. Try: {postdownloadtry}");
                                }
                            } while (!downloaded && postdownloadtry < 5);
                        }

                        this.PostDownloadSemaphore.Release();
                        postsDownloaded++;
                        currentDownloadingPosts--;
                        sendUpdate();
                    });
                    tasks.Add(task);
                }
                await Task.WhenAll(tasks);
            });
        }

        public async Task DownloadPoolsByTagsAsync(string tags, string targetDirAll, Action<float?, string> onUpdate)
        {
            var poolsFound = 0;
            var poolsDownloaded = 0;
            var postsFound = 0;
            var postsDownloaded = 0;
            var currentDownloadingPools = 0;
            var currentDownloadingPosts = 0;

            void sendUpdate()
            {
                var str = "Downloading pools...\n" +
                          $"Posts: {postsDownloaded} / {postsFound} (Downloading {currentDownloadingPosts})\n" +
                          $"Pools: {poolsDownloaded} / {poolsFound} (Downloading {currentDownloadingPools})\n";

                var percent = 0f;
                if (postsFound != 0)
                {
                    percent = postsDownloaded * 100 / postsFound;
                }
                onUpdate.Invoke(percent, str);
            }

            var tasks = new List<Task>();
            await Task.Run(() =>
            {
                foreach (var pool in this.GetPoolsByTag(tags))
                {
                    poolsFound++;
                    sendUpdate();

                    var dirname = pool.Name;
                    var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                    foreach (var c in invalid)
                    {
                        dirname = dirname.Replace(c.ToString(), "");
                    }

                    var targetDir = Path.Combine(targetDirAll, dirname);

                    var task = Task.Run(async () =>
                    {
                        currentDownloadingPools++;
                        sendUpdate();

                        await this.DownloadPostsByPoolIdAsync(pool.Id, targetDir,
                            () =>
                            {
                                postsFound++;
                                sendUpdate();
                            },
                            () =>
                            {
                                currentDownloadingPosts++;
                                sendUpdate();
                            },
                            () =>
                            {
                                currentDownloadingPosts--;
                                postsDownloaded++;
                                sendUpdate();
                            });

                        poolsDownloaded++;
                        currentDownloadingPools--;
                        sendUpdate();
                    });
                    tasks.Add(task);
                }
            });

            await Task.WhenAll(tasks);
        }

        private IEnumerable<Pool> GetPoolsByTag(string tags)
        {
            var foundIds = new List<int>();
            foreach (var post in this.GetPostsByTags(tags + " inpool:true"))
            {
                var ids = this.GetPoolIdsForPost(post);
                foreach (var id in ids)
                {
                    if (foundIds.Any(x => x == id))
                    {
                        continue;
                    }

                    foundIds.Add(id);
                    Debug.WriteLine($"Found Pool {id}");
                    yield return new Pool(this, id);
                }
            }
        }

        private IEnumerable<int> GetPoolIdsForPost(Post post)
        {
            var url = $"https://e621.net/post/show/{post.Id}";
            var data = WebClientHelper.GetE621WebClient().DownloadString(url);
            var regex = new Regex(@"\/pool\/show\/(\d+)");
            if (regex.IsMatch(data))
            {
                var ids = new List<int>();
                foreach (Match m in regex.Matches(data))
                {
                    ids.Add(Convert.ToInt32(m.Groups[1].Value));
                }

                return ids;
            }

            return null;
        }

        private SemaphoreSlim PoolDownloadSemaphore = new SemaphoreSlim(3);

        private IEnumerable<Post> GetPostsByTags(string tags)
        {
            var page = 1;
            var found = true;
            do
            {
                var url = $"https://e621.net/post/index.xml?tags={tags}&limit=50&page={page}";
                page++;
                var xml = XmlHelper.GetXmlFromUrl(url);
                foreach (var p in xml.Elements("post"))
                {
                    yield return Post.Get(this, Convert.ToInt32(p.Element("id").Value), p);
                }
            } while (found);
        }

        public XElement GetPoolData(int poolId)
        {
            return XmlHelper.GetXmlFromUrl($"https://e621.net/pool/show.xml?id={poolId}");
        }

        public XElement GetPostData(int postId)
        {
            return XmlHelper.GetXmlFromUrl($"https://e621.net/post/show.xml?id={postId}");
        }

        private IEnumerable<Post> GetPostsByPoolId(int poolId)
        {
            var poolData = this.GetPoolData(poolId);
            var index = 0;
            var page = 1;
            var work = true;
            var postCount = Convert.ToInt32(poolData.Attribute("post_count").Value);
            do
            {
                var posts = poolData.Element("posts").Elements("post");
                foreach (var p in posts)
                {
                    var postid = p.Element("id").Value;
                    index++;
                    yield return Post.Get(this, Convert.ToInt32(postid), p);
                }

                if (index == postCount)
                {
                    work = false;
                }
                else
                {
                    page++;
                    poolData = XmlHelper.GetXmlFromUrl($"https://e621.net/pool/show.xml?id={poolId}&page={page}");
                }
            } while (work);
        }

        private SemaphoreSlim PostDownloadSemaphore = new SemaphoreSlim(10);

        private async Task DownloadPostsByPoolIdAsync(int poolId, string directory,Action onNewPostDiscovered, Action onNewPostDownload, Action onFinishedPostDownload, int tryCount = 0)
        {
            if (tryCount > 5)
            {
                return;
            }

            Debug.WriteLine($"Downloading Pool {poolId}");
            await this.PoolDownloadSemaphore.WaitAsync().ConfigureAwait(true);
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tasks = new List<Task>();
                var index = 0;
                foreach (var p in this.GetPostsByPoolId(poolId))
                {
                    onNewPostDiscovered.Invoke();

                    await this.PostDownloadSemaphore.WaitAsync().ConfigureAwait(true);
                    Debug.WriteLine($"Downloading Post {p}");
                    onNewPostDownload.Invoke();

                    var url = p.FileUrl;
                    var file = Path.Combine(directory, index + "." + p.Data.Element("file_ext").Value);
                    if (File.Exists(file))
                    {
                        this.PostDownloadSemaphore.Release();
                        onFinishedPostDownload.Invoke();
                        continue;
                    }

                    var task = Task.Run(async () =>
                    {
                        var postdownloadtry = 0;
                        var downloaded = false;
                        do
                        {
                            postdownloadtry++;
                            try
                            {
                                await WebClientHelper.GetE621WebClient().DownloadFileTaskAsync(new Uri(url), file);
                                downloaded = true;
                            }
                            catch
                            {
                                Debug.WriteLine($"Downloading Post {p} FAILED. Try: {postdownloadtry}");
                            }
                        } while (!downloaded && postdownloadtry < 5);
                        

                        this.PostDownloadSemaphore.Release();
                        onFinishedPostDownload.Invoke();
                    });

                    index++;
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }
            catch(Exception ex)
            {
                tryCount++;
                await Task.Run(() => this.DownloadPostsByPoolIdAsync(poolId, directory, onNewPostDiscovered, onNewPostDownload, onFinishedPostDownload, tryCount));
            }
            finally
            {
                this.PoolDownloadSemaphore.Release();
            }
            
        }
    }
}