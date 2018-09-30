namespace E621_PoolDownloader
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using Core;
    using Models;
    using MessageBox = System.Windows.MessageBox;

    /// <summary>
    ///     Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private string BaseDirectory { get; set; }

        private WebClient WebClient
        {
            get
            {
                var wc = new WebClient();
                wc.Headers.Add("user-agent", "E621 PoolDownloader/1.0 (by vito on e621)");
                return wc;
            }
        }

        private string SelectDirectory()
        {
            if (this.BaseDirectory != null)
            {
                return this.BaseDirectory;
            }

            using (var fbd = new FolderBrowserDialog())
            {
                var result = fbd.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    this.BaseDirectory = fbd.SelectedPath;
                }
            }

            return this.BaseDirectory;
        }

        private void DownloadPoolsByTag_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private async void DownloadPoolListButton_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                this.PostsDownloadProgress.IsIndeterminate = true;
                this.DownloadPoolListButton.IsEnabled = false;
                this.PostsDownloadProgress.Value = 0;
            });

            var directory = this.SelectDirectory();
            var api = new E621Api();
            var tags = this.DownloadPoolListUrl.Text;

            void updateMethod(float? percent, string status)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (percent.Value != null)
                    {
                        this.PostsDownloadProgress.IsIndeterminate = false;
                        this.PostsDownloadProgress.Value = percent.Value;
                    }
                    else if (percent.Value == 100)
                    {
                        this.PostsDownloadProgress.IsIndeterminate = true;
                    }

                    this.DownloadPoolsStatus.Content = status;
                });
            };

            if (this.DownloadMethodPools.IsChecked == true)
            {
                await api.DownloadPoolsByTagsAsync(tags, directory, updateMethod);
            }
            if (this.DownloadMethodPosts.IsChecked == true)
            {
                await api.DownloadPostsByTagsAsync(tags, directory, updateMethod);
            }
            

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                this.DownloadPoolListButton.IsEnabled = true;
                this.PostsDownloadProgress.Value = 0;
                MessageBox.Show("Download finished!");
            });
        }
    }
}