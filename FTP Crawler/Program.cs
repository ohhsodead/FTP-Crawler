using System;
using System.Net;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Newtonsoft.Json;
using FTP_Crawler.Utilities;
using FTP_Crawler.Resources;
using FTP_Crawler.Models;
using EasyConsole;

namespace FTP_Crawler
{
    class Program
    {
        /// <summary>
        /// Current applications path
        /// </summary>
        public static string FilenameCrawler = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// Default Configuration File Path
        /// </summary>
        public static string FilenameConfig = $@"{FilenameCrawler}\Config.json";

        /// <summary>
        /// Writes and stores the server crawl history to this path
        /// </summary>
        public static string FilenameHistory = $@"{FilenameCrawler}\History.json";
        
        /// <summary>
        /// Writes all log messages to this file path
        /// </summary>
        public static string FilenameLog = $@"{FilenameCrawler}\OutputLog.txt";


        /// <summary>
        /// URL to get top searches from
        /// </summary>
        public static string UrlFileChefSearches = "http://www.filechef.com/searches/index";

        /// <summary>
        /// Hide/show root menu/options
        /// </summary>
        private static bool RootMenu = true;

        /// <summary>
        /// Simple ASCII title art
        /// </summary>
        public static string Header = $@"
                                                                                      
                    ███████╗████████╗██████╗      ██████╗██████╗  █████╗ ██╗    ██╗██╗     ███████╗██████╗ 
                    ██╔════╝╚══██╔══╝██╔══██╗    ██╔════╝██╔══██╗██╔══██╗██║    ██║██║     ██╔════╝██╔══██╗
                    █████╗     ██║   ██████╔╝    ██║     ██████╔╝███████║██║ █╗ ██║██║     █████╗  ██████╔╝
                    ██╔══╝     ██║   ██╔═══╝     ██║     ██╔══██╗██╔══██║██║███╗██║██║     ██╔══╝  ██╔══██╗
                    ██║        ██║   ██║         ╚██████╗██║  ██║██║  ██║╚███╔███╔╝███████╗███████╗██║  ██║
                    ╚═╝        ╚═╝   ╚═╝          ╚═════╝╚═╝  ╚═╝╚═╝  ╚═╝ ╚══╝╚══╝ ╚══════╝╚══════╝╚═╝  ╚═╝
                                                                                      
                                                                                      
-----------------------------------------------------------------------------------------------------------------------------
                                                                                      ";

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += ExceptionEvents.CurrentDomainUnhandledException;

            Console.Title = "FTP Crawler";

            while (RootMenu)
            {
                OutputTitle();
                OutputInstructions();

                var menu = new Menu()
                .Add("Run FTP Crawler \nWrites all information about files stored on FTP servers to the specified file path. Setup configuration before running.", () => StartFTPCrawler())
                .Add(@"Write Searches \nSimply writes the most searches parsed from FileChef.com pages and writes them to '\searches.txt'", () => WriteTopSearches())
                .Add("Exit \nClose console application.", () => Environment.Exit(0));
                menu.Display();
            }
        }

