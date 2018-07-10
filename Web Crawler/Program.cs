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
using Web_Crawler.Models;
using Web_Crawler.Utilities;
using Web_Crawler.Resources;

namespace Web_Crawler
{
    class Program
    {
        /// <summary>
        /// This applications path
        /// </summary>
        public static string PathCrawler = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// Log File Path
        /// </summary>
        public static string FilePathLog = $@"{PathCrawler}\Log.txt";

        /// <summary>
        /// Default Configuration File Path
        /// </summary>
        public static string FilePathConfig = $@"{PathCrawler}\config.json";

        /// <summary>
        /// URL to get top searches from
        /// </summary>
        public static string UrlFileChefSearches = "http://www.filechef.com/searches/index";

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
            AppDomain.CurrentDomain.UnhandledException += ExceptionEvents.CurrentDomainUnhandledException;

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
                OutputInstructions();

                var menu = new Menu()
                .Add("Run HTML Crawler \n- This is not a recommended method for crawling servers. It uses the simple use of regex to parse html returned from web pages and tries to build and validate file urls. I suggest enabling access for anonymous logins to your server and crawling it then.", () => StartCrawler())
                .Add("Run FTP Crawler \n- For now, the anonymous login method is used as I need to add support for formatting ftps with their credentials to be parsed.", () => StartFTPCrawler())
                .Add("Write Top Searches \n- Simply writes the top searches parsed from FileChef.com pages and writes them to top-searches.txt", () => WriteTopSearches())
                .Add("Exit", () => Environment.Exit(0));
                menu.Display();
            }
        }

        /// <summary>
        /// Write files from FTP servers
        /// </summary>
        public static void StartFTPCrawler()
        {
            RootMenu = false;
            OutputTitle();            
            var usersConfig = UsersConfig();
            var ftpServers = new List<string>();

            /* Loads the list of web servers from either the local file or a web file containing the list of servers */
            OutputMessage($"Reading servers from [{usersConfig.ServersFilename}]...");

            if (FileExtensions.IsLocalFile(usersConfig.ServersFilename)) // Checks if this file is local
                if (File.Exists(usersConfig.ServersFilename))
                    ftpServers.AddRange(File.ReadAllLines(usersConfig.ServersFilename));
            else if (FileExtensions.IsWebFile(usersConfig.ServersFilename)) // Checks if it's a web file
                if (!FileExtensions.URLExists(usersConfig.ServersFilename))
                    ftpServers.AddRange(FileExtensions.LoadWebTextFileItems(usersConfig.ServersFilename, PathCrawler));
            else
            {
                OutputMessage($"Servers list cannot be identified. Make sure this file exists either on your machine or a web server and that 'ServersFilename' is set to the correct location.", ConsoleColor.Red);
                OutputPause();
                RootMenu = true;
                return;
            }

            OutputMessage($"Servers loaded successfully.", ConsoleColor.Green);

            var outputFileName = $@"{usersConfig.OutputFilename}.json".Replace("/", @"\"); // Directory to output results lists to (If we're writing to one file)

            if (usersConfig.Overwrite) // Deletes old output if we're overwriting, otherwise we load the existing files to not add duplicates
            {
                if (!File.Exists(outputFileName))
                    File.Delete(outputFileName);

                OutputMessage("Deleted old results file...");
            }
            else
            {
                OutputMessage($"Getting existing files...", ConsoleColor.Green);

                if (!File.Exists(outputFileName))
                    File.WriteAllText(outputFileName, "");

                using (FileStream fs = File.Open(outputFileName, FileMode.Open))
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
            }

            // File types to include in results, exluding those not specified
            var filesTypes = FileTypes.All;

            if (usersConfig.FileTypes == "*")
                filesTypes = FileTypes.All;
            else
                filesTypes = new List<string>(usersConfig.FileTypes.Split('|'));
            
            timeCrawl.Start(); // Start the timer 

            // Loops FTP servers while crawling files and writing them to file(s)
            foreach (var ftpServer in ftpServers)
            {
                try
                {
                    OutputTitle();
                    OutputMessage("Crawling " + ftpServer);
                    WriteFTPFiles(ftpServer, usersConfig, outputFileName, filesTypes);
                }
                catch (Exception ex) { BrokenServers.Add(ftpServer); LogMessage(ex.Message); }
            }

            timeCrawl.Stop();

            OutputResult(ftpServers.Count(), new TimeSpan(timeCrawl.ElapsedTicks));

            // Removes broken servers from the server list
            File.Delete(usersConfig.ServersFilename);
            using (StreamWriter sw = File.AppendText(usersConfig.ServersFilename))
                foreach (var ftpServer in ftpServers)
                    if (!BrokenServers.Contains(ftpServer))
                        sw.WriteLine(ftpServer);

            RootMenu = true;
        }

        static List<string> BrokenServers = new List<string>();
        static Stopwatch timeCrawl = new Stopwatch();

        static string[] invalidItems = new string[] { ".", "..", "...", "cAos" }; // Ignored items, causes an infinite loop as they're usually default items (parent directory) or items that cause it to break

        public static void WriteFTPFiles(string ftpServer, ConfigFile usersConfig, string outputFileName, List<string> fileTypes)
        {
            try
            {

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpServer);
                request.Timeout = usersConfig.RequestTimeout;
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential("anonymous", "password"); // ADD SUPPORT FOR MANUAL USERNAME AND PASSWORD VIA FTP LIST (USE A FORMAT)

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    var directoryListing = reader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    foreach (var item in directoryListing)
                    {
                        string itemURL = $"{ftpServer}{item}";
                        Uri itemUri = new Uri(itemURL);

                        if (fileTypes.Any(x => item.ToUpper().EndsWith("." + x))) // Assume this is a file, as it ends with ".(SUPPORTED-EXTENSION)" e.g. ".MP4" OR ".MP3"
                        {
                            if (!existingFileURLs.Contains(itemUri.AbsoluteUri.Replace("#", "%23")))
                            {
                                WebFile newFile = new WebFile
                                {
                                    Name = Path.GetFileNameWithoutExtension(new Uri(itemURL).LocalPath),
                                    URL = itemUri.AbsoluteUri.Replace("#", "%23"),
                                    Host = itemUri.Host,
                                    Type = Path.GetExtension(itemURL).Replace(".", "").ToUpper(),
                                    Size = FileExtensions.FtpFileSize(itemURL),
                                    DateUploaded = FileExtensions.FtpFileTimestamp(itemURL)
                                };

                                using (StreamWriter sw = File.AppendText(outputFileName))
                                {
                                    sw.WriteLine(JsonConvert.SerializeObject(newFile));
                                    existingFileURLs.Add(newFile.URL);
                                    LogMessage("File Added : " + newFile.Name + " [" + newFile.URL + "]");
                                    filesSize = filesSize + newFile.Size;
                                    filesAdded++;
                                }
                            }
                        }
                        else
                        {
                            if (!invalidItems.Contains(item))
                                if (!item.StartsWith("#"))
                                    if (!item.EndsWith("#"))
                                        WriteFTPFiles($"{itemURL}/", usersConfig, outputFileName, fileTypes);
                        }
                    }
                }
            }
            catch (StackOverflowException ex)
            {
                LogMessage($"Overflow exception occurred (Sometimes happens with large items) - {ex.Message}"); // Can't seem to overcome this issue, perhaps invoke StreamReader?
            }
            catch (Exception ex)
            {
                LogMessage($"Unable to get directory listing [{ftpServer}] - {ex.Message}");
            }
        }

        /* Write Files from Web Servers using HTML parsing */

        static List<string> existingFileURLs = new List<string>();

        public static void StartCrawler()
        {
            RootMenu = false;

            OutputTitle();

            if (!File.Exists(FilePathLog)) File.WriteAllText(FilePathLog, ""); // Creates blank log file

            // Load users configuration json file
            OutputMessage($"Loading your configuration file...");
            ConfigFile usersConfig = UsersConfig();

            // Checks if config OutputFilename exists on machine
            if (!Directory.Exists(usersConfig.OutputFilename))
                Directory.CreateDirectory(usersConfig.OutputFilename);

            List<string> webServers = new List<string>(); // Servers to loop

            /* Loads the list of web servers from either the local file or a web file containing the items */
            OutputMessage($"Loading web servers from [{usersConfig.ServersFilename}]...");

            if (FileExtensions.IsLocalFile(usersConfig.ServersFilename)) // Checks if file is local
            {
                if (!File.Exists(usersConfig.ServersFilename))
                {
                    OutputMessage($"Web servers list doesn't exist on your machine. Create a text file with your list of servers and replace [{usersConfig.ServersFilename}] with this file path.");
                    OutputPause();
                    RootMenu = true;
                    return;
                }
                else
                    webServers.AddRange(File.ReadAllLines(usersConfig.ServersFilename));
            }
            else if (FileExtensions.IsWebFile(usersConfig.ServersFilename)) // Checks if is web file
            {
                if (!FileExtensions.URLExists(usersConfig.ServersFilename))
                {
                    OutputMessage($"Web servers list doesn't exist on your machine. Create a text file with your list of servers and replace [{usersConfig.ServersFilename}] with this file path.");
                    OutputPause();
                    RootMenu = true;
                    return;
                }
                else
                    webServers.AddRange(FileExtensions.LoadWebTextFileItems(usersConfig.ServersFilename, PathCrawler));
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
            var outputFileName = $@"{usersConfig.OutputFilename}.json".Replace("/", @"\");

            // Deletes old list if we're re-writing, otherwise we load the existing files to not add duplicates
            if (usersConfig.Overwrite)
            {
                if (!File.Exists(outputFileName))
                    File.Delete(outputFileName);

                OutputMessage("Deleted old results file...");
            }
            else
            {
                using (FileStream fs = File.Open(outputFileName, FileMode.Open))
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
            
            // Sets the file types to be used
            var filesTypes = FileTypes.All;

            if (usersConfig.FileTypes == "*")
                filesTypes = FileTypes.All;
            else
                filesTypes = new List<string>(usersConfig.FileTypes.Split('|'));

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            /* Gets web files from web servers and writes links to them in specified path */
            foreach (var webServer in webServers)
            {
                try
                {
                    OutputTitle();
                    OutputMessage("Crawling " + webServer);

                    WriteWebFiles(webServer, usersConfig, outputFileName, filesTypes);
                }
                catch (Exception ex) { LogMessage(ex.Message); }
            }

            stopWatch.Stop();

            OutputMessage("Web Crawl Completed. So, what now?");
            OutputResult(webServers.Count(), new TimeSpan(stopWatch.ElapsedTicks));

            RootMenu = true;
        }

        // Results
        static int filesFound = 0;
        static int filesAdded = 0;
        static long filesSize = 0;

        public static ConfigFile UsersConfig()
        {

           if (!File.Exists(FilePathConfig)) // Creates a config file for the user if it doesn't exist
            {
                OutputMessage($"Configuration file doesn't exist. Go to [{PathCrawler}] to configure your crawler.");
                File.WriteAllText(FilePathConfig, CrawlConfig.DefaultCrawlConfig);
                OutputPause();
            }

            var config = JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(FilePathConfig).Replace(@"\", "/"));
            return config;
        }

        public static void WriteWebFiles(string webServer, ConfigFile configFile, string outputFileName, List<string> fileTypes)
        {
            var usersConfig = configFile;

            var oldItemSplit = webServer.Split('/');
            Array.Resize(ref oldItemSplit, oldItemSplit.Length - 2);
            var previousItem = String.Join("/", oldItemSplit);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(webServer);
            request.Timeout = usersConfig.RequestTimeout;
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
                                    if (FileExtensions.URLExists(listItemURL)) // Checks if this file actually exists on the server
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
                                                Size = FileExtensions.WebFileSize(listItemURL),
                                                DateUploaded = FileExtensions.WebFileTimestamp(listItemURL)
                                            };

                                            if (!existingFileURLs.Contains(foundFile.URL))
                                                using (StreamWriter sw = File.AppendText(outputFileName))
                                                {
                                                    existingFileURLs.Add(foundFile.URL);
                                                    sw.WriteLine(JsonConvert.SerializeObject(foundFile));
                                                    filesSize = filesSize + foundFile.Size;
                                                    OutputMessage("Found File : " + foundFile.Name + " [" + foundFile.URL + "]");
                                                    filesAdded++;
                                                }

                                            filesFound++;
                                        }
                                    }
                                }
                                else // Checks if this item is a sub directory/folder
                                {
                                    try
                                    {
                                        if (listItem.ToLower() != "")
                                        {
                                            if (listItem.ToLower() != previousItem)
                                            {
                                                string[] ignoredItems = { "parent directory", "...", "..", "description", "../", ".../", "/", "desc", "seriaегенд", "seria?????" };
                                                if (!ignoredItems.Any(listItem.ToLower().Contains))
                                                    if (FileExtensions.URLExists(listItemURL))
                                                        if (!fileTypes.Any(x => listItem.ToUpper().EndsWith(x)))
                                                            WriteWebFiles(listItemURL.TrimEnd('/').Replace(" ", "%20").Replace("#", "%23") + " / ", usersConfig, outputFileName, fileTypes);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message);
                                    }
                                }

                            }

                        }

                    }
                }
            }
        }

        static void WriteTopSearches()
        {
            var items = new List<string>();
            items.AddRange(GetTopSearches(200));
            items.AddRange(GetTopSearches(300));

            // Load users configuration json file
            var usersConfig = UsersConfig();

            File.WriteAllText(usersConfig.OutputFilename + @"\searches.txt", ""); // Clear all current items

            using (StreamWriter sw = File.AppendText(usersConfig.OutputFilename + @"\searches.txt"))
                foreach (var item in items)
                    sw.WriteLine(item);

            OutputMessage("Top Searches Completed: Results (" + items.Count() + ")");
            RootMenu = true;
        }

        static List<string> GetTopSearches(int number = 100) // page 1 is a different url, but others work - 200 = Page 2, 300 = Page 3
        {
            List<string> listTopSearches = new List<string>();
            using (var client = new WebClient())
            using (var stream = client.OpenRead("https://api.hackertarget.com/pagelinks/?q=" + UrlFileChefSearches + "/" + number))
            using (var reader = new StreamReader(stream))
            {
                stream.ReadTimeout = 60000;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // line = http://www.filechef.com/direct-download/[ITEM]/[TYPE] item = name, type = video/other/etc (we only use name)

                    var formattedItem = line.Replace("http://www.filechef.com/direct-download/", "");
                    formattedItem = formattedItem.Substring(0, formattedItem.LastIndexOf('/')).Replace("-", " ");

                    if (formattedItem != "" && formattedItem != UrlFileChefSearches)
                        listTopSearches.Add(formattedItem);
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

        public static void OutputInstructions()
        {
            Output.WriteLine(ConsoleColor.Cyan, "Notes: " +
                "\n- Crawler is only a work in progress and there's ongoing active development." +
                "\n- " +
                "\n- Not recommended for productional purposes." +
                "\n- Configuration file (config.json) must be located in your crawler's startup directory before running.");
        }

        public static void OutputMessage(string message, ConsoleColor color = ConsoleColor.Cyan)
        {
            Output.WriteLine(color, message);
        }

        public static void OutputResult(int webServersCrawled, TimeSpan timeTaken)
        {
            OutputTitle();
            Output.WriteLine(ConsoleColor.Red, string.Format(@"------------- Results --------------

Servers Crawled: {0}
Servers Broken: {1}
Files Added: {2}
Files Size: {3}
Time Taken (mins): {4}
--------------------
Total Files Found: {5}

---------------------------------------", webServersCrawled, BrokenServers.Count, filesAdded, Utilities.StringExtensions.BytesToPrefix(filesSize), timeTaken.TotalMinutes, filesFound));

            OutputPause();
        }

        public static void OutputPause()
        {
            Output.WriteLine(ConsoleColor.Green, "Press any key to continue . . . ");
            Console.ReadKey(true);
        }

        static StreamWriter log = File.AppendText(FilePathLog);

        public static void LogMessage(string message)
        {
            log.WriteLine(message);
        }
    }
}