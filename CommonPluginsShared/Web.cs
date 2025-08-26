﻿using CommonPlayniteShared;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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


    public class HttpHeader
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }


    public class Web
    {
        private static ILogger Logger => LogManager.GetLogger();

        public static string UserAgent => $"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:126.0) Gecko/20100101 Firefox/126.0 Playnite/{API.Instance.ApplicationInfo.ApplicationVersion.ToString(2)}";


        private static string StrWebUserAgentType(WebUserAgentType userAgentType)
        {
            switch (userAgentType)
            {
                case WebUserAgentType.Request:
                    return "request";
                default:
                    break;
            }
            return string.Empty;
        }


        /// <summary>
        /// Download file image and resize in icon format (64x64).
        /// </summary>
        /// <param name="imageFileName"></param>
        /// <param name="url"></param>
        /// <param name="imagesCachePath"></param>
        /// <param name="pluginName"></param>
        /// <returns></returns>
        public static Task<bool> DownloadFileImage(string imageFileName, string url, string imagesCachePath, string pluginName)
        {
            string PathImageFileName = Path.Combine(imagesCachePath, pluginName.ToLower(), imageFileName);

            if (!StringExtensions.IsHttpUrl(url))
            {
                return Task.FromResult(false);
            }

            using (var client = new HttpClient())
            {
                try
                {
                    var cachedFile = HttpFileCache.GetWebFile(url);
                    if (string.IsNullOrEmpty(cachedFile))
                    {
                        //logger.Warn("Web file not found: " + url);
                        return Task.FromResult(false);
                    }

                    ImageTools.Resize(cachedFile, 64, 64, PathImageFileName);
                }
                catch (Exception ex)
                {
                    if (!url.Contains("steamcdn-a.akamaihd.net", StringComparison.InvariantCultureIgnoreCase) && !ex.Message.Contains("(403)"))
                    {
                        Common.LogError(ex, false, $"Error on download {url}");
                    }
                    return Task.FromResult(false);
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
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public static async Task<bool> DownloadFileImageTest(string url)
        {
            if (!url.ToLower().Contains("http"))
            {
                return false;
            }

            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                    HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on download {url}");
                    return false;
                }
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
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
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

        public static async Task<Stream> DownloadFileStream(string url, List<HttpCookie> cookies)
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (cookies != null)
            {
                CookieContainer cookieContainer = new CookieContainer();

                foreach (var cookie in cookies)
                {
                    Cookie c = new Cookie();
                    c.Name = cookie.Name;
                    c.Value = Tools.FixCookieValue(cookie.Value);
                    c.Domain = cookie.Domain;
                    c.Path = cookie.Path;

                    try
                    {
                        cookieContainer.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                    }
                }

                handler.CookieContainer = cookieContainer;
            }

            using (var client = new HttpClient(handler))
            {
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
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
        /// Download string data and keep url parameter when there is a redirection.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<string> DownloadStringDataKeepParam(string url)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get
                };

                HttpResponseMessage response;
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                    response = await client.SendAsync(request).ConfigureAwait(false);

                    var uri = response.RequestMessage.RequestUri.ToString();
                    if (uri != url)
                    {
                        var urlParams = url.Split('?').ToList();
                        if (urlParams.Count == 2)
                        {
                            uri += "?" + urlParams[1];
                        }
                        
                        return await DownloadStringDataKeepParam(uri);
                    }
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

                    return await DownloadStringDataKeepParam(redirectUri.ToString());
                }
                else
                {
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                return await client.GetStringAsync(url).ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Download string data with manage redirect url.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<string> DownloadStringData(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpRequestMessage request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get
                };

                HttpResponseMessage response;
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
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
        /// <param name="userAgentType"></param>
        /// <returns></returns>
        public static async Task<string> DownloadStringData(string url, WebUserAgentType userAgentType)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get
                };

                HttpResponseMessage response;
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd(StrWebUserAgentType(userAgentType));
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
                    Logger.Warn($"DownloadStringData() with statuscode {statusCode} for {url}");
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Download string data with custom cookies.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="cookies"></param>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        public static async Task<string> DownloadStringData(string url, List<HttpCookie> cookies = null, string userAgent = "", bool keepParam = false)
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (cookies != null)
            {
                CookieContainer cookieContainer = new CookieContainer();

                foreach (var cookie in cookies)
                {
                    Cookie c = new Cookie();
                    c.Name = cookie.Name;
                    c.Value = Tools.FixCookieValue(cookie.Value);
                    c.Domain = cookie.Domain;
                    c.Path = cookie.Path;

                    try
                    {
                        cookieContainer.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                    }
                }

                handler.CookieContainer = cookieContainer;
            }

            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get
            };

            HttpResponseMessage response;
            using (var client = new HttpClient(handler))
            {
                if (userAgent.IsNullOrEmpty())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                }
                else
                {
                    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                }

                try
                {
                    response = await client.SendAsync(request).ConfigureAwait(false);
                    int statusCode = (int)response.StatusCode;
                    bool IsRedirected = (request.RequestUri.ToString() != url) || (statusCode >= 300 && statusCode <= 399);

                    // We want to handle redirects ourselves so that we can determine the final redirect Location (via header)
                    if (IsRedirected)
                    {
                        string urlNew = request.RequestUri.ToString();
                        var redirectUri = response.Headers.Location;
                        if (!redirectUri?.IsAbsoluteUri ?? false)
                        {
                            redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);
                            urlNew = redirectUri.ToString();
                        }
                        
                        if (keepParam)
                        {
                            var urlParams = url.Split('?').ToList();
                            if (urlParams.Count == 2)
                            {
                                var urlNewParams = urlNew.Split('?').ToList();
                                if (urlNewParams.Count == 2)
                                {
                                    if (urlParams[1] != urlNewParams[1])
                                    {
                                        urlNew += "&" + urlParams[1];
                                    }
                                }
                                else
                                {
                                    urlNew += "?" + urlParams[1];
                                }
                            }
                        }

                        Common.LogDebug(true, string.Format("DownloadStringData() redirecting to {0}", urlNew));
                        return await DownloadStringData(urlNew, cookies, userAgent);
                    }
                    else
                    {
                        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Section=ResponseHeader Detail=CR"))
                    {
                        Logger.Warn($"Used UserAgent: Anything");
                        return DownloadStringData(url, cookies, "Anything").GetAwaiter().GetResult();
                    }
                    else
                    {
                        Common.LogError(ex, false, $"Error on Get {url}");
                    }
                }
            }

            return string.Empty;
        }
        
        public static async Task<string> DownloadStringData(string url, CookieContainer cookies = null, string userAgent = "")
        {
            var response = string.Empty;

            HttpClientHandler handler = new HttpClientHandler();
            if (cookies?.Count > 0)
            {
                handler.CookieContainer = cookies;
            }

            using (var client = new HttpClient(handler))
            {
                if (userAgent.IsNullOrEmpty())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                }
                else
                {
                    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                }

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
                        Logger.Error($"Web error with status code {result.StatusCode.ToString()}");
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on Get {url}");
                }
            }

            return response;
        }

        public static async Task<string> DownloadStringData(string url, List<HttpHeader> httpHeaders = null, List<HttpCookie> cookies = null)
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (cookies != null)
            {
                CookieContainer cookieContainer = new CookieContainer();

                foreach (var cookie in cookies)
                {
                    Cookie c = new Cookie
                    {
                        Name = cookie.Name,
                        Value = Tools.FixCookieValue(cookie.Value),
                        Domain = cookie.Domain,
                        Path = cookie.Path
                    };

                    try
                    {
                        cookieContainer.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                    }
                }

                handler.CookieContainer = cookieContainer;
            }

            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get
            };

            string response = string.Empty;
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);

                if (httpHeaders != null)
                {
                    httpHeaders.ForEach(x => 
                    {
                        client.DefaultRequestHeaders.Add(x.Key, x.Value);
                    });
                }

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
                        Logger.Error($"Web error with status code {result.StatusCode.ToString()}");
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on Get {url}");
                }
            }

            return response;
        }

        /// <summary>
        /// Downloads string data from a URL using an optional token and language header.
        /// Optionally performs a pre-request to another URL before the main call.
        /// </summary>
        /// <param name="url">The URL to fetch the data from.</param>
        /// <param name="token">The Bearer token for Authorization header.</param>
        /// <param name="urlBefore">An optional URL to call before the main request (e.g., for session setup).</param>
        /// <param name="langHeader">Optional Accept-Language header value (e.g., "en-US").</param>
        /// <returns>The response content as a string.</returns>
        public static async Task<string> DownloadStringData(string url, string token, string urlBefore = "", string langHeader = "")
        {
            using (var client = new HttpClient())
            {
                // Set the user agent for the request
                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);

                // Add Accept-Language header if provided
                if (!langHeader.IsNullOrWhiteSpace())
                {
                    client.DefaultRequestHeaders.Add("Accept-Language", langHeader);
                }

                // Make an optional preliminary request if specified
                if (!urlBefore.IsNullOrWhiteSpace())
                {
                    try
                    {
                        await client.GetStringAsync(urlBefore).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Pre-request to {urlBefore} failed.");
                    }
                }

                // Add the Authorization header
                if (!token.IsNullOrWhiteSpace())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                }

                // Perform the main request
                string result = await client.GetStringAsync(url).ConfigureAwait(false);
                return result;
            }
        }



        public static async Task<string> DownloadPageText(string url, List<HttpCookie> cookies = null, string userAgent = "")
        {
            WebViewSettings webViewSettings = new WebViewSettings
            {
                JavaScriptEnabled = true,
                UserAgent = userAgent.IsNullOrEmpty() ? Web.UserAgent : userAgent
            };

            using (IWebView webView = API.Instance.WebViews.CreateOffscreenView(webViewSettings))
            {
                cookies?.ForEach(x =>
                {
                    string domain = x.Domain.StartsWith(".") ? x.Domain.Substring(1) : x.Domain;
                    webView.SetCookies("https://" + domain, x);
                });

                webView.NavigateAndWait(url);
                return await webView.GetPageTextAsync();
            }
        }


        public static async Task<string> DownloadStringDataWithUrlBefore(string url, string urlBefore = "", string langHeader = "")
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);

                if (!langHeader.IsNullOrEmpty())
                {
                    client.DefaultRequestHeaders.Add("Accept-Language", langHeader);
                }

                if (!urlBefore.IsNullOrEmpty())
                {
                    await client.GetStringAsync(urlBefore).ConfigureAwait(false);
                }
                
                string result = await client.GetStringAsync(url).ConfigureAwait(false);
                return result;
            }
        }


        public static async Task<string> DownloadStringDataJson(string url)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "*/*");

                string result = await client.GetStringAsync(url).ConfigureAwait(false);
                return result;
            }
        }


        public static async Task<string> PostStringData(string url, string token, StringContent content)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                var response = await client.PostAsync(url, content);
                var str = await response.Content.ReadAsStringAsync();
                return str;
            }
        }

        public static async Task<string> PostStringData(string url, StringContent content)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                var response = await client.PostAsync(url, content);
                var str = await response.Content.ReadAsStringAsync();
                return str;
            }
        }

        /// <summary>
        /// Post data with a payload.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="payload"></param>
        /// <returns></returns>

        public static async Task<string> PostStringDataPayload(
                    string url,
                    string payload,
                    List<HttpCookie> Cookies = null,
                    List<KeyValuePair<string, string>> moreHeader = null)
        {
            string response = string.Empty;

            // List of cookies to include
            var allowedCookies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "hltb_alive",
                "hltb_view_list",
                "hltb_online",
                "OTGPPConsent",
                "OptanonConsent",
                "usprivacy"
            };

            // Create cookie container
            var cookieContainer = new CookieContainer();

            if (Cookies != null)
            {
                foreach (var cookie in Cookies)
                {
                    if (cookie.Domain.Contains("howlongtobeat") && !allowedCookies.Contains(cookie.Name))
                        continue; // skip cookies not in the allowed list

                    try
                    {
                        cookieContainer.Add(new Cookie(
                            cookie.Name,
                            Tools.FixCookieValue(cookie.Value),
                            string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path,
                            cookie.Domain
                        ));
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                    }
                }
            }

            // Handler with automatic decompression
            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            using (var client = new HttpClient(handler))
            {
                // Minimal working headers
                var uri = new Uri(url);
                client.DefaultRequestHeaders.Host = uri.Host;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(Web.UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");

                // Any additional headers
                if (moreHeader != null)
                {
                    foreach (var kv in moreHeader)
                    {
                        client.DefaultRequestHeaders.Add(kv.Key, kv.Value);
                    }
                }

                // JSON content
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                try
                {
                    var result = await client.PostAsync(url, content).ConfigureAwait(false);
                    if (result.IsSuccessStatusCode)
                    {
                        response = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        Logger.Error($"Web error with status code {result.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on Post {url}");
                }
            }

            return response;
        }


        public static async Task<string> PostStringDataCookies(string url, FormUrlEncodedContent formContent, List<HttpCookie> cookies = null)
        {
            var response = string.Empty;

            HttpClientHandler handler = new HttpClientHandler();
            if (cookies != null)
            {
                CookieContainer cookieContainer = new CookieContainer();

                foreach (HttpCookie cookie in cookies)
                {
                    Cookie c = new Cookie
                    {
                        Name = cookie.Name,
                        Value = Tools.FixCookieValue(cookie.Value),
                        Domain = cookie.Domain,
                        Path = cookie.Path
                    };

                    try
                    {
                        cookieContainer.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                    }
                }

                handler.CookieContainer = cookieContainer;
            }

            using (var client = new HttpClient(handler))
            {
                var els = url.Split('/');
                string baseUrl = els[0] + "//" + els[2];

                client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                client.DefaultRequestHeaders.Add("origin", baseUrl);
                client.DefaultRequestHeaders.Add("referer", baseUrl);

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
                        Logger.Error($"Web error with status code {result.StatusCode.ToString()}");
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
