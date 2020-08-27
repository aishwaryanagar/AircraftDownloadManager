using System;
using System.IO;
using System.Web;
using System.Web.Configuration;

namespace AircraftDownloadManager.Logic
{
    public class DownloadManager
    {
        private string _DocumentUrl { get; set; }

        private string _DirectoryPath { get; set; }
        private string _FileName { get; set; }

        private bool IsStarted { get { return false; } set { _ = IsStarted; } }
        private bool IsDownloading { get { return false; } set { _ = IsDownloading; } }

        private bool IsDownloadSuccessful { get { return false; } set { _ = IsDownloadSuccessful; } }
        public DownloadManager(string documentUrl, string directory, string FileName)
        {
            _DocumentUrl = documentUrl;
            _DirectoryPath = directory;
            _FileName = FileName;
        }

        public decimal DownloadedPercent(HttpContext httpContext, string filePath)
        {
            if (httpContext.Request.Headers["Range"] != null)
            {
                FileStream fileStr = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long fileLength = fileStr.Length;
                string[] range = httpContext.Request.Headers["Range"].Split(new char[] { '=', '-' });
               long bytesDownloaded = Convert.ToInt64(range[1]);
                return (bytesDownloaded / fileLength) / 100;
            }
                return 0;
        }
        public bool DownloadFile(HttpContext httpContext, string filePath)
        {
            bool result = true;
            filePath = filePath.Replace("/",
                  " ").Replace("%20", " ");
            #region Check if file exists
            if (!File.Exists(filePath))
            {
                httpContext.Response.StatusCode = 404;
                return false;
            }
            #endregion
            long startBytes = 0;
            int packSize = 1024 * 5;//read in block，every block 5K bytes
            FileStream fileStr = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BinaryReader br = new BinaryReader(fileStr);
            long fileLength = fileStr.Length;
            string lastUpdateTime = File.GetLastWriteTimeUtc(filePath).ToString();

            try
            {
                httpContext.Response.Clear();
                httpContext.Response.AddHeader("Accept-Ranges", "bytes");
                httpContext.Response.AddHeader("Content-Disposition", "attachment");
                httpContext.Response.AppendHeader("Last-Modified", lastUpdateTime);
                httpContext.Response.AddHeader("Content-Length", (fileLength - startBytes).ToString());
                if (httpContext.Request.Headers["Range"] != null)
                {
                    httpContext.Response.StatusCode = 206;
                    string[] range = httpContext.Request.Headers["Range"].Split(new char[] { '=', '-' });
                    //format of range would be <unit>=<range-start>-; so the 2nd element of array would return the byte from where we need to resume download
                    startBytes = Convert.ToInt64(range[1]);
                    if (startBytes < 0 || startBytes >= fileLength)
                    {
                        return false;
                    }
                }
                if (startBytes > 0)
                {
                    httpContext.Response.AddHeader("Content-Range", string.Format(" bytes {0}-{1}/{2}", startBytes, fileLength - 1, fileLength));
                }

                IsStarted = true;
                //send data
                br.BaseStream.Seek(startBytes, SeekOrigin.Begin);
                int maxCount = (int)Math.Ceiling(fileLength - startBytes + 0.0);//length of remaining bytes

                IsDownloading = true;
                //TODO:  raise the download starting event.
                for (int i = 0; i < maxCount && httpContext.Response.IsClientConnected; i++)
                {
                    httpContext.Response.BinaryWrite(br.ReadBytes(packSize));
                    httpContext.Response.Flush();
                }
                //TODO:  raise the download ended successfully event.
                IsDownloadSuccessful = true;
            }
            catch
            {
                return false;
            }
            finally
            {
                //TODO:  raise the error event.
                br.Close();
                fileStr.Close();
            }

            return result;
        }
    }
}
