using EpicManifestParser.Objects;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace EpicGamesNotifications
{
    class Program
    {
        static string GetAccessToken()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token");
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            request.Method = "POST";

            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "UELauncher/13.1.2-18458102+++Portal+Release-Live Windows/10.0.19042.1.256.64bit";
            request.Headers.Add("X-Epic-Correlation-ID", "UE4-9d44f1444730e0ab67b97c96877fd423-4456F05F406FD9A4D2DC75A6EC8D70CF-5B619D4544431FD6636BF69374BDCA3D");
            request.Headers.Add("Authorization", "basic MzRhMDJjZjhmNDQxNGUyOWIxNTkyMTg3NmRhMzZmOWE6ZGFhZmJjY2M3Mzc3NDUwMzlkZmZlNTNkOTRmYzc2Y2Y=");

            var body = Encoding.ASCII.GetBytes($"grant_type=client_credentials&token_type=eg1");
            using (var stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            HttpWebResponse response;
            try { response = (HttpWebResponse)request.GetResponse(); } catch (WebException ee) { response = (HttpWebResponse)ee.Response; }

            return ((dynamic)Newtonsoft.Json.JsonConvert.DeserializeObject(new StreamReader(response.GetResponseStream()).ReadToEnd())).access_token;
        }
        static string GetManifestUrl()
        {
             HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/public/assets/v2/platform/Windows/launcher?label=Live");
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "UELauncher/13.1.2-18458102+++Portal+Release-Live Windows/10.0.19042.1.256.64bit";
            request.Headers.Add("X-Epic-Correlation-ID", "UE4-9d44f1444730e0ab67b97c96877fd423-4456F05F406FD9A4D2DC75A6EC8D70CF-5B619D4544431FD6636BF69374BDCA3D");
            request.Headers.Add("Authorization", $"bearer {GetAccessToken()}");

            HttpWebResponse response;
            try { response = (HttpWebResponse)request.GetResponse(); } catch (WebException ee) { response = (HttpWebResponse)ee.Response; }
            
            dynamic manifest = ((dynamic)(Newtonsoft.Json.JsonConvert.DeserializeObject(new StreamReader(response.GetResponseStream()).ReadToEnd()))).elements[1].manifests[0];
            string url = "";

            url += manifest.uri + "?";
            foreach (dynamic queryParam in manifest.queryParams)
            {
                url += queryParam.name + "=" + queryParam.value + "&";
            }

            return url.Substring(0, url.Length - 1);
        }
        static byte[] DownloadManifest()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GetManifestUrl());
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            HttpWebResponse response;
            try { response = (HttpWebResponse)request.GetResponse(); } catch (WebException ee) { response = (HttpWebResponse)ee.Response; }

            MemoryStream ms = new MemoryStream();
            response.GetResponseStream().CopyTo(ms);
            return ms.ToArray();
        }
        static void Main(string[] args)
        {
            Manifest manifest = new Manifest(DownloadManifest(), new ManifestOptions
            {
                ChunkBaseUri = new Uri("http://epicgames-download1.akamaized.net/Builds/UnrealEngineLauncher/CloudDir/ChunksV4/", UriKind.Absolute),
                ChunkCacheDirectory = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Chunks"))
            });

            var NotificationsFile = manifest.FileManifests.Find(x => x.Name == "BuildNotificationsV2.json");
            byte[] NotificationsArray = new byte[(int)NotificationsFile.GetStream().Length];
            NotificationsFile.GetStream().Read(NotificationsArray, 0, (int)NotificationsFile.GetStream().Length).ToString();

            dynamic Notifications = (dynamic)Newtonsoft.Json.JsonConvert.DeserializeObject(Encoding.Default.GetString(NotificationsArray));
            foreach (dynamic Notification in Notifications.BuildNotifications)
            {
                // TODO: DisplayCondition
                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(Notification));
            }
        }
    }
}
