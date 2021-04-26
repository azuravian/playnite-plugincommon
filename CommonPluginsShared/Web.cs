﻿using Newtonsoft.Json;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CommonPluginsShared
{
    // TODO https://stackoverflow.com/questions/62802238/very-slow-httpclient-sendasync-call

    public enum WebUserAgentType
    {
        Request
    }

    public class Web
    {
        private static ILogger logger = LogManager.GetLogger();


        private static string StrWebUserAgentType(WebUserAgentType UserAgentType)
        {
            switch (UserAgentType)
            {
                case (WebUserAgentType.Request):
                    return "request";
            }
            return string.Empty;
        }


        /// <summary>
        /// Download file image and resize in icon format (64x64).
        /// </summary>
        /// <param name="ImageFileName"></param>
        /// <param name="url"></param>
        /// <param name="ImagesCachePath"></param>
        /// <param name="PluginName"></param>
        /// <returns></returns>
        public static async Task<bool> DownloadFileImage(string ImageFileName, string url, string ImagesCachePath, string PluginName)
        {
            string PathImageFileName = Path.Combine(ImagesCachePath, PluginName.ToLower(), ImageFileName);

            if (!url.ToLower().Contains("http"))
            {
                return false;
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");

                Stream imageStream;
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                    imageStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on download {url}");
                    return false;
                }

                if (imageStream != null)
                {
                    ImageTools.Resize(imageStream, 64, 64, PathImageFileName);
                }
            }

            // Delete file is empty
            try
            {
                if (File.Exists(PathImageFileName + ".png"))
                {
                    FileInfo fi = new FileInfo(PathImageFileName + ".png");
                    if (fi.Length == 0)
                    {
                        File.Delete(PathImageFileName + ".png");
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on delete file image");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Download file stream.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<Stream> DownloadFileStream(string url)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");
                    HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                    return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on download {url}");
                    return null;
                }
            }
        }

        public static async Task<Stream> DownloadFileStream(string url, List<HttpCookie> Cookies)
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (Cookies != null)
            {
                CookieContainer cookieContainer = new CookieContainer();

                foreach (var cookie in Cookies)
                {
                    Cookie c = new Cookie();
                    c.Name = cookie.Name;
                    c.Value = cookie.Value;
                    c.Domain = cookie.Domain;
                    c.Path = cookie.Path;

                    try
                    {
                        cookieContainer.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                        logger.Warn($"Cookies ignored for {url}");
                    }
                }

                handler.CookieContainer = cookieContainer;
            }

            using (var client = new HttpClient(handler))
            {
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");
                    HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                    return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on download {url}");
                    return null;
                }
            }
        }


        /// <summary>
        /// Download string data with manage redirect url.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<string> DownloadStringData(string url)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");

                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get
                };

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on download {url}");
                    return string.Empty;
                }

                if (response == null)
                {
                    return string.Empty;
                }

                int statusCode = (int)response.StatusCode;

                // We want to handle redirects ourselves so that we can determine the final redirect Location (via header)
                if (statusCode >= 300 && statusCode <= 399)
                {
                    var redirectUri = response.Headers.Location;
                    if (!redirectUri.IsAbsoluteUri)
                    {
                        redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);
                    }

                    Common.LogDebug(true, string.Format("DownloadStringData() redirecting to {0}", redirectUri));

                    return await DownloadStringData(redirectUri.ToString());
                }
                else
                {
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Download string data with a specific UserAgent.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="UserAgentType"></param>
        /// <returns></returns>
        public static async Task<string> DownloadStringData(string url, WebUserAgentType UserAgentType)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(StrWebUserAgentType(UserAgentType));

                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get
                };

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on download {url}");
                    return string.Empty;
                }

                if (response == null)
                {
                    return string.Empty;
                }

                int statusCode = (int)response.StatusCode;
                if (statusCode == 200)
                {
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                else
                {
                    logger.Warn($"DownloadStringData() with statuscode {statusCode} for {url}");
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Download compressed string data.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<string> DownloadStringDataWithGz(string url)
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            using (HttpClient client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");
                return await client.GetStringAsync(url).ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Post data with a payload.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        public static async Task<string> PostStringDataPayload(string url, string payload)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = (SettingsSection)config.GetSection("system.net/settings");
            var defaultValue = settings.HttpWebRequest.UseUnsafeHeaderParsing;
            settings.HttpWebRequest.UseUnsafeHeaderParsing = true;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("system.net/settings");

            var response = string.Empty;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");
                client.DefaultRequestHeaders.Add("accept", "application/json, text/javascript, */*; q=0.01");
                client.DefaultRequestHeaders.Add("Vary", "Accept-Encoding");
                HttpContent c = new StringContent(payload, Encoding.UTF8, "application/json");

                HttpResponseMessage result;
                try
                {
                    result = await client.PostAsync(url, c).ConfigureAwait(false);
                    if (result.IsSuccessStatusCode)
                    {
                        response = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        logger.Error($"Web error with status code {result.StatusCode.ToString()}");
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on Post {url}");
                }
            }

            //settings.HttpWebRequest.UseUnsafeHeaderParsing = defaultValue;
            //config.Save(ConfigurationSaveMode.Modified);
            //ConfigurationManager.RefreshSection("system.net/settings");

            return response;
        }

        //var formContent = new FormUrlEncodedContent(new[]
        //{
        //    new KeyValuePair<string, string>("comment", comment),
        //    new KeyValuePair<string, string>("questionId", questionId)
        //});
        public static async Task<string> PostStringDataCookies(string url, FormUrlEncodedContent formContent, List<HttpCookie> Cookies = null)
        {
            var response = string.Empty;

            HttpClientHandler handler = new HttpClientHandler();
            if (Cookies != null)
            {
                CookieContainer cookieContainer = new CookieContainer();

                foreach (var cookie in Cookies)
                {
                    Cookie c = new Cookie();
                    c.Name = cookie.Name;
                    c.Value = cookie.Value;
                    c.Domain = cookie.Domain;
                    c.Path = cookie.Path;

                    try
                    {
                        cookieContainer.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                        logger.Warn($"Cookies ignored for {url}");
                    }
                }

                handler.CookieContainer = cookieContainer;
            }

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");

                HttpResponseMessage result;
                try
                {
                    result = await client.PostAsync(url, formContent).ConfigureAwait(false);
                    if (result.IsSuccessStatusCode)
                    {
                        response = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        logger.Error($"Web error with status code {result.StatusCode.ToString()}");
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on Post {url}");
                }
            }

            return response;
        }


        public static async Task<string> DownloadStringData(string url, List<HttpCookie> Cookies = null)
        {
            var response = string.Empty;

            HttpClientHandler handler = new HttpClientHandler();
            if (Cookies != null)
            {
                CookieContainer cookieContainer = new CookieContainer();

                foreach (var cookie in Cookies)
                {
                    Cookie c = new Cookie();
                    c.Name = cookie.Name;
                    c.Value = cookie.Value;
                    c.Domain = cookie.Domain;
                    c.Path = cookie.Path;

                    try
                    {
                        cookieContainer.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                        logger.Warn($"Cookies ignored for {url}");
                    }
                }

                handler.CookieContainer = cookieContainer;
            }

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");

                HttpResponseMessage result;
                try
                {
                    result = await client.GetAsync(url).ConfigureAwait(false);
                    if (result.IsSuccessStatusCode)
                    {
                        response = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        logger.Error($"Web error with status code {result.StatusCode.ToString()}");
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on Post {url}");
                }
            }

            return response;
        }
    }
}
