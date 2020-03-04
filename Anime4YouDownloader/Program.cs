using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Anime4YouDownloader
{
    class Program
    {
        static List<Episode> episodes = new List<Episode>();
        static ProgressBar progress;
        static int count = 0;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("ERROR: No series specified");
                Environment.Exit(0);
            }

            string url = args[0];
            if (url.Contains("epi/"))
            {
                url = url.Split(new string[] { "epi/" }, StringSplitOptions.None)[0];
            }

            fetchEpisodes(args[0]);
            
            foreach (var episode in episodes)
            {
                string streamUrl = videoFetch(episode.vivourl);
                string dirName = MakeValidFileName(episode.showName);
                Directory.CreateDirectory(dirName);
                progress = new ProgressBar();

                string filename = MakeValidFileName(episode.showName + " - " + "Episode " + episode.episodeNumber + ".mp4");
                Console.WriteLine("Downloading; " + filename + " ");

                Task.Run(async () =>
                {
                    using (var client = new WebClient())
                    {
                        client.DownloadProgressChanged += wc_DownloadProgressChanged;

                        await client.DownloadFileTaskAsync(new Uri(streamUrl), Path.Combine(dirName, filename));
                    }
                }).GetAwaiter().GetResult();
                Console.WriteLine();

            }
        }

        static void fetchEpisodes(string url)
        {
            string baseUrl = "https://www.anime4you.one";
            Console.WriteLine("Fetching show episodes...");

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            HtmlNode titlenode = doc.DocumentNode.SelectSingleNode("//div[@class='titel']//h3/text()");            
            HtmlNodeCollection episodelistnodes = doc.DocumentNode.SelectNodes("//div[@id='episodenliste']//li//a");

            foreach (HtmlNode node in episodelistnodes)
            {
                if (count > 20)
                {
                    Console.WriteLine("Server is freaking out, waiting for 10 seconds.");
                    Thread.Sleep(10000);
                    count = 0;
                }

                Uri episode = new Uri(baseUrl + node.Attributes[1].Value);
                int episodenum = Convert.ToInt32(episode.Segments[6].Replace("/", ""));
                int seriesnum = Convert.ToInt32(episode.Segments[4].Replace("/", ""));

                Episode episodeInfo = fetchStreamInfo(titlenode.InnerText, episodenum, seriesnum, baseUrl + node.Attributes[1].Value);
                count++;
                episodes.Add(episodeInfo);

            }
            Console.WriteLine("Found " + episodes.Count + " episodes of " + titlenode.InnerText);

        }

        static Episode fetchStreamInfo(string showName, int episodeNum, int seriesID, string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.anime4you.one/check_hoster.php");

                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/82.0.4076.0 Safari/537.36";
                request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                request.Headers.Add("Origin", @"https://www.anime4you.one");
                request.Headers.Add("Sec-Fetch-Site", @"same-origin");
                request.Headers.Add("Sec-Fetch-Mode", @"cors");
                request.Headers.Add("Sec-Fetch-Dest", @"empty");
                request.Referer = "https://www.anime4you.one/";
                request.Method = "POST";

                string body = @"epi=" + episodeNum + "&aid=" + seriesID + "&act=" + episodeNum + "&vkey=&username=";
                byte[] postBytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = postBytes.Length;
                Stream stream = request.GetRequestStream();
                stream.Write(postBytes, 0, postBytes.Length);
                stream.Close();

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = new StreamReader(receiveStream); ;
                string data = readStream.ReadToEnd();

                var doc = new HtmlDocument();
                doc.LoadHtml(data);
                HtmlNodeCollection nodeCollection = doc.DocumentNode.SelectNodes("//ul[@class='streamer']//li//button");

                string vivoUrl = nodeCollection[0].Attributes[0].Value;

                Episode episode = new Episode
                {
                    showName = showName,
                    episodeNumber = episodeNum,
                    seriesID = seriesID,
                    a4yurl = url,
                    vivourl = vivoUrl,
                };

                return episode;
            }
            catch (WebException e)
            {

                throw;
            }
        }

        static string videoFetch(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/82.0.4076.0 Safari/537.36";
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            
            Stream receiveStream = response.GetResponseStream();
            StreamReader readStream = new StreamReader(receiveStream); ;
            string data = readStream.ReadToEnd();

            var doc = new HtmlDocument();
            doc.LoadHtml(data);
            var nodeCollection = doc.DocumentNode.SelectNodes("//script");


            foreach (var node in nodeCollection)
            {
                if (node.InnerText.Contains("Core.InitializeStream"))
                {
                    foreach (var items in node.InnerText.Split(','))
                    {
                        if (items.Contains("source:"))
                        {
                            return Rot47(Uri.UnescapeDataString(items.Split('\'')[1]));
                        }
                    }
                }
            }
            return "";
        }

        public static string Rot47(string input)
        {
            return !string.IsNullOrEmpty(input) ? new string(input.Select(x =>
                (x >= 33 && x <= 126) ? (char)((x + 14) % 94 + 33) : x).ToArray()) : input;
        }

        private static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "");
        }
        
        static void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            progress.Report((double)e.ProgressPercentage / 100);
        }
    }
}
