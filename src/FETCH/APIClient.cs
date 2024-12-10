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
        private HttpClient _httpClient;
        private string _scheme;
        private string _host;
        private int _port;
        private Credentials _credentials;
        private string _userAgent;

        public APIClient(HttpClient httpClient, string scheme, string host, int port, Credentials credentials, string userAgent = @"sharphound/2.1.9.0")
        {
            _httpClient = httpClient;
            _scheme = scheme;
            _host = host;
            _port = port;
            _credentials = credentials;
            _userAgent = userAgent;
        }

        public static void LoadEnvVariablesFromFile()
        {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string envFilePath = Path.Combine(basePath, ".env");

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
            }
        }

        public static void LoadSpecificEnvVariables(out string DOMAIN, out int PORT, out string SCHEME, out string SHARPHOUND_USER_AGENT, out string SHARPHOUND_CLIENT_NAME, out string TOKEN_ID, out string TOKEN_KEY)
        {
            DOMAIN = Environment.GetEnvironmentVariable("DOMAIN");
            int.TryParse(Environment.GetEnvironmentVariable("PORT"), out PORT);
            SCHEME = Environment.GetEnvironmentVariable("SCHEME");
            SHARPHOUND_USER_AGENT = Environment.GetEnvironmentVariable("SHARPHOUND_USER_AGENT");
            SHARPHOUND_CLIENT_NAME = Environment.GetEnvironmentVariable("SHARPHOUND_CLIENT_NAME");
            TOKEN_ID = Environment.GetEnvironmentVariable("TOKEN_ID");
            TOKEN_KEY = Environment.GetEnvironmentVariable("TOKEN_KEY");
        }

        public static APIClient InitializeAPIClient(string TOKEN_ID, string TOKEN_KEY, string SCHEME, string DOMAIN, int PORT)
        {
            // Initialize Credentials
            Credentials adminCredentials = new Credentials(TOKEN_ID, TOKEN_KEY);

            // Initialize HTTP client handler
            var httpHandler = new HttpClientHandler();

            // Proxy settings
            //var trustAllCerts = new Fetch.TrustAllCertsPolicy();
            //ServicePointManager.ServerCertificateValidationCallback = trustAllCerts.ValidateCertificate;
            //httpHandler.Proxy = new WebProxy("http://127.0.0.1:8080");

            // Initialize authentication handler
            var authHandler = new AuthSigner(adminCredentials.TokenKey, adminCredentials.TokenId, httpHandler);

            // Initialize HttpClient
            var client = new HttpClient(authHandler);

            // Initialize User Agent Header
            var header = new ProductHeaderValue("sharphound",
                Assembly.GetExecutingAssembly().GetName().Version.ToString());
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(header));

            // Initialize and return APIClient
            return new APIClient(client, SCHEME, DOMAIN, PORT, adminCredentials);
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
                        && response.StatusCode != HttpStatusCode.Continue)
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

        public static async Task<JToken> CheckIfSharpHoundClientExists(APIClient adminClient, string sharpHoundClientName)
        {
            Console.WriteLine("[*] Checking if SharpHound client " + sharpHoundClientName + " exists");
            JObject getClientsResponse = await adminClient.GetClientsAsync();
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

        public async Task<HttpResponseMessage> CreateJobAsync(APIClient adminAPIClient, JToken sharpHoundClient)
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
            var request = CreateRequestMessage("POST", $"/api/v2/clients/{sharpHoundClient["id"]}/jobs", Encoding.UTF8.GetBytes(body));

            // Send the request
            Console.WriteLine($"[*] Creating job for SharpHound client at {DateTime.UtcNow}");
            HttpResponseMessage response = await adminAPIClient.SendRequestAsync(request);
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

        public static async Task SendItAsync(List<JObject> bloodHoundDataChunks)
        {
            // Get environment variables from %USERPROFILE%\.env
            LoadEnvVariablesFromFile();
            LoadSpecificEnvVariables(out string DOMAIN, out int PORT, out string SCHEME, out string SHARPHOUND_USER_AGENT, out string SHARPHOUND_CLIENT_NAME, out string TOKEN_ID, out string TOKEN_KEY);

            // Initialize API client using admin token
            APIClient adminAPIClient = InitializeAPIClient(TOKEN_ID, TOKEN_KEY, SCHEME, DOMAIN, PORT);

            // Check if a SharpHound ingest client exists and get a token for it, otherwise create one
            JToken sharpHoundClient = await CheckIfSharpHoundClientExists(adminAPIClient, SHARPHOUND_CLIENT_NAME);

            if (sharpHoundClient != null)
            {
                JObject newTokenForExistingClient = await adminAPIClient.GetNewClientTokenAsync(sharpHoundClient["id"].ToString());
                sharpHoundClient["token"] = newTokenForExistingClient;
            }
            else
            {
                JObject createdClient = await adminAPIClient.CreateClientAsync(SHARPHOUND_CLIENT_NAME, "sharphound");
                if (createdClient != null)
                {
                    sharpHoundClient = createdClient["data"] as JObject;
                }
            }

            if (sharpHoundClient == null) return;

            // Create job for SharpHound client
            HttpResponseMessage response = await adminAPIClient.CreateJobAsync(adminAPIClient, sharpHoundClient);

            // Create API client for SharpHound client
            Credentials sharpHoundClientCreds = new Credentials(sharpHoundClient["token"]["data"]["id"].ToString(), sharpHoundClient["token"]["data"]["key"].ToString());
            APIClient sharpHoundAPIClientSigned = InitializeAPIClient(sharpHoundClientCreds.TokenId, sharpHoundClientCreds.TokenKey, SCHEME, DOMAIN, PORT);

            // Get the job we created and start it
            JArray jobs = await sharpHoundAPIClientSigned.GetJobsAsync();
            if (jobs.Count == 0)
            {
                Console.WriteLine("[!] No jobs found");
                return;
            }
            JObject nextJob = jobs[0] as JObject;
            await sharpHoundAPIClientSigned.StartJobAsync((int)nextJob["id"]);

            // Prepare data
            int totalPosts = bloodHoundDataChunks.Count();
            int postsLeft = totalPosts;
            foreach (JObject chunk in bloodHoundDataChunks)
            {
                // Send data to ingest
                postsLeft--;
                response = await sharpHoundAPIClientSigned.PostIngestAsync(Encoding.UTF8.GetBytes(chunk.ToString(Formatting.None)), totalPosts, totalPosts - postsLeft);

            }
            // Mark the job as done so the ingest API scoops it up
            await sharpHoundAPIClientSigned.EndJobAsync();
        }

        public static async Task<(APIClient, JToken, APIClient)> GetAPIClients()
        {
            // Get environment variables from %USERPROFILE%\.env
            LoadEnvVariablesFromFile();
            LoadSpecificEnvVariables(out string DOMAIN, out int PORT, out string SCHEME, out string SHARPHOUND_USER_AGENT, out string SHARPHOUND_CLIENT_NAME, out string TOKEN_ID, out string TOKEN_KEY);

            // Initialize API client using admin token
            APIClient adminAPIClient = InitializeAPIClient(TOKEN_ID, TOKEN_KEY, SCHEME, DOMAIN, PORT);

            // Check if a SharpHound ingest client exists and get a token for it, otherwise create one
            JToken sharpHoundClient = await CheckIfSharpHoundClientExists(adminAPIClient, SHARPHOUND_CLIENT_NAME);

            if (sharpHoundClient != null)
            {
                JObject newTokenForExistingClient = await adminAPIClient.GetNewClientTokenAsync(sharpHoundClient["id"].ToString());
                sharpHoundClient["token"] = newTokenForExistingClient;
            }
            else
            {
                JObject createdClient = await adminAPIClient.CreateClientAsync(SHARPHOUND_CLIENT_NAME, "sharphound");
                if (createdClient != null)
                {
                    sharpHoundClient = createdClient["data"] as JObject;
                }
            }

            if (sharpHoundClient == null) return (null, null, null);

            // Create API client for SharpHound client
            Credentials sharpHoundClientCreds = new Credentials(sharpHoundClient["token"]["data"]["id"].ToString(), sharpHoundClient["token"]["data"]["key"].ToString());
            APIClient sharpHoundAPIClientSigned = InitializeAPIClient(sharpHoundClientCreds.TokenId, sharpHoundClientCreds.TokenKey, SCHEME, DOMAIN, PORT);
            return (adminAPIClient, sharpHoundClient, sharpHoundAPIClientSigned);
        }
    }

    public class Credentials
    {
        public string TokenId { get; }
        public string TokenKey { get; }

        public Credentials(string tokenId, string tokenKey)
        {
            TokenId = tokenId;
            TokenKey = tokenKey;
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