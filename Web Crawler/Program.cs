using EasyConsole;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Web_Crawler.Models;
using Web_Crawler.Utilities;
using Web_Crawler.Resources;
using static Web_Crawler.Models.ServerLog;

namespace Web_Crawler
{
    class Program
    {
        /// <summary>
        /// This applications path
        /// </summary>
        public static string FilenameCrawler = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// Log File Path
        /// </summary>
        public static string FilenameLog = $@"{FilenameCrawler}\Log.txt";

        /// <summary>
        /// Default Configuration File Path
        /// </summary>
        public static string FilenameConfig = $@"{FilenameCrawler}\config.json";

        /// <summary>
        /// URL to get top searches from
        /// </summary>
        public static string UrlFileChefSearches = "http://www.filechef.com/searches/index";

        /// <summary>
        /// 
        /// </summary>
        public static string FilenameServerLog = $@"{FilenameCrawler}\ServerLog.json";

        /// <summary>
        /// Sets root menu to display
        /// </summary>
        private static bool RootMenu = true;

        /// <summary>
        /// Simple ASCII title art
        /// </summary>
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

            while (RootMenu)
            {
                OutputTitle();
                OutputInstructions();

                var menu = new Menu()
                .Add("Run FTP Crawler \n-  For now, the anonymous login method is used as I need to add support for formatting ftps with their credentials to be parsed.", () => StartFTPCrawler())
                .Add("Write Top Searches \n-  Simply writes the top searches parsed from FileChef.com pages and writes them to top-searches.txt", () => WriteTopSearches())
                .Add("Exit", () => Environment.Exit(0));
                menu.Display();
            }
        }