        /// <summary>
        /// Gets (or creates a new) users configuration file stored in crawler's directory path
        /// </summary>
        /// <returns></returns>
        private static ConfigFile UsersConfig()
        {
            if (!File.Exists(FilenameConfig))
            {
                OutputMessage($"Configuration file doesn't exist. Locate [{FilenameCrawler}] to configure your crawler options.");
                File.WriteAllText(FilenameConfig, CrawlerConfig.DefaultCrawlConfig);
                OutputPause();
                return null;
            }
            else if (File.ReadAllText(FilenameConfig) == CrawlerConfig.DefaultCrawlConfig)
            {
                OutputMessage($"You haven't setup your configuration file. Locate [{FilenameCrawler}] to configure your crawler options.");
                OutputPause();
                return null;
            }

            return JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(FilenameConfig).Replace(@"\", "/"));
        }

        /// <summary>
        /// Gets/sets the server log, its main purpose is to not crawl servers within the last week of last time
        /// </summary>
        private static ServerHistory ServerHistory { get; set; } = new ServerHistory();

        /// <summary>
        /// Stores the list of broken servers, used to rewrite the server list (add config option)
        /// </summary>
        private static List<string> BrokenServers { get; set; } = new List<string>();
        
        /// <summary>
        /// Stores the list of existing file URLs from the last crawl
        /// </summary>
        private static List<string> ExistingFileURLs { get; set; } = new List<string>();
        
        /// <summary>
        /// Write files from FTP servers
        /// </summary>
        public static void StartFTPCrawler()
        {
            RootMenu = false;
            OutputTitle();

            ConfigFile usersConfig;
            if (UsersConfig() == null) { RootMenu = true; return; } else usersConfig = UsersConfig();

            var timeCrawler = new Stopwatch(); // Measure elapsed time for the crawler
            var ftpServers = new List<string>(); // Store list of servers we're going to crawl

            // Loads the list of ftp servers from either a local file or web file
            OutputMessage($"Reading servers from [{usersConfig.ServersFilename}]...");

            if (FileExtensions.IsLocalFile(usersConfig.ServersFilename))
                if (File.Exists(usersConfig.ServersFilename))
                    ftpServers.AddRange(File.ReadAllLines(usersConfig.ServersFilename));
            else if (FileExtensions.IsWebFile(usersConfig.ServersFilename))
                if (!FileExtensions.URLExists(usersConfig.ServersFilename))
                    ftpServers.AddRange(FileExtensions.LoadWebTextFileItems(usersConfig.ServersFilename, FilenameCrawler));
            else
            {
                OutputMessage($"Servers list cannot be identified. Make sure this file exists either on your machine or a web server and that 'ServersFilename' is set to the correct location.", ConsoleColor.Red);
                OutputPause();
                RootMenu = true;
                return;
            }

            OutputMessage($"Servers loaded.", ConsoleColor.Magenta);

            var outputFileName = $@"{usersConfig.OutputFilename}.json".Replace("/", @"\"); // Directory to output results lists to (If we're writing to one file)

            // Deletes old output if we're overwriting, otherwise we load the existing files to not ignore duplicates
            if (usersConfig.Overwrite)
            {
                OutputMessage("Deleting old results file...");

                if (!File.Exists(outputFileName))
                    File.Delete(outputFileName);
            }
            else
            {
                OutputMessage($"Getting existing files...", ConsoleColor.Magenta);

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
                            ExistingFileURLs.Add(JsonConvert.DeserializeObject<FtpFile>(s).URL);
                        }
                        catch { }
                    }
                }
            }

            timeCrawler.Start(); // Start the timer 

            // Reads servers log contents and sets to current ServerLog
            if (File.Exists(FilenameHistory))
            {
                using (FileStream fs = File.Open(FilenameHistory, FileMode.Open))
                using (BufferedStream bs = new BufferedStream(fs))
                using (StreamReader sr = new StreamReader(bs))
                {
                    try
                    {
                        ServerHistory = JsonConvert.DeserializeObject<ServerHistory>(sr.ReadToEnd());
                    }
                    catch { }
                }
            }

            // Loops FTP servers while crawling files and writing them to file(s)
            foreach (var ftpServer in ftpServers)
            {
                try
                {
                    var uriServer = new Uri(ftpServer);

                    string serverHost;
                    int serverPort;
                    string serverUsername;
                    string serverPassword;

                    // Attempts to get host, port, username and password from ftp server url - username:password@host:port / username:password host:port
                    string serverUserInfo = uriServer.UserInfo;
                    serverUsername = serverUserInfo.Split(':')[0];
                    serverPassword = serverUserInfo.Split(':')[1];

                    serverHost = uriServer.Host;
                    serverPort = uriServer.Port;

                    OutputTitle();
                    OutputMessage("Crawling " + ftpServer);

                    if ((GetServerLog(serverHost).LastCrawled - DateTime.Now).TotalDays > 7) // Only crawl server if it was last crawled over a week ago
                    {
                        WriteFTPFiles($"ftp://{serverHost}:{serverPort}/", serverUsername, serverPassword, outputFileName, usersConfig.RequestTimeout);
                        UpdateServerLogDate(serverHost, DateTime.Now);
                    }
                }
                catch (Exception ex) { BrokenServers.Add(ftpServer); LogFtpMessage(ex.Message); }
            }

