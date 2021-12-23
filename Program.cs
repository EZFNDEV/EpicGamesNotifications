using EpicManifestParser.Objects;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace EpicGamesNotifications
{
    class Program
    {
        static HttpClient client = new HttpClient();
        static async Task<string> GetAccessToken()
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "MzRhMDJjZjhmNDQxNGUyOWIxNTkyMTg3NmRhMzZmOWE6ZGFhZmJjY2M3Mzc3NDUwMzlkZmZlNTNkOTRmYzc2Y2Y=");
            StringContent data = new StringContent("grant_type=client_credentials&token_type=eg1", Encoding.UTF8, "application/x-www-form-urlencoded");
            var resp = await client.PostAsync("https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token", data);
            string result = resp.Content.ReadAsStringAsync().Result;
            return JsonDocument.Parse(result).RootElement.GetProperty("access_token").ToString();
        }
        static async Task<string> GetManifestUrl()
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken());
            await using var resp = await client.GetStreamAsync("https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/public/assets/v2/platform/Windows/launcher?label=Live");
            client.DefaultRequestHeaders.Remove("Authorization");

            JsonDocument jsonDocument = JsonDocument.Parse(resp);

            var manifest = jsonDocument.RootElement.GetProperty("elements")[1].GetProperty("manifests")[0];
            var uriBuilder = new UriBuilder(manifest.GetProperty("uri").GetString());
            if (manifest.TryGetProperty("queryParams", out var queryParamsArray))
            {
                foreach (var queryParam in queryParamsArray.EnumerateArray())
                {
                    var queryParamName = queryParam.GetProperty("name").GetString();
                    var queryParamValue = queryParam.GetProperty("value").GetString();
                    var query = $"{queryParamName}={queryParamValue}";

                    if (uriBuilder.Query.Length == 0)
                    {
                        uriBuilder.Query = query;
                    }
                    else
                    {
                        uriBuilder.Query += '&' + query;
                    }
                }
            }

            return uriBuilder.ToString();
        }
        static async Task<byte[]> GetManifest()
        {
            string manifestUrl = await GetManifestUrl();
            return await client.GetByteArrayAsync(manifestUrl);
        }
        static async Task Main()
        {
            client.DefaultRequestHeaders.Add("User-Agent", "UELauncher/13.1.2-18458102+++Portal+Release-Live Windows/10.0.19042.1.256.64bit");
            
            Manifest manifest = new Manifest(await GetManifest(), new ManifestOptions
            {
                ChunkBaseUri = new Uri("http://epicgames-download1.akamaized.net/Builds/UnrealEngineLauncher/CloudDir/ChunksV4/", UriKind.Absolute),
                ChunkCacheDirectory = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Chunks"))
            });

            var NotificationsFile = manifest.FileManifests.Find(x => x.Name == "BuildNotificationsV2.json");
            JsonDocument Notifications = JsonDocument.Parse(NotificationsFile.GetStream());

            foreach (var Notification in Notifications.RootElement.GetProperty("BuildNotifications").EnumerateArray())
            {
                Console.WriteLine(Notifications.RootElement.ToString());
            }
        }
    }
}
