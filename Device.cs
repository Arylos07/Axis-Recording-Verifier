using Device_Recording_List;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;


public class Device
{
    public string SiteName { get; set; }
    public string DeviceName { get; set; }
    public string DeviceModel { get; set; }
    public string Host { get; set; }
    public string Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }

    public string Status { get; set; }

    public string Firmware { get; set; }
    public string TargetFirmware { get; set; }
    public string VmdStatus { get; set; }
    public string ObjectAnalyticsStatus { get; set; }
    public long UptimeSeconds { get; set; }
    public string Uptime { get; set; }
    public bool RecordingStatus { get; set; }
    public VMSType VMS { get; set; }


    public Device(string sname, string dname, string host, string port, string user, string pass)
    {
        SiteName = sname;
        DeviceName = dname;
        Host = host;
        Port = port;
        Username = user;
        Password = pass;
    }

    public string About(bool includeCreds = false)
    {
        string about = $"{SiteName}: {DeviceName} ({Host}:{Port})";
        if (includeCreds) { about += $" login: {Username}, {Password}"; }
        return about;
    }

    public string AboutCSV(bool includeCreds = false)
    {
        if (includeCreds)
        {
            return $"{SiteName},{DeviceName},{Host},{Port},{Username},{Password},{Status}";
        }
        else
        {
            return $"{SiteName},{DeviceName},{Status},{RecordingStatus},{VMS.ToString()}";
        }
    }

    public string GetConnectionInfo()
    {
        return $"http://{Host}:{Port}/";
    }

    public async Task<bool> Poke(TimeSpan timeout = default)
    {
        if (timeout == default) { timeout = TimeSpan.FromSeconds(5); }

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, GetConnectionInfo());
        using CancellationTokenSource cts = new CancellationTokenSource(timeout);
        try
        {
            // Send the HEAD request
            HttpResponseMessage response = await Program.client.SendAsync(request, cts.Token);
            Status = "Online";
            // Check if the status code indicates success
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            // TaskCanceledException may indicate a timeout
            //Console.WriteLine("Request timed out.");
            Status = "Offline";
            return false;
        }
        catch (HttpRequestException)
        {
            // Handle exceptions if the URL is unreachable
            //Console.WriteLine("Request timed out.");
            Status = "Offline";
            return false;
        }
    }

    public async Task<string> GetDeviceProperty(string propertyName)
    {
        // Build the endpoint URL based on the device's IP and port.
        string uri = $"{GetConnectionInfo()}axis-cgi/basicdeviceinfo.cgi";
        // JSON payload to get all properties.
        string payload = @"{
    ""apiVersion"":""1.0"",
    ""method"":""getAllProperties""
}";

        var credCache = new CredentialCache();
        credCache.Add(new Uri(GetConnectionInfo()), "Digest", new NetworkCredential(Username, Password));
        var httpClient = new HttpClient(new HttpClientHandler { Credentials = credCache });

        try
        {
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync(new Uri(uri), content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: Received status code {response.StatusCode}");
                return $"Error: {response.StatusCode}";
            }

            // Read the response as a byte array.
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            // Detect the character set.
            var charset = response.Content.Headers.ContentType?.CharSet;
            if (!string.IsNullOrWhiteSpace(charset) && charset.Equals("utf8", StringComparison.OrdinalIgnoreCase))
            {
                charset = "utf-8";
            }
            Encoding encoding;
            try
            {
                encoding = !string.IsNullOrWhiteSpace(charset) ? Encoding.GetEncoding(charset) : Encoding.UTF8;
            }
            catch
            {
                encoding = Encoding.UTF8;
            }
            string jsonResponse = encoding.GetString(bytes);

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                Console.WriteLine("Empty response received.");
                return "Empty response";
            }

            // Parse the JSON response to extract the requested property.
            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("data", out JsonElement dataElement) &&
                    dataElement.TryGetProperty("propertyList", out JsonElement propertyList) &&
                    propertyList.TryGetProperty(propertyName, out JsonElement propertyElement))
                {
                    return propertyElement.GetString();
                }
                else
                {
                    return $"{propertyName} not found";
                }
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Request timed out.");
            return "Time out (cancelled)";
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("Request failed: " + ex.Message);
            return "Request failed";
        }
    }

    public async Task<(bool isReady, string uptimeFormatted)> CheckSystemReady()
    {
        string uri = $"{GetConnectionInfo()}axis-cgi/systemready.cgi";
        string payload = @"{
    ""apiVersion"": ""1.0"",
    ""method"": ""systemready"",
    ""params"": { ""timeout"": 1 }
}";

        var credCache = new CredentialCache();
        credCache.Add(new Uri(GetConnectionInfo()), "Digest", new NetworkCredential(Username, Password));
        var httpClient = new HttpClient(new HttpClientHandler { Credentials = credCache });
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync(new Uri(uri), content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Systemready error: HTTP {response.StatusCode}");
                return (false, "");
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            var charset = response.Content.Headers.ContentType?.CharSet;
            if (!string.IsNullOrWhiteSpace(charset) && charset.Equals("utf8", StringComparison.OrdinalIgnoreCase))
                charset = "utf-8";
            Encoding encoding;
            try { encoding = !string.IsNullOrWhiteSpace(charset) ? Encoding.GetEncoding(charset) : Encoding.UTF8; }
            catch { encoding = Encoding.UTF8; }
            string jsonResponse = encoding.GetString(bytes);

            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("data", out JsonElement dataElement))
                {
                    string systemReady = dataElement.GetProperty("systemready").GetString();
                    string uptimeStr = dataElement.GetProperty("uptime").GetString();
                    long uptimeSeconds = 0;
                    if (!long.TryParse(uptimeStr, out uptimeSeconds))
                        uptimeSeconds = 0;
                    UptimeSeconds = uptimeSeconds;
                    string formattedUptime = uptimeSeconds.ToHumanReadableTime();
                    Uptime = formattedUptime;

                    bool isReady = systemReady.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    return (isReady, formattedUptime);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("CheckSystemReady exception: " + ex.Message);
        }
        return (false, "");
    }

    public async Task<bool> CheckRecordingStatus()
    {
        string uri = $"{GetConnectionInfo()}axis-cgi/record/list.cgi?recordingid=all";

        var credCache = new CredentialCache();
        credCache.Add(new Uri(GetConnectionInfo()), "Digest", new NetworkCredential(Username, Password));
        var httpClient = new HttpClient(new HttpClientHandler { Credentials = credCache });
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(new Uri(uri));
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Recording list error: HTTP {response.StatusCode}");
                return false;
            }

            string xmlResponse = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(xmlResponse))
            {
                Console.WriteLine("Empty recording list response.");
                return false;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlResponse);

            XmlNodeList recordingNodes = doc.SelectNodes("//recording");
            foreach (XmlNode node in recordingNodes)
            {
                var statusAttr = node.Attributes?["recordingstatus"];
                if (statusAttr != null && statusAttr.Value.Equals("recording", StringComparison.OrdinalIgnoreCase))
                {
                    // Found an active recording
                    return true;
                }
            }
            // No active recording found
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("CheckRecordingStatus exception: " + ex.Message);
            return false;
        }
    }

    public async Task<VMSType> CheckVMS()
    {
        string uri = $"{GetConnectionInfo()}axis-cgi/admin/param.cgi?action=list&group=root.RemoteService.ServerList";

        var credCache = new CredentialCache();
        credCache.Add(new Uri(GetConnectionInfo()), "Digest", new NetworkCredential(Username, Password));
        var httpClient = new HttpClient(new HttpClientHandler { Credentials = credCache });
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(new Uri(uri));
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Recording list error: HTTP {response.StatusCode}");
                return VMSType.Unknown;
            }

            string rawResponse = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                Console.WriteLine("Empty recording list response.");
                return VMSType.Unknown;
            }

            if(rawResponse.Contains("axis.com", StringComparison.OrdinalIgnoreCase))
            {
                return VMSType.ACSEdge;
            }
            else if(rawResponse.Contains("yoursix.com", StringComparison.OrdinalIgnoreCase))
            {
                return VMSType.YourSix;
            }
            else
            {
                return VMSType.Unknown;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("CheckVMS exception: " + ex.Message);
            return VMSType.Unknown;
        }
    }
}

public enum VMSType
{
    Unknown,
    ACSEdge,
    YourSix
}