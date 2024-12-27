using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Knapcode.TorSharp;

namespace Zlibrary网址获取器
{
    public partial class MainWindow : Window
    {
        private const string DefaultTorUrl = "http://ethanielragn6ug6op5w3x2f6rawzzzu7qfrsksaaq527oltkmmu7yad.onion/index.txt"; // Tor的网址

        private static readonly string[] ChineseUrls = new[] {  //中文网址
            "https://z-lib.100713.xyz/index.txt",
            "https://z-libdomain.100713.xyz/index.txt",
            "https://zlib.100713.xyz/index.txt",  //100713.xyz
            "https://z-lib.2010hcy.us.kg/index.txt",
            "https://zlib.2010hcy.us.kg/index.txt",
            "https://z-libdomain.2010hcy.us.kg/index.txt",  //2010hcy.us.kg
            "https://z-lib.broker.us.kg/index.txt",
            "https://zlib.broker.us.kg/index.txt",
            "https://z-libdomain.broker.us.kg/index.txt",  //broker.us.kg
            "https://z-librarydomain.pages.dev/index.txt",
        };

        private static readonly string[] EnglishUrls = new[] {   //英文的网址
            "https://z-lib.100713.xyz/index-EN.txt",
            "https://z-libdomain.100713.xyz/index-EN.txt",
            "https://zlib.100713.xyz/index-EN.txt",  //100713.xyz
            "https://z-lib.2010hcy.us.kg/index-EN.txt",
            "https://zlib.2010hcy.us.kg/index-EN.txt",
            "https://z-libdomain.2010hcy.us.kg/index-EN.txt",  //2010hcy.us.kg
            "https://z-lib.broker.us.kg/index-EN.txt",
            "https://zlib.broker.us.kg/index-EN.txt",
            "https://z-libdomain.broker.us.kg/index-EN.txt",  //broker.us.kg
            "https://z-librarydomain.pages.dev/index-EN.txt",
        };

        private bool isEnglish;

        public MainWindow()
        {
            InitializeLanguage();
            InitializeComponent();
        }

        private void InitializeLanguage()
        {
            // 获取系统语言
            var currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // 根据系统语言设置显示的语言（是网址框里的，不是应用程序的）
            isEnglish = currentCulture == "en";
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await FetchContentOnStartupAsync();
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            // 显示加载中提示，不然鬼知道有没有在干活
            LoadingTextBlock.Visibility = Visibility.Visible;

            // 加载内容
            await FetchContentOnStartupAsync();

            // 隐藏加载中提示
            LoadingTextBlock.Visibility = Visibility.Collapsed;
        }

        private async Task FetchContentOnStartupAsync()
        {
            var urls = isEnglish ? EnglishUrls : ChineseUrls;
            string url = await GetFastestUrlAsync(urls) ?? DefaultTorUrl;

            if (url == DefaultTorUrl)
            {
                await FetchContentUsingTorAsync(url);
            }
            else
            {
                await FetchContentAsync(url);
            }
        }

        private async Task<string> GetFastestUrlAsync(string[] urls)
        {
            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                string fastestUrl = null;
                long fastestTime = long.MaxValue;

                foreach (var url in urls)
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var response = await httpClient.GetAsync(url);
                        stopwatch.Stop();

                        if (response.IsSuccessStatusCode)
                        {
                            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                            if (elapsedMilliseconds < fastestTime)
                            {
                                fastestTime = elapsedMilliseconds;
                                fastestUrl = url;
                            }
                        }
                    }
                    catch
                    {
                        // 忽略掉访问失败的域名，放松，访问失败是正常的
                    }
                }

                return fastestUrl;
            }
        }

        private async Task FetchContentAsync(string url)
        {
            try
            {
                using (var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    Dispatcher.Invoke(() => ResponseTextBox.Text = content);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ResponseTextBox.Text = $"获取内容出错 {url}: {ex.Message}");
            }
        }

        private async Task FetchContentUsingTorAsync(string url)
        {
            var settings = new TorSharpSettings
            {
                ZippedToolsDirectory = "tor-tools",
                ExtractedToolsDirectory = "tor-extracted",
                PrivoxySettings = new TorSharpPrivoxySettings { Port = 18118 },
                TorSettings = new TorSharpTorSettings
                {
                    SocksPort = 19050,
                    ControlPort = 19051
                }
            };

            var proxy = new TorSharpProxy(settings);

            try
            {
                Dispatcher.Invoke(() => LoadingTextBlock.Visibility = Visibility.Visible);

                await new TorSharpToolFetcher(settings, new HttpClient()).FetchAsync();

                await proxy.ConfigureAndStartAsync();

                var handler = new HttpClientHandler
                {
                    Proxy = new System.Net.WebProxy(new Uri("http://localhost:18118")),
                    UseProxy = true
                };

                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) })
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    Dispatcher.Invoke(() => ResponseTextBox.Text = content);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ResponseTextBox.Text = $"使用压轴手段失败: {ex.Message}");
            }
            finally
            {
                Dispatcher.Invoke(() => LoadingTextBlock.Visibility = Visibility.Collapsed);
                proxy.Stop();
            }
        }
    }
}