            timeCrawler.Stop(); // Stop crawler here

            // Rewrites server list but without broken servers (servers that weren't crawled)
            File.Delete(usersConfig.ServersFilename);
            using (var sw = File.AppendText(usersConfig.ServersFilename))
                foreach (var ftpServer in ftpServers)
                    if (!BrokenServers.Contains(ftpServer))
                        sw.WriteLine(ftpServer);

            UpdateServerLogFile(); // Updates server log

            OutputResults(ftpServers.Count(), new TimeSpan(timeCrawler.ElapsedTicks)); // Output results to console

            timeCrawler.Reset(); // Resets the crawler for next time

            RootMenu = true;
        }

        public static void WriteFTPFiles(string serverAddress, string serverUsername, string serverPassword, string outputFileName, int requestTimeout)
        {
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(serverAddress);
                request.Timeout = requestTimeout;
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(serverUsername, serverPassword);

                using (var response = (FtpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    foreach (var item in reader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    {
                        var itemUrl = $"{serverAddress}{item}";
                        var itemUri = new Uri(itemUrl);

                        if (!ExistingFileURLs.Contains(itemUri.AbsoluteUri.Replace("#", "%23"))) // Checks if this file isn't a duplicate
                        {
                            if (IsFile(itemUrl)) // Check if item is file by requesting file size, otherwise returning false if an error occurs
                            {
                                // Create a new FTP File object to write to output
                                var newFile = new FtpFile
                                {
                                    Name = Path.GetFileName(new Uri(itemUrl).LocalPath),
                                    Size = FileExtensions.FtpFileSize(itemUrl, serverUsername, serverPassword),
                                    Modified = FileExtensions.FtpFileTimestamp(itemUrl, serverUsername, serverPassword),
                                    URL = itemUri.AbsoluteUri.Replace("#", "%23")
                                };

                                using (StreamWriter sw = File.AppendText(outputFileName))
                                {
                                    sw.WriteLine(JsonConvert.SerializeObject(newFile));
                                    ExistingFileURLs.Add(newFile.URL);
                                    LogFtpMessage("File Added : " + newFile.Name + " [" + newFile.URL + "]");
                                    ResultsTotalSize = ResultsTotalSize + newFile.Size;
                                    ResultsTotalSize++;
                                }
                            }
                            else
                            {
                                if (!item.StartsWith("#") || !item.EndsWith("#"))
                                    WriteFTPFiles($"{itemUrl}/", serverUsername, serverPassword, outputFileName, requestTimeout);
                            }
                        }
                    }
                }
            }
            catch (StackOverflowException ex)
            {
                LogFtpMessage($"Overflow exception occurred (Sometimes happens with large items) - {ex.Message}"); // Can't seem to overcome this issue, perhaps invoke StreamReader?
            }
            catch (Exception ex)
            {
                LogFtpMessage($"Unable to get directory listing [{serverAddress}] - {ex.Message}");
            }
        }

        static bool IsFile(string ftpPath)
        {
            var request = (FtpWebRequest)WebRequest.Create(ftpPath);
            request.Timeout = 700000;
            request.Method = WebRequestMethods.Ftp.GetFileSize;
            request.Credentials = new NetworkCredential("anonymous", "password");
            try
            {
                using (var response = (FtpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteTopSearches()
        {
            var items = new List<string>();
            items.AddRange(GetSearches(200));
            items.AddRange(GetSearches(300));

            // Load users configuration json file
            var usersConfig = UsersConfig();

            File.WriteAllText(usersConfig.OutputFilename + @"\searches.txt", ""); // Clear all current items

            using (StreamWriter sw = File.AppendText(usersConfig.OutputFilename + @"\searches.txt"))
                foreach (var item in items)
                    sw.WriteLine(item);

            OutputMessage("Searches Completed: Results (" + items.Count() + ")");
            RootMenu = true;
        }

        private static List<string> GetSearches(int number = 100) // page 1 is a different url, but others work - 200 = Page 2, 300 = Page 3
        {
            List<string> listSearches = new List<string>();
            using (var client = new WebClient())
            using (var stream = client.OpenRead("https://api.hackertarget.com/pagelinks/?q=" + UrlFileChefSearches + "/" + number))
            using (var reader = new StreamReader(stream))
            {
                stream.ReadTimeout = 60000;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // line example = http://www.filechef.com/direct-download/[ITEM]/[TYPE] item = name, type = video/other/etc (we only use name)

                    var formattedItem = line.Replace("http://www.filechef.com/direct-download/", "");
                    formattedItem = formattedItem.Substring(0, formattedItem.LastIndexOf('/')).Replace("-", " ");

                    if (formattedItem != "" && formattedItem != UrlFileChefSearches)
                        listSearches.Add(formattedItem);
                }
            }
            listSearches.Reverse();
            return listSearches;
        }

        private static void OutputTitle()
        {
            Console.Clear();
            Output.WriteLine(ConsoleColor.Green, Header);
        }

        private static void OutputInstructions()
        {
            Output.WriteLine(ConsoleColor.Gray, "Simple FTP File Crawler. Notes:" +
                "\n- This is a work in progress and is only a basic concept." +
                "\n- This was created for my personal use, although it's made public for others to try out." +
                "\n- It's not recommended for productional use as there's still a lot of improvements to be made." +
                "\n- Your configuration file (CrawlerConfig.json) must be located in this application's directory before running." + 
                "\n");
        }

        private static void OutputMessage(string message, ConsoleColor color = ConsoleColor.Cyan)
        {
            Output.WriteLine(color, message);
        }
        
        private static int ResultsFilesFound { get; set; } = 0;
        private static int ResultsFilesAdded { get; set; } = 0;
        private static long ResultsTotalSize { get; set; } = 0;

        private static void OutputResults(int ftpServersCrawled, TimeSpan timeTaken)
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

---------------------------------------", ftpServersCrawled, BrokenServers.Count, ResultsFilesAdded, Utilities.StringExtensions.BytesToPrefix(ResultsTotalSize), timeTaken.TotalMinutes, ResultsFilesFound));

            OutputPause();
        }

        private static void OutputPause()
        {
            Output.WriteLine(ConsoleColor.Yellow, "Press any key to continue . . . ");
            Console.ReadKey(true);
        }

        /// <summary>
        /// Logs a message about the current ftp process/state/message
        /// </summary>
        /// <param name="message"></param>
        public static void LogFtpMessage(string message)
        {
            using (StreamWriter FTPLog = File.AppendText(FilenameLog))
                FTPLog.WriteLine(message);
        }

        /// <summary>
        /// Gets the server log content for the specified server
        /// </summary>
        /// <param name="server">Server object by name to return</param>
        /// <returns>Server Log</returns>
        private static ServerHistory.Server GetServerLog(string server)
        {
            // Tries to find the server in the current log and returns the matching server object
            foreach (var log in ServerHistory.Servers)
                if (log.Name == server)
                    return log;

            // Creates a new server log, adds it to the list of current servers log and then returns it
            var newServerLog = new ServerHistory.Server() { Name = server, LastCrawled = DateTime.Now };
            ServerHistory.Servers.Add(newServerLog); return newServerLog;
        }

        /// <summary>
        /// Sets the log date of a server to the specified time
        /// </summary>
        /// <param name="server">Server object by name to update server date</param>
        /// <param name="time">Date to replace the date to</param>
        private static void UpdateServerLogDate(string server, DateTime time)
        {
            foreach (var log in ServerHistory.Servers)
                if (log.Name == server)
                    log.LastCrawled = time; return;
        }

        /// <summary>
        /// Updates the server log file with the latest crawl results
        /// </summary>
        private static void UpdateServerLogFile()
        {
            File.WriteAllText(FilenameHistory, JsonConvert.SerializeObject(ServerHistory));
        }
    }
}