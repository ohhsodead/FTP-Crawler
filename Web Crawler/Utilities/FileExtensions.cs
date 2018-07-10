using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Web_Crawler.Utilities
{
    class FileExtensions
    {
        /// <summary>
        /// Gets total size of ftp file
        /// </summary>
        /// <param name="fileURL">FTP File</param>
        /// <returns></returns>
        public static long FtpFileSize(string fileURL)
        {
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(fileURL);
                request.Timeout = 900000;
                request.Credentials = new NetworkCredential("anonymous", "password");
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                using (WebResponse response = request.GetResponse())
                    return response.ContentLength;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Gets file DateTimestamp of ftp file
        /// </summary>
        /// <param name="fileURL">FTP File</param>
        /// <returns></returns>
        public static DateTime FtpFileTimestamp(string fileURL)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileURL);
                request.Timeout = 900000;
                request.Credentials = new NetworkCredential("anonymous", "password");
                request.Method = WebRequestMethods.Ftp.GetDateTimestamp;
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                return response.LastModified;
            }
            catch { return DateTime.MinValue; }
        }

        /// <summary>
        /// Gets web file size in bytes
        /// </summary>
        /// <param name="fileURL"></param>
        /// <returns></returns>
        public static int WebFileSize(string FileURL)
        {
            try
            {
                var req = WebRequest.Create(FileURL);
                req.Method = "HEAD";
                req.Timeout = 300000;
                using (var fileResponse = (HttpWebResponse)req.GetResponse())
                    if (int.TryParse(fileResponse.Headers.Get("Content-Length"), out int ContentLength))
                        return ContentLength;
                    else
                        return 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Gets web file last modified date
        /// </summary>
        /// <param name="fileURL"></param>
        /// <returns></returns>
        public static DateTime WebFileTimestamp(string FileURL)
        {
            try
            {
                var req = WebRequest.Create(FileURL);
                req.Method = "HEAD";
                req.Timeout = 300000;
                using (var fileResponse = (HttpWebResponse)req.GetResponse())
                    if (fileResponse.LastModified != null)
                        return fileResponse.LastModified;
                    else
                        return DateTime.MinValue;
            }
            catch { return DateTime.MinValue; }
        }

        /// <summary>
        /// Checks if web file exists on server
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static bool URLExists(string url)
        {
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(url);
                req.Timeout = 300000;

                try
                {
                    using (var fileResponse = (FtpWebResponse)req.GetResponse())
                        return true;
                }
                catch
                {
                    return false;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Returns web text file contents in a list
        /// </summary>
        /// <param name="fileURL"></param>
        /// <param name="filePathToDownload"></param>
        /// <returns></returns>
        public static List<string> LoadWebTextFileItems(string fileURL, string filePathToDownload)
        {
            var textItems = new List<string>();
            var webClient = new WebClient();
            webClient.DownloadFile(fileURL, filePathToDownload + @"\web-servers.txt");
            textItems.AddRange(File.ReadAllLines(filePathToDownload + @"\web-servers.txt"));
            return textItems;
        }

        /// <summary>
        /// Checks if path is a local file and exists on machine
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool IsLocalFile(string filePath)
        {
            return File.Exists(filePath);
        }

        /// <summary>
        /// Checks if path is valid
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static bool IsWebFile(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}