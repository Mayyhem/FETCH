using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sharphound
{
    public class APIClient
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TokenId { get; set; }
        public string TokenKey { get; set; }

        private HttpClient _httpClient;
        private string _scheme;
        private string _host;
        private int _port;
        private string _userAgent;

        public APIClient(string scheme, string host, int port, string tokenId, string tokenKey, string proxy = "")
        {
            _scheme = scheme;
            _host = host;
            _port = port;

            TokenId = tokenId;
            TokenKey = tokenKey;

            // Initialize HTTP client handler
            var httpHandler = new HttpClientHandler();

            // Proxy settings
            if (!string.IsNullOrEmpty(proxy))
            {
                var trustAllCerts = new Fetch.TrustAllCertsPolicy();
                ServicePointManager.ServerCertificateValidationCallback = trustAllCerts.ValidateCertificate;
                httpHandler.Proxy = new WebProxy(proxy);
            }

            // Initialize authentication handler
            var authHandler = new AuthSigner(tokenKey, tokenId, httpHandler);

            // Initialize HttpClient
            _httpClient = new HttpClient(authHandler);

            // Initialize User Agent Header
            var header = new ProductHeaderValue("sharphound",
                Assembly.GetExecutingAssembly().GetName().Version.ToString());
            _userAgent = header.ToString();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(header));
        }

        public static bool LoadEnvVariablesFromFile(string envFilePath = "")
        {
            if (string.IsNullOrEmpty(envFilePath))
            {
                string basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                envFilePath = Path.Combine(basePath, ".env");
            }

            if (File.Exists(envFilePath))
            {
                foreach (string line in File.ReadAllLines(envFilePath))
                {
                    string[] parts = line.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
                return true;
            }
            else
            {
                Console.WriteLine("[!] The specified environment variables file does not exist");
                return false;
            }

        }

        public static void LoadSpecificEnvVariables(out string domain, out int port, out string scheme, out string sharpHoundUserAgent, 
            out string sharpHoundClientName, out string userTokenId, out string userTokenKey, out string clientTokenId, out string clientTokenKey)
        {
            domain = Environment.GetEnvironmentVariable("DOMAIN");
            int.TryParse(Environment.GetEnvironmentVariable("PORT"), out port);
            scheme = Environment.GetEnvironmentVariable("SCHEME");
            sharpHoundUserAgent = Environment.GetEnvironmentVariable("SHARPHOUND_USER_AGENT");
            sharpHoundClientName = Environment.GetEnvironmentVariable("SHARPHOUND_CLIENT_NAME");
            userTokenId = Environment.GetEnvironmentVariable("USER_TOKEN_ID");
            userTokenKey = Environment.GetEnvironmentVariable("USER_TOKEN_KEY");
            clientTokenId = Environment.GetEnvironmentVariable("CLIENT_TOKEN_ID");
            clientTokenKey = Environment.GetEnvironmentVariable("CLIENT_TOKEN_KEY");
        }

        public static byte[] Compress(string toCompress)
        {
            var encoded = Encoding.UTF8.GetBytes(toCompress);
            var compressed = Compress(encoded);
            return compressed;
        }

        private static byte[] Compress(byte[] toCompress)
        {
            using var compressed = new MemoryStream();
            using (var zipStream = new GZipStream(compressed, CompressionMode.Compress))
            {
                zipStream.Write(toCompress, 0, toCompress.Length);
            }

            return compressed.ToArray();
        }

        public static ByteArrayContent GetCompressedContent(byte[] body)
        {
            var compressedContent = Compress(body);

            var requestContent = new ByteArrayContent(compressedContent, 0, compressedContent.Length);
            requestContent.Headers.Add("Content-Encoding", "gzip");
            requestContent.Headers.Add("Content-Type", "application/json");

            return requestContent;
        }


        private HttpRequestMessage CreateRequestMessage(string method, string uri, byte[] body = null)
        {
            var formattedUri = uri.StartsWith("/") ? uri.Substring(1) : uri;
            var request = new HttpRequestMessage(new HttpMethod(method), $"{_scheme}://{_host}:{_port}/{formattedUri}");

            if (body != null)
            {
                // gzip
                //request.Content = GetCompressedContent(body);
                // Don't gzip
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            request.Headers.Add("User-Agent", _userAgent);

            return request;
        }

        private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
        {
            HttpResponseMessage response = null;
            try
            {
                response = await _httpClient.SendAsync(request);
                if (response != null)
                {
                    if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted
                        && response.StatusCode != HttpStatusCode.Continue && response.StatusCode != HttpStatusCode.Created)
                    {
                        await Console.Out.WriteLineAsync($"[!] Did not receive an OK/Accept/Continue response from the server: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"[!] Did not receive a response from the server: {ex.Message}");
            }
            return response;
        }

        public async Task<JObject> GetClientsAsync()
        {
            var request = CreateRequestMessage("GET", "/api/v2/clients");

            var response = await SendRequestAsync(request);
            if (response == null)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            if (responseContent != null)
            {
                try
                {
                    return JObject.Parse(responseContent);
                }
                catch
                {
                    await Console.Out.WriteLineAsync("[!] Could not parse JSON from the response:");
                }
            }
            Console.WriteLine(responseContent);
            return null;
        }

        public async Task<JToken> CheckIfSharpHoundClientExists(string sharpHoundClientName)
        {
            Console.WriteLine("[*] Checking if SharpHound client " + sharpHoundClientName + " exists");
            JObject getClientsResponse = await GetClientsAsync();
            if (getClientsResponse != null)
            {
                JArray clients = (JArray)getClientsResponse["data"];

                foreach (JObject client in clients)
                {
                    if (client["name"] != null && client["name"].ToString() == sharpHoundClientName)
                    {
                        Console.WriteLine("[*] SharpHound client named " + sharpHoundClientName + " found!");
                        return client;
                    }
                }
            }
            Console.WriteLine("[!] SharpHound client named " + sharpHoundClientName + " not found");
            return null;
        }

        public async Task<JObject> GetNewClientTokenAsync(string clientId)
        {
            var request = CreateRequestMessage("PUT", $"/api/v2/clients/{clientId}/token");

            Console.WriteLine("[*] Generating new API token for SharpHound client");
            var response = await SendRequestAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseContent);
            return JObject.Parse(responseContent);
        }


        public async Task<JObject> CreateClientAsync(string name, string type = "sharphound", string domainController = "")
        {
            var data = new
            {
                name,
                type,
                domain_controller = domainController
            };

            var body = JsonConvert.SerializeObject(data);
            var request = CreateRequestMessage("POST", "/api/v2/clients", Encoding.UTF8.GetBytes(body));

            Console.WriteLine("[*] Creating SharpHound client: " + name);
            var response = await SendRequestAsync(request);
            if (response != null)
            {

                var responseContent = await response.Content.ReadAsStringAsync();
                if (responseContent != null)
                {
                    try
                    {
                        return JObject.Parse(responseContent);
                    }
                    catch
                    {
                        await Console.Out.WriteLineAsync("[!] Could not parse JSON from the response:");
                    }
                    Console.WriteLine(responseContent);
                }
            }
            return null;
        }

        public async Task<HttpResponseMessage> CreateFileUploadJobAsync()
        {
            // Create the HTTP request
            var request = CreateRequestMessage("POST", $"/api/v2/file-upload/start");

            // Send the request
            Console.WriteLine($"[*] Creating job for file upload at {DateTime.UtcNow}");
            HttpResponseMessage response = await SendRequestAsync(request);
            return response;
        }

        public async Task<JArray> GetFileUploadJobsAsync()
        {
            // Create the HTTP request
            var request = CreateRequestMessage("GET", $"/api/v2/file-upload");

            // Send the request
            Console.WriteLine($"[*] Getting file upload jobs");
            HttpResponseMessage response = await SendRequestAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return (JArray)JObject.Parse(responseContent)["data"];
        }

        public async Task<HttpResponseMessage> UploadFileAsync(int jobId, byte[] body, int totalPosts = 0, int thisPostNum = 0)
        {
            // Create the HTTP request
            var request = CreateRequestMessage("POST", $"/api/v2/file-upload/{jobId}", body);

            if (totalPosts > 0)
            {
                Console.WriteLine($"[*] Sending FETCH data chunk {thisPostNum} of {totalPosts} to API for ingestion");
            }

            // Send the request
            return await SendRequestAsync(request);
        }

        public async Task<HttpResponseMessage> EndFileUploadJobAsync(int jobId)
        {
            // Create the HTTP request
            var request = CreateRequestMessage("POST", $"/api/v2/file-upload/{jobId}/end");

            // Send the request
            Console.WriteLine($"[*] Ending file upload job at {DateTime.UtcNow}");
            HttpResponseMessage response = await SendRequestAsync(request);
            return response;
        }

        public async Task SendItFileUploadAsync(List<JObject> bloodHoundDataChunks)
        {
            // Create job for SharpHound client
            HttpResponseMessage response = await CreateFileUploadJobAsync();
            var responseContent = await response.Content.ReadAsStringAsync();
            int jobId = (int)JObject.Parse(responseContent)["data"]["id"];

            // Get the job we created and start it
            JArray jobs = await GetFileUploadJobsAsync();
            if (jobs.Count == 0)
            {
                Console.WriteLine("[!] No jobs found");
                return;
            }

            // Prepare datazt
            int totalPosts = bloodHoundDataChunks.Count();
            int postsLeft = totalPosts;
            foreach (JObject chunk in bloodHoundDataChunks)
            {
                // Send data to ingest
                postsLeft--;
                response = await UploadFileAsync(jobId, Encoding.UTF8.GetBytes(chunk.ToString(Formatting.None)), totalPosts, totalPosts - postsLeft);

            }
            // Mark the job as done so the ingest API scoops it up
            await EndFileUploadJobAsync(jobId);
        }


        public async Task<HttpResponseMessage> CreateJobAsync(string sharpHoundClientId)
        {
            // Set up the job data
            bool adStructureCollection = true;
            bool localGroupCollection = true;
            bool sessionCollection = true;
            bool allTrustedDomains = false;
            string[] domains = default;
            string[] ous = default;

            // Prepare the data
            var data = new
            {
                ad_structure_collection = adStructureCollection,
                all_trusted_domains = allTrustedDomains,
                domains,
                local_group_collection = localGroupCollection,
                ous,
                session_collection = sessionCollection
            };

            // Serialize to JSON
            var body = JsonConvert.SerializeObject(data);

            // Create the HTTP request
            var request = CreateRequestMessage("POST", $"/api/v2/clients/{sharpHoundClientId}/jobs", Encoding.UTF8.GetBytes(body));

            // Send the request
            Console.WriteLine($"[*] Creating job for SharpHound API client at {DateTime.UtcNow}");
            HttpResponseMessage response = await SendRequestAsync(request);
            return response;
        }

        public async Task<JArray> GetJobsAsync()
        {
            var request = CreateRequestMessage("GET", "/api/v2/jobs/available");

            Console.WriteLine("[*] Getting jobs for SharpHound client");
            var response = await SendRequestAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return (JArray)JObject.Parse(responseContent)["data"];
        }

        public async Task<JToken> GetPermissionsAsync()
        {
            var request = CreateRequestMessage("GET", "/api/v2/permissions");

            Console.WriteLine("[*] Getting permissions for SharpHound client");
            var response = await SendRequestAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseContent)["data"];
        }

        public async Task<JToken> GetSelfAsync()
        {
            var request = CreateRequestMessage("GET", "/api/v2/self");

            Console.WriteLine($"[*] Getting ID for token {TokenId}");
            var response = await SendRequestAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseContent)["data"];
        }

        public async Task<HttpResponseMessage> StartJobAsync(int jobId)
        {
            var data = new
            {
                id = jobId
            };

            var body = JsonConvert.SerializeObject(data);
            var request = CreateRequestMessage("POST", "/api/v2/jobs/start", Encoding.UTF8.GetBytes(body));

            Console.WriteLine("[*] Starting job with ID: " + jobId);
            return await SendRequestAsync(request);
        }

        public static async Task<HttpResponseMessage> StartNextJobAsync(APIClient signedIngestClient)
        {
            JArray jobs = await signedIngestClient.GetJobsAsync();
            if (jobs.Count == 0)
            {
                Console.WriteLine("[!] No jobs. Schedule an on-demand scan for client and run the script again.");
                return null;
            }
            JObject nextJob = jobs[0] as JObject;
            return await signedIngestClient.StartJobAsync((int)nextJob["id"]);
        }

        public async Task<HttpResponseMessage> PostIngestAsync(byte[] body, int totalPosts = 0, int thisPostNum = 0)
        {
            var request = CreateRequestMessage("POST", "/api/v2/ingest", body);

            if (totalPosts > 0)
            {
                Console.WriteLine($"[*] Sending FETCH data chunk {thisPostNum} of {totalPosts} to API for ingestion");
            }
            return await SendRequestAsync(request);
        }

        public async Task<HttpResponseMessage> EndJobAsync()
        {
            var data = new
            {
                status = "Complete",
                message = "Manual ingest upload"
            };

            var body = JsonConvert.SerializeObject(data);
            var request = CreateRequestMessage("POST", "/api/v2/jobs/end", Encoding.UTF8.GetBytes(body));

            Console.WriteLine($"[*] Marking job as done at {DateTime.UtcNow}");
            return await SendRequestAsync(request);
        }

        public async Task SendItAsync(string sharpHoundClientId, List<JObject> bloodHoundDataChunks)
        {
            // Create job for SharpHound client
            HttpResponseMessage response = await CreateJobAsync(sharpHoundClientId);

            // Get the job we created and start it
            JArray jobs = await GetJobsAsync();
            if (jobs.Count == 0)
            {
                Console.WriteLine("[!] No jobs found");
                return;
            }
            JObject nextJob = jobs[0] as JObject;
            await StartJobAsync((int)nextJob["id"]);

            // Prepare data
            int totalPosts = bloodHoundDataChunks.Count();
            int postsLeft = totalPosts;
            foreach (JObject chunk in bloodHoundDataChunks)
            {
                // Send data to ingest
                postsLeft--;
                response = await PostIngestAsync(Encoding.UTF8.GetBytes(chunk.ToString(Formatting.None)), totalPosts, totalPosts - postsLeft);

            }
            // Mark the job as done so the ingest API scoops it up
            await EndJobAsync();
        }

        public static async Task<APIClient> GetAPIClient(string envFilePath, string proxy)
        {
            // Get environment variables from %USERPROFILE%\.env
            bool success = LoadEnvVariablesFromFile(envFilePath);
            if (!success) {
                await Console.Out.WriteLineAsync("[!] Could not load the specified environment variables file");
                return null;
            }
            else 
            { 
                LoadSpecificEnvVariables(out string domain, out int port, out string scheme, out string sharpHoundUserAgent,
                    out string sharpHoundClientName, out string userTokenId, out string userTokenKey, out string clientTokenId, out string clientTokenKey);

                if (string.IsNullOrEmpty(userTokenId) && string.IsNullOrEmpty(userTokenKey))
                {
                    await Console.Out.WriteLineAsync("[!] Please specify a USER_TOKEN_ID and USER_TOKEN_KEY in the environment variables file");
                    return null;
                }

                APIClient userAPIClient = new APIClient(scheme, domain, port, userTokenId, userTokenKey, proxy);

                JToken userClientGetSelfResponse = await userAPIClient.GetSelfAsync();
                if (userClientGetSelfResponse != null)
                { 
                    userAPIClient.Id = userClientGetSelfResponse["id"].ToString();
                    await Console.Out.WriteLineAsync("[*] Received response for user token ");
                }
                else
                {
                    await Console.Out.WriteLineAsync("[!] Could not validate user token id and key");
                    return null;
                }

                /* Removed in favor of file upload to allow users with Upload-Only permission to use FETCH
                if (string.IsNullOrEmpty(clientTokenId) && string.IsNullOrEmpty(clientTokenKey))
                {

                    // Check if a SharpHound ingest client exists and get a token for it, otherwise create one
                    JToken sharpHoundClientResponse = await userAPIClient.CheckIfSharpHoundClientExists(sharpHoundClientName);

                    if (sharpHoundClientResponse != null)
                    {
                        JObject newTokenForExistingClientResponse = await userAPIClient.GetNewClientTokenAsync(sharpHoundClientResponse["id"].ToString());
                        if (newTokenForExistingClientResponse != null)
                        {
                            clientTokenId = newTokenForExistingClientResponse["data"]["id"].ToString();
                            clientTokenKey = newTokenForExistingClientResponse["data"]["key"].ToString();
                        }
                        else
                        {
                            await Console.Out.WriteLineAsync("[!] Could not create a new token for the specified client");
                            return (userAPIClient, null);
                        }
                    }
                    else
                    {
                        JObject createClientResponse = await userAPIClient.CreateClientAsync(sharpHoundClientName, "sharphound");
                        if (createClientResponse != null)
                        {
                            clientTokenId = createClientResponse["data"]["token"]["id"].ToString();
                            clientTokenKey = createClientResponse["data"]["token"]["key"].ToString();
                        }
                        else
                        {
                            await Console.Out.WriteLineAsync("[!] Could not create a new SharpHound client");
                            return (userAPIClient, null);
                        }
                    }
                }                

                APIClient sharpHoundAPIClient = new APIClient(scheme, domain, port, clientTokenId, clientTokenKey, proxy);

                JToken sharpHoundClientGetSelfResponse = await sharpHoundAPIClient.GetSelfAsync();
                if (sharpHoundClientGetSelfResponse != null) { 
                    sharpHoundAPIClient.Id = sharpHoundClientGetSelfResponse["id"].ToString();
                    await Console.Out.WriteLineAsync("[*] Received response for SharpHound client token");
                }
                else
                {
                    await Console.Out.WriteLineAsync("[!] Could not validate SharpHound client token id and key");
                    return (userAPIClient, null);
                }
                */
                return userAPIClient;
            }
        }
    }

    public static class TrustDirectionLookup
    {
        public static readonly Dictionary<int, string> Values = new Dictionary<int, string>()
        {
            { 0, "Disabled" },
            { 1, "Inbound" },
            { 2, "Outbound" },
            { 3, "Bidirectional" }
        };
    }

    public static class TrustTypeLookup
    {
        public static readonly Dictionary<int, string> Values = new Dictionary<int, string>()
        {
            { 0, "ParentChild" },
            { 1, "CrossLink" },
            { 2, "Forest" },
            { 3, "External" },
            { 4, "Unknown" }
        };
    }
}