        /// <summary>
        /// Gets the users configuration file stored in their crawler's directory path
        /// </summary>
        /// <returns></returns>
        private static ConfigFile UsersConfig()
        {

            if (!File.Exists(FilenameConfig)) // Creates a default config file if it doesn't exist
            {
                OutputMessage($"Configuration file doesn't exist. Go to [{FilenameCrawler}] to configure your crawler.");
                File.WriteAllText(FilenameConfig, CrawlConfig.DefaultCrawlConfig);
                OutputPause();
            }

            return JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(FilenameConfig).Replace(@"\", "/"));
        }

        /// <summary>
        /// Gets/sets the server log, its main purpose is to not crawl servers within the last week of last time
        /// </summary>
        private static ServerLog ServerLog { get; set; } = new ServerLog();

        /// <summary>
        /// Stores the list of broken servers, used to rewrite the server list (add config option)
        /// </summary>
        private static List<string> BrokenServers { get; set; } = new List<string>();
        
        /// <summary>
        /// Stores the list of existing file URLs in the server list
        /// </summary>
        private static List<string> ExistingFileURLs { get; set; } = new List<string>();

        /// <summary>
        /// Measures the elapsed time the crawler took
        /// </summary>

        /// <summary>
        /// Write files from FTP servers
        /// </summary>
        public static void StartFTPCrawler()
        {
            RootMenu = false;
            OutputTitle();            
            var usersConfig = UsersConfig();
            var timeCrawler = new Stopwatch();
            var ftpServers = new List<string>();

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

            OutputMessage($"Servers loaded successfully.", ConsoleColor.Green);

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
                            ExistingFileURLs.Add(JsonConvert.DeserializeObject<FtpFile>(s).URL);
                        }
                        catch { }
                    }
                }
            }

            timeCrawler.Start(); // Start the timer 

            // Reads servers log contents and sets to ServerLog
            if (File.Exists(FilenameServerLog))
            {
                using (FileStream fs = File.Open(FilenameServerLog, FileMode.Open))
                using (BufferedStream bs = new BufferedStream(fs))
                using (StreamReader sr = new StreamReader(bs))
                {
                    try
                    {
                        ServerLog = JsonConvert.DeserializeObject<ServerLog>(sr.ReadToEnd());
                    }
                    catch { }
                }
            }
            else
            {
                File.WriteAllText(FilenameServerLog, ""); // Creates a blank server log file to output log to
            }

            // Loops FTP servers while crawling files and writing them to file(s)
            foreach (var ftpServer in ftpServers)
            {
                try
                {
                    string serverHost;
                    int serverPort;
                    string serverUsername;
                    string serverPassword;

                    // Attempts to get host, port, username and password from ftp server url - username:password@host:port / username:password host:port
                    string serverUserInfo = new Uri(ftpServer).UserInfo;
                    serverUsername = serverUserInfo.Split(':')[0];
                    serverPassword = serverUserInfo.Split(':')[1];

                    serverHost = new Uri(ftpServer).Host;
                    serverPort = new Uri(ftpServer).Port;

                    OutputTitle();
                    OutputMessage("Crawling " + ftpServer);

                    if ((GetServerLog(serverHost).LastCrawled - DateTime.Now).TotalDays > 7) // If server was last crawled more than a week ago, scan again...
                    {
                        WriteFTPFiles($"ftp://{serverHost}:{serverPort}/", serverUsername, serverPassword, usersConfig, outputFileName);
                        UpdateServerLogDate(serverHost, DateTime.Now);
                    }
                }
                catch (Exception ex) { BrokenServers.Add(ftpServer); LogFtpMessage(ex.Message); }
            }

            timeCrawler.Stop(); // Stop crawler here

            // Rewrites server list but without broken servers (servers that weren't crawled)
            File.Delete(usersConfig.ServersFilename);
            using (StreamWriter sw = File.AppendText(usersConfig.ServersFilename))
                foreach (var ftpServer in ftpServers)
                    if (!BrokenServers.Contains(ftpServer))
                        sw.WriteLine(ftpServer);

            UpdateServerLogFile(); // Updates server log

            OutputResults(ftpServers.Count(), new TimeSpan(timeCrawler.ElapsedTicks)); // Output results to console

            timeCrawler.Reset(); // Resets the crawler for next time

            RootMenu = true;
        }

        public static void WriteFTPFiles(string serverAddress, string serverUsername, string serverPassword, ConfigFile usersConfig, string outputFileName)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(serverAddress);
                request.Timeout = usersConfig.RequestTimeout;
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(serverUsername, serverPassword);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    var directoryListing = reader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    foreach (var item in directoryListing)
                    {
                        string itemURL = $"{serverAddress}{item}";
                        Uri itemUri = new Uri(itemURL);

                        if (!ExistingFileURLs.Contains(itemUri.AbsoluteUri.Replace("#", "%23"))) // Checks if this file isn't a duplicate
                        {
                            if (IsFile(itemURL)) // Checks if this is file by requesting file size and returning false if an error occurs
                            {
                                FtpFile newFile = new FtpFile
                                {
                                    Name = Path.GetFileName(new Uri(itemURL).LocalPath),
                                    Size = FileExtensions.FtpFileSize(itemURL, serverUsername, serverPassword),
                                    Modified = FileExtensions.FtpFileTimestamp(itemURL, serverUsername, serverPassword),
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
                                if (!item.StartsWith("#"))
                                    if (!item.EndsWith("#"))
                                        WriteFTPFiles($"{itemURL}/", serverUsername, serverPassword, usersConfig, outputFileName);
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

        private static List<string> GetTopSearches(int number = 100) // page 1 is a different url, but others work - 200 = Page 2, 300 = Page 3
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
                    // line example = http://www.filechef.com/direct-download/[ITEM]/[TYPE] item = name, type = video/other/etc (we only use name)

                    var formattedItem = line.Replace("http://www.filechef.com/direct-download/", "");
                    formattedItem = formattedItem.Substring(0, formattedItem.LastIndexOf('/')).Replace("-", " ");

                    if (formattedItem != "" && formattedItem != UrlFileChefSearches)
                        listTopSearches.Add(formattedItem);
                }
            }
            listTopSearches.Reverse();
            return listTopSearches;
        }

        private static void OutputTitle()
        {
            Console.Clear();
            Output.WriteLine(ConsoleColor.Blue, Header);
        }

        private static void OutputInstructions()
        {
            Output.WriteLine(ConsoleColor.Cyan, "Notes: " +
                "\n- Crawler is only a work in progress and there's ongoing active development." +
                "\n- " +
                "\n- Not recommended for productional purposes." +
                "\n- Configuration file (config.json) must be located in your crawler's startup directory before running.");
        }

        private static void OutputMessage(string message, ConsoleColor color = ConsoleColor.Cyan)
        {
            Output.WriteLine(color, message);
        }
        
        private static int ResultsFilesFound { get; set; } = 0;
        private static int ResultsFilesAdded { get; set; } = 0;
        private static long ResultsTotalSize { get; set; } = 0;

        private static void OutputResults(int webServersCrawled, TimeSpan timeTaken)
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

---------------------------------------", webServersCrawled, BrokenServers.Count, ResultsFilesAdded, Utilities.StringExtensions.BytesToPrefix(ResultsTotalSize), timeTaken.TotalMinutes, ResultsFilesFound));

            OutputPause();
        }

        private static void OutputPause()
        {
            Output.WriteLine(ConsoleColor.Green, "Press any key to continue . . . ");
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
        /// Gets the log content for a server
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        private static Server GetServerLog(string server)
        {
            // Tries to find the server in the current log and returns the matching server name
            foreach (var log in ServerLog.Servers)
                if (log.Name == server)
                    return log;

            // Creates a new server log, adds it to the list of current servers log and then returns it
            var newServerLog = new Server() { Name = server, LastCrawled = DateTime.Now };
            ServerLog.Servers.Add(newServerLog); return newServerLog;
        }

        /// <summary>
        /// Sets the log date of a server to the specified time
        /// </summary>
        /// <param name="server"></param>
        /// <param name="time"></param>
        private static void UpdateServerLogDate(string server, DateTime time)
        {
            foreach (var log in ServerLog.Servers)
                if (log.Name == server)
                    log.LastCrawled = time;
        }

        /// <summary>
        /// Updates the server log file with the latest crawler results
        /// </summary>
        private static void UpdateServerLogFile()
        {
            File.WriteAllText(FilenameServerLog, JsonConvert.SerializeObject(ServerLog));
        }
    }
}