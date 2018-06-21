using EasyConsole;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Web_Crawler
{
    class Program
    {
        /// <summary>
        /// This applications path
        /// </summary>
        public static string pathCrawler = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// Log File Path
        /// </summary>
        public static string pathLogFile = $@"{pathCrawler}\Log.txt";

        /// <summary>
        /// Default Configuration File Path
        /// </summary>
        public static string configFilePath = $@"{pathCrawler}\crawler-config.json";

        /// <summary>
        /// URL to get top searches from
        /// </summary>
        public static string linkFileChefSearches = "http://www.filechef.com/searches/index"; // + (number)

        /// <summary>
        /// Sets root menu to display
        /// </summary>
        private static bool RootMenu = true;

        //
        
        public static string Header = $@"
                                                                                      
              __        __   _        ____                    _           
              \ \      / /__| |__    / ___|_ __ __ ___      _| | ___ _ __      / _ \
               \ \ /\ / / _ \ '_ \  | |   | '__/ _` \ \ /\ / / |/ _ \ '__|   \_\(_)/_/
                \ V  V /  __/ |_) | | |___| | | (_| |\ V  V /| |  __/ |       _//o\\_ 
                 \_/\_/ \___|_.__/   \____|_|  \__,_| \_/\_/ |_|\___|_|        /   \  
                                                                                      
                                                                                      
------------------------------------------------------------------------------------------------------
                                                                                      ";

        static void Main(string[] args)
        {
            Console.Title = "Web Crawler";

            FileTypes.All.AddRange(FileTypes.Video);
            FileTypes.All.AddRange(FileTypes.Image);
            FileTypes.All.AddRange(FileTypes.Audio);
            FileTypes.All.AddRange(FileTypes.Book);
            FileTypes.All.AddRange(FileTypes.Subtitle);
            FileTypes.All.AddRange(FileTypes.Torrent);
            FileTypes.All.AddRange(FileTypes.Software);
            FileTypes.All.AddRange(FileTypes.Other);

            while (RootMenu)
            {
                OutputTitle();

                var menu = new Menu()
                .Add("Start Web Crawler", () => StartCrawler())
                .Add("Write Top Searches", () => WriteTopSearches())
                .Add("Exit", () => Environment.Exit(0));
                menu.Display();
            }
        }

        static List<string> existingFileURLs = new List<string>();

        public static void StartCrawler()
        {
            RootMenu = false;

            OutputTitle();

            // Load users configuration json file
            OutputMessage($"Loading your configuration file...");
            var usersConfig = UsersConfig();

            // Checks if config PathToWrite exists on machine
            if (!Directory.Exists(usersConfig.PathToWrite))
                Directory.CreateDirectory(usersConfig.PathToWrite);

            List<string> webServers = new List<string>(); // Servers to loop

            /* Loads the list of web servers from either the local file or a web file containing the items */
            OutputMessage($"Loading web servers from [{usersConfig.ServerList}]...");

            if (Utilities.IsLocalFile(usersConfig.ServerList)) // Checks if file is local
            {
                if (!File.Exists(usersConfig.ServerList))
                {
                    OutputMessage($"Web servers list doesn't exist on your machine. Create a text file with your list of servers and replace [{usersConfig.ServerList}] with this file path.");
                    OutputPause();
                    RootMenu = true;
                    return;
                }
                else
                {
                    webServers.AddRange(File.ReadAllLines(usersConfig.ServerList));
                }
            }
            else if (Utilities.IsWebFile(usersConfig.ServerList)) // Checks if is web file
            {
                if (!Utilities.URLExists(usersConfig.ServerList))
                {
                    OutputMessage($"Web servers list doesn't exist on your machine. Create a text file with your list of servers and replace [{usersConfig.ServerList}] with this file path.");
                    OutputPause();
                    RootMenu = true;
                    return;
                }
                else
                {
                    webServers.AddRange(Utilities.LoadWebTextFileItems(usersConfig.ServerList, pathCrawler));
                }
            }
            else
            {
                OutputMessage($"Web servers list cannot be identified. Make sure this file exists either on your machine or a web server.");
                OutputPause();
                RootMenu = true;
                return;
            }

            OutputMessage($"Web servers loaded successfully.");

            // Directory to output results lists to (If we're writing to one file)
            var oneFilePathToWrite = $@"{usersConfig.PathToWrite}/{usersConfig.OneFileName}.json".Replace("/", @"\");

            // Deletes old list if we're re-writing, otherwise we load the existing files to not add duplicates
            if (usersConfig.RewriteList)
            {
                if (!File.Exists(oneFilePathToWrite))
                    File.Delete(oneFilePathToWrite);

                OutputMessage("Deleted old results file...");
            }
            else
            {
                using (FileStream fs = File.Open(oneFilePathToWrite, FileMode.Open))
                using (BufferedStream bs = new BufferedStream(fs))
                using (StreamReader sr = new StreamReader(bs))
                {
                    string s;
                    while ((s = sr.ReadLine()) != null)
                    {
                        try
                        {
                            existingFileURLs.Add(JsonConvert.DeserializeObject<WebFile>(s).URL);
                        }
                        catch { }
                    }
                }

                OutputMessage("Found existing files - (" + existingFileURLs.Count() + ")");
            }

            // Creates directory to output results lists to (If we're writing to multiple lists)
            var pathWriteListsTo = $@"{usersConfig.PathToWrite}\{DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")}\"; // Folder to be used for this instance of crawling

            if (!usersConfig.OneFile)
                Directory.CreateDirectory(pathWriteListsTo);

            // Sets the file types to be used
            var filesTypes = FileTypes.All;

            if (usersConfig.FileTypes == "*")
                filesTypes = FileTypes.All;
            else
                filesTypes = new List<string>(usersConfig.FileTypes.Split('|'));

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            int filesFound = 0;
            int filesAdded = 0;
            long filesSize = 0;

            /* Gets web files from web servers and writes links to them in specified path */
            foreach (var webServer in webServers)
            {
                try
                {
                    OutputTitle();
                    OutputMessage("Crawling " + webServer);

                    if (usersConfig.OneFile) // Writes results to one file
                    {
                        foreach (var webFile in GetWebFiles(webServer, usersConfig.SubDirectories, usersConfig.RequestTimeout, filesTypes))
                            if (!existingFileURLs.Contains(webFile.URL))
                                using (StreamWriter sw = File.AppendText(oneFilePathToWrite))
                                {
                                    sw.WriteLine(JsonConvert.SerializeObject(webFile));
                                    filesSize = filesSize + webFile.Size;
                                    OutputMessage("Found File : " + webFile.Name + " [" + webFile.URL + "]");
                                    filesAdded++;
                                }
                        filesFound++;
                    }
                    else // Writes results to multiple file paths (One directory for each host)
                    {
                        foreach (var webFile in GetWebFiles(webServer, usersConfig.SubDirectories, usersConfig.RequestTimeout, filesTypes))
                            if (!existingFileURLs.Contains(webFile.URL))
                                using (StreamWriter sw = File.AppendText($@"{pathWriteListsTo}\{webFile.Host}.json"))
                                {
                                    sw.WriteLine(JsonConvert.SerializeObject(webFile));
                                    OutputMessage("Found File : " + webFile.Name + " [" + webFile.URL + "]");
                                    filesSize = filesSize + webFile.Size;
                                    filesAdded++;
                                }
                        filesFound++;
                    }
                }
                catch (Exception ex) { LogMessage(ex.Message); }
            }

            stopWatch.Stop();

            OutputMessage("Web Crawl Completed. So, what now?");
            OutputResult(webServers.Count(), filesAdded, filesSize, new TimeSpan(stopWatch.ElapsedTicks), filesFound);
            OutputPause();

            RootMenu = true;
        }

        public static ConfigFile UsersConfig()
        {
            if (!File.Exists(configFilePath))
            {
                OutputMessage("Configuration file doesn't exist. Creating a default one, you will need to configure it yourself. You can find it at [" + pathCrawler + "]");
                File.WriteAllText(configFilePath, CrawlConfig.DefaultCrawlConfig);
            }

            var config = JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(configFilePath).Replace(@"\", "/"));
            return config;
        }

        static List<WebFile> foundFiles = new List<WebFile>();

        public static List<WebFile> GetWebFiles(string webServer, bool crawlSubDirectories, int requestTimeout, List<string> fileTypes)
        {
            var oldItemSplit = webServer.Split('/');
            Array.Resize(ref oldItemSplit, oldItemSplit.Length - 2);
            var previousItem = String.Join("/", oldItemSplit);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(webServer);
            request.Timeout = requestTimeout;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.Default))
                {
                    string htmlContent = reader.ReadToEnd();

                    Regex regex = new Regex("<a href=\".*\">(?<name>.*)</a>", RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);
                    MatchCollection matches = regex.Matches(htmlContent);
                    if (matches.Count > 0)
                    {
                        foreach (Match match in matches)
                        {
                            if (match.Success)
                            {
                                string listItem = match.Groups["name"].ToString();
                                string listItemURL = webServer + listItem;

                                if (Path.HasExtension(listItemURL)) // Checks if this item is a file, there must be a better way for this (Path.HasExtension)
                                {
                                    if (Utilities.URLExists(listItemURL)) // Checks if this file actually exists on the server
                                    {
                                        string formattedName = listItem.Replace("&amp;", "&"); // Replaces unicodes to their proper symbols
                                        formattedName = formattedName.StartsWith(" ") | formattedName.StartsWith("%20") ? formattedName.Substring(0, 1) : formattedName;

                                        if (fileTypes.Contains(Path.GetExtension(webServer + formattedName).Replace(".", "").ToUpper()))
                                        {
                                            var foundFile = new WebFile
                                            {
                                                Name = Path.GetFileNameWithoutExtension(new Uri(webServer + formattedName).LocalPath),
                                                URL = new Uri(webServer + formattedName).AbsoluteUri,
                                                Host = new Uri(webServer + formattedName).Host.Replace("www.", ""),
                                                Type = Path.GetExtension(webServer + formattedName).Replace(".", "").ToUpper(),
                                                Size = Utilities.FileSize(listItemURL),
                                                DateUploaded = Utilities.FileLastModified(listItemURL)
                                            };

                                            foundFiles.Add(foundFile);
                                        }
                                    }
                                }
                                else // Checks if this item is a sub directory/folder
                                {
                                    if (crawlSubDirectories)
                                    {
                                        try
                                        {
                                            if (listItem.ToLower() != "")
                                            {
                                                if (listItem.ToLower() != previousItem)
                                                {
                                                    string[] ignoredItems = { "parent directory", "...", "..", "description", "../", ".../", "/", "desc", "seriaегенд", "seria?????" };
                                                    if (!ignoredItems.Any(listItem.ToLower().Contains))
                                                        if (Utilities.URLExists(listItemURL))
                                                            if (!fileTypes.Any(x => listItem.ToUpper().EndsWith(x)))
                                                                GetWebFiles(listItemURL.TrimEnd('/').Replace(" ", "%20").Replace("#", "%23") + " / ", crawlSubDirectories, requestTimeout, fileTypes);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                            break;
                                        }
                                    }
                                }

                            }

                        }

                    }
                }
            }

            return foundFiles;
        }

        static void WriteTopSearches()
        {
            var items = new List<string>();
            items.AddRange(GetTopSearches(100));
            items.AddRange(GetTopSearches(200));

            // Load users configuration json file
            var usersConfig = UsersConfig();

            File.WriteAllText(usersConfig.PathToWrite + @"\top-searches.txt", ""); // Clear all current items

            using (StreamWriter sw = File.AppendText(usersConfig.PathToWrite + @"\top-searches.txt"))
            {
                foreach (var item in items)
                {
                    sw.WriteLine(item);
                }
            }

            OutputMessage("Top Searches Completed: Results (" + items.Count() + ")");
            RootMenu = true;
        }

        static List<string> GetTopSearches(int number = 100) // 100 = Page 1, 200 = Page 2
        {
            List<string> listTopSearches = new List<string>();
            using (var client = new WebClient())
            using (var stream = client.OpenRead("https://api.hackertarget.com/pagelinks/?q=" + linkFileChefSearches + "/" + number))
            using (var reader = new StreamReader(stream))
            {
                stream.ReadTimeout = 60000;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // line = http://www.filechef.com/direct-download/[ITEM]/[TYPE] item = value, type = video/other/etc (we only return item)
                    
                    var formattedItem = line.Replace("http://www.filechef.com/direct-download/", "");
                    formattedItem = formattedItem.Substring(0, formattedItem.LastIndexOf('/')).Replace("-", " ");
                    if (formattedItem != "" && formattedItem != linkFileChefSearches)
                    {
                        listTopSearches.Add(formattedItem);
                    }
                }
            }
            listTopSearches.Reverse();
            return listTopSearches;
        }

        public static void OutputTitle()
        {
            Console.Clear();
            Output.WriteLine(ConsoleColor.Blue, Header);
        }

        public static void OutputMessage(string message)
        {
            Output.WriteLine(ConsoleColor.Cyan, message);
        }

        public static void OutputResult(int webServersCrawled, int filesAdded, long filesSize, TimeSpan timeTaken, int filesFound)
        {
            OutputTitle();
            Output.WriteLine(ConsoleColor.Red, string.Format(@"------------- Results --------------

Web Servers Crawled: {0}
Files Added: {1}
Files Size: {2}
Time Taken (mins): {3}
--------------------
Total Files Found: {4}

---------------------------------------", webServersCrawled, filesAdded, StringExtensions.BytesToPrefix(filesSize), timeTaken.TotalMinutes, filesFound));

            OutputPause();
        }

        public static void OutputPause()
        {
            Output.WriteLine(ConsoleColor.Green, "Press any key to continue . . . ");
            Console.ReadKey(true);
        }

        public static void LogMessage(string message)
        {
            using (var log = File.AppendText(pathLogFile))
            {
                log.WriteLine(message);
            }
        }
    }
}