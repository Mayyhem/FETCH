using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sharphound
{
    public class Fetch
    {
        public static ManagementScope NewWmiConnection(string server, string wmiNamespace)
        {
            ConnectionOptions connection = new ConnectionOptions();
            ManagementScope wmiConnection = null;

            try
            {
                if (!string.IsNullOrEmpty(wmiNamespace))
                {
                    wmiConnection = new ManagementScope(wmiNamespace, connection);
                    Console.WriteLine($"[+] Connecting to {wmiConnection.Path}");
                    wmiConnection.Connect();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                
                Console.WriteLine($"[!] Access to the WMI provider was not authorized: {ex.Message.Trim()}");
            }
            catch (ManagementException ex)
            {
                Console.WriteLine($"[!] Could not connect to {wmiNamespace}: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unhandled exception of type {ex.GetType()} occurred: {ex.Message}");
            }
            return wmiConnection;
        }

        public static int InitiateSccmClientOperationExMethodCall(string query, string smsProvider, string collectionId, string deviceId, string siteCode)
        {
            try
            {
                // Get the SMS_ClientOperation WMI class
                ManagementScope scope = NewWmiConnection(smsProvider, @$"root\SMS\site_{siteCode}");
                ManagementClass clientOperationClass = new ManagementClass(scope, new ManagementPath("SMS_ClientOperation"), null);

                //Prepare the content of the Param Parameter for the method call
                var queryPlainTextBytes = Encoding.UTF8.GetBytes(query);
                string queryBase64 = Convert.ToBase64String(queryPlainTextBytes);
                string parametersXML = $"<ScriptParameters><ScriptParameter ParameterGroupGuid=\"\" ParameterGroupName=\"PG_\" ParameterName=\"kustoquery\" ParameterDataType=\"System.String\" ParameterVisibility=\"0\" ParameterType=\"0\" ParameterValue=\"E:RSgwKQ==\"/><ScriptParameter ParameterGroupGuid=\"\" ParameterGroupName=\"PG_\" ParameterName=\"select\" ParameterDataType=\"System.String\" ParameterVisibility=\"0\" ParameterType=\"0\" ParameterValue=\"E:RGV2aWNlOkRldmljZSxMaW5lOk51bWJlcixDb250ZW50OlN0cmluZw==\"/><ScriptParameter ParameterGroupGuid=\"\" ParameterGroupName=\"PG_\" ParameterName=\"wmiquery\" GroupClass=\"\" ParameterDataType=\"System.String\" ParameterVisibility=\"0\" ParameterType=\"0\" ParameterValue=\"E:{queryBase64}\"/></ScriptParameters>";
                SHA256 SHA256 = new SHA256Cng();
                byte[] parametersBytes = SHA256.ComputeHash(Encoding.Unicode.GetBytes(parametersXML));
                string parametersHash = string.Join("", parametersBytes.Select(b => b.ToString("X2"))).ToLower();
                string contentXml = "" +
                            "<ScriptContent ScriptGuid='7DC6B6F1-E7F6-43C1-96E0-E1D16BC25C14'>" +
                                "<ScriptVersion>1</ScriptVersion>" +
                                "<ScriptType>0</ScriptType>" +
                                "<ScriptHash ScriptHashAlg='SHA256'>e77a6861a7f6fc25753bc9d7ab49c26d2ddfc426f025b902acefc406ae3b3732</ScriptHash>" +
                                "<ScriptParameters>" +
                                    "<ScriptParameter ParameterGroupGuid='' ParameterGroupName='PG_' ParameterName='kustoquery' ParameterDataType='System.String' ParameterVisibility='0' ParameterType='0' ParameterValue='E:RSgwKQ=='/>" +
                                    "<ScriptParameter ParameterGroupGuid='' ParameterGroupName='PG_' ParameterName='select' ParameterDataType='System.String' ParameterVisibility='0' ParameterType='0' ParameterValue='E:RGV2aWNlOkRldmljZSxMaW5lOk51bWJlcixDb250ZW50OlN0cmluZw=='/>" +
                                    $"<ScriptParameter ParameterGroupGuid='' ParameterGroupName='PG_' ParameterName='wmiquery' GroupClass='' ParameterDataType='System.String' ParameterVisibility='0' ParameterType='0' ParameterValue='E:{queryBase64}'/>" +
                                "</ScriptParameters>" +
                                $"<ParameterGroupHash ParameterHashAlg='SHA256'>{parametersHash}</ParameterGroupHash>" +
                            "</ScriptContent>";

                byte[] contentPlainTextBytes = Encoding.UTF8.GetBytes(contentXml);
                string contentBase64 = Convert.ToBase64String(contentPlainTextBytes);

                // Set up the rest of the input parameters for the method call
                ManagementBaseObject inParams = clientOperationClass.GetMethodParameters("InitiateClientOperationEx");
                inParams["Type"] = (uint)145;
                inParams["TargetCollectionID"] = collectionId;
                uint.TryParse(deviceId, out uint devId);
                inParams["TargetResourceIDs"] = new uint[] { devId };
                inParams["RandomizationWindow"] = null;
                inParams["Param"] = contentBase64;

                // Call the InitiateClientOperationEx method with the specified arguments
                ManagementBaseObject outParams = clientOperationClass.InvokeMethod("InitiateClientOperationEx", inParams, null);
                int operationId = Convert.ToInt32(outParams.Properties["OperationID"].Value);
                if (operationId > 0)
                {
                    Console.WriteLine("[+] Fallback method call succeeded");
                }
                else
                {
                    Console.WriteLine("[!] Method call failed with error code {0}.", operationId);
                }
                return operationId;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[!] An error occurred while attempting to call the SMS Provider: " + ex.Message);
                return 0;
            }
        }

        public static int GetSccmOperationIdForQuery(string query, string smsProvider, string siteCode, string collectionId)
        {
            int operationId = 0;

            // Trust self-signed certificates on SMS Providers
            var trustAllCerts = new TrustAllCertsPolicy();
            ServicePointManager.ServerCertificateValidationCallback = trustAllCerts.ValidateCertificate;

            // Prepare query
            string json = $"{{\"InputQuery\":\"{query}\"}}";
            byte[] data = Encoding.UTF8.GetBytes(json);

            // Prepare request to AdminService
            string url = $"https://{smsProvider}/AdminService/v1.0/Collections('{collectionId}')/AdminService.RunCMPivot";
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.UseDefaultCredentials = true;

            try
            {
                HttpWebResponse response = null;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                Console.WriteLine($"[+] Sending CMPivot query to AdminService for {query} on clients in collection {collectionId}");
                using (response = (HttpWebResponse)request.GetResponse())
                {
                    var statusCode = response.StatusCode;
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        var jsonResponse = streamReader.ReadToEnd();
                        var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonResponse);
                        var regex = new Regex("\"OperationId\":\\s*\\d+");
                        var match = regex.Match(jsonObject.ToString());

                        if (match.Success)
                        {
                            operationId = int.Parse(Regex.Match(match.Value, "\\d+").Value);
                            Console.WriteLine($"[+] Found OperationId for CMPivot query: {operationId}");
                        }
                        else
                        {
                            Console.WriteLine("[!] Could not find an OperationId in the response");
                        }
                    }
                    return operationId;
                }
            }

            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ex.Response as HttpWebResponse;
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.BadRequest:
                            //Handle 400 Error and fall back to SMS Provider method call to insure query is valid
                            query = !string.IsNullOrEmpty(query) ? query.Replace(@"\", @"\\") : null;
                            Console.WriteLine("[!] Received a 400 ('Bad request') response from the API. Falling back to SMS Provider WMI method ");
                            operationId = InitiateSccmClientOperationExMethodCall(query, smsProvider, collectionId, null, siteCode);
                            if (operationId != 0)
                            {
                                return operationId;
                            }
                            Console.WriteLine($"[!] The AdminService call failed due to an invalid query: {query}");
                            break;
                        case HttpStatusCode.NotFound:
                            // Handle HTTP 404 error
                            Console.WriteLine($"[!] Could not find a URI for a resource matching: {collectionId}");
                            break;
                        case HttpStatusCode.InternalServerError:
                            // Handle HTTP 500 error
                            Console.WriteLine($"[!] The requested AdminService endpoint responded with a 500 internal server error");
                            break;
                        default:
                            // Handle other HTTP errors
                            Console.WriteLine($"[!] The AdminService responded with an error message: {ex.Message}\r\n");
                            Console.WriteLine($"{ex.InnerException}\r\n");
                            Console.WriteLine($"{ex.StackTrace}");
                            break;
                    }
                }

                else if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    // Handle DNS resolution failure error
                    Console.WriteLine($"[!] The SMS Provider name could not be resolved: {smsProvider}");
                }
                return operationId;
            }
        }

        public static List<JObject> PrepareFileContentQueryResults(JObject cmPivotResponse)
        {
            List<JObject> fileContentResults = new List<JObject>();

            // Extract the "Content" property for each "Line"
            var queryResults = cmPivotResponse["value"]
                .SelectMany(lineValue => lineValue["Result"])
                .Select(lineResult => new
                {
                    Line = (string)lineResult["Line"],
                    Content = (string)lineResult["Content"],
                    Device = (string)lineResult["Device"]
                })
                .ToList();

            foreach (var lineData in queryResults)
            {
                // Parse the "Content" property value for each "Line" as a JObject
                JObject hostContent = JObject.Parse(lineData.Content);

                // Update the "version" attribute in the "meta" object to 5
                Console.WriteLine($"[*] Setting meta version");
                if (hostContent["meta"] is JObject metaObject)
                {
                    metaObject["version"] = 5;
                }
                fileContentResults.Add(hostContent);
            }
            return fileContentResults;
        }

        public static async Task<List<JObject>> GetFetchResultsFromDir(string remoteDirectory, int lookbackDays)
        {
            List<JObject> fetchResults = new List<JObject>();
            try
            {
                if (Directory.Exists(remoteDirectory))
                {
                    DateTime cutoffDate = DateTime.UtcNow.AddDays(-lookbackDays);

                    foreach (string filePath in Directory.EnumerateFiles(remoteDirectory, "FetchResults*.json"))
                    {
                        string fileName = Path.GetFileName(filePath);
                        // Extract date part from filename
                        Match match = Regex.Match(fileName, @"_(\d{8})-\d{6}-UTC\.json$");

                        if (match.Success)
                        {
                            string dateString = match.Groups[1].Value;
                            if (DateTime.TryParseExact(dateString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fileDate))
                            {
                                // Check if this date is within the lookback period
                                if (fileDate >= cutoffDate)
                                {
                                    try
                                    {
                                        // Read and deserialize JSON file
                                        using (StreamReader sr = new StreamReader(filePath))
                                        {
                                            string jsonText = await sr.ReadToEndAsync();
                                            JObject jsonObj = JObject.Parse(jsonText);
                                            fetchResults.Add(jsonObj);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[!] Error processing file {fileName}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[!] Directory does not exist.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] An error occurred: {ex.Message}");
            }
            return fetchResults;
        }

        public static async Task<JObject> QuerySccmAdminService(string query, string smsProvider, string siteCode, string collectionId, int timeout)
        {
            int operationId = GetSccmOperationIdForQuery(query, smsProvider, siteCode, collectionId);

            if (operationId != 0)
            {
                int attemptCounter = 1;
                int maxAttempts = 0;
                if (timeout > 0)
                {
                    // User supplied timeout from minutes to seconds with 1 request every 10 seconds
                    maxAttempts = timeout * 60 / 10;
                }
                int status = 0;
                string url = $"https://{smsProvider}/AdminService/v1.0/Collections('{collectionId}')/AdminService.CMPivotResult(OperationId={operationId})";

                // Trust self-signed certificates on SMS Providers
                var clientHandler = new HttpClientHandler();
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                clientHandler.UseDefaultCredentials = true;

                var client = new HttpClient(clientHandler);
                HttpResponseMessage response = null;
                Console.WriteLine($"[+] Querying for CMPivot operation results at {url}");

                // Loop infinitely or until the provided timeout is reached, checking for results every 10 seconds
                while (attemptCounter <= maxAttempts || timeout == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    Console.WriteLine($"[+] Checking whether CMPivot has finished querying clients: attempt {attemptCounter} of {(maxAttempts > 0 ? maxAttempts : "∞")}");
                    response = await client.GetAsync(url);
                    status = (int)response.StatusCode;

                    if (status != 200)
                    {
                        attemptCounter++;
                        Console.WriteLine("[+] Trying again in 10 seconds");
                    }
                    else
                    {
                        Console.WriteLine("[+] Successfully retrieved CMPivot results from AdminService");
                        break;
                    }
                }

                if (status == 200)
                {
                    // Deserialize the received JSON
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var jsonBody = responseBody.Replace("\\r\\n\\r\\n", Environment.NewLine);
                    var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonBody);

                    // Here we display the output as JSON after the user supplies the required flag
                    Console.WriteLine(jsonObject.ToString());
                    return jsonObject;
                }
                else if (status == 404)
                {
                    Console.WriteLine("[!] Could not collect FETCH results before FetchTimeout was reached");
                    Console.WriteLine("[+] This could mean that the device is not online or the timeout value is too short");
                    Console.WriteLine($"[+] When they are ready, the results can be retrieved manually from {url}");
                }
            }
            else
            {
                Console.WriteLine("[!] Could not successfully create an operation via the AdminService API");
            }
            return null;
        }

        public static async IAsyncEnumerable<FetchQueryResult> QuerySiteDatabase(
            string siteDatabaseFqdn,
            string siteCode,
            string tablePrefix,
            string collectionType,
            int lookbackDays,
            int pageSize = 1000)
        {
            string connectionString = $"Server={siteDatabaseFqdn};Database=CM_{siteCode};Integrated Security=True;";

            List<string> rowNames = new List<string>();
            if (collectionType == "Sessions" || collectionType == "TestSessions")
            {
                rowNames.Add("UserSID00");
                rowNames.Add("LastSeen00");
                rowNames.Add("ComputerSID00");
            }
            else if (collectionType == "UserRights" || collectionType == "TestUserRights")
            {
                rowNames.Add("Privilege00");
                rowNames.Add("ObjectIdentifier00");
                rowNames.Add("ObjectType00");
            }
            else if (collectionType == "LocalGroups" || collectionType == "TestLocalGroups")
            {
                rowNames.Add("GroupName00");
                rowNames.Add("GroupSID00");
                rowNames.Add("MemberType00");
                rowNames.Add("MemberSID00");
            }
            else
            {
                throw new ArgumentException("Invalid collection type");
            }

            // Get collection data organized by machine where data originated, filter to lookback period 
            string baseQuery = @$"
                WITH FilteredCollections AS (
                    SELECT 
                        MachineID,
                        CollectionDatetime00,
                        {string.Join(",\r\n", rowNames)}
                    FROM {tablePrefix}{collectionType}_DATA
                    WHERE CollectionDatetime00 >= DATEADD(day, -{lookbackDays}, GETDATE())
                )
                SELECT 
                    FC.MachineID,
                    FC.CollectionDatetime00,
                    {string.Join(",\r\n", rowNames.Select(r => $"FC.{r}"))},
                    SD.SID0,
                    SD.Netbios_Name0,
                    SD.Full_Domain_Name0
                FROM FilteredCollections FC
                LEFT JOIN System_DISC SD ON FC.MachineID = SD.ItemKey
                ORDER BY FC.CollectionDatetime00 DESC
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                int offset = 0;
                bool hasMoreResults = true;

                while (hasMoreResults)
                {
                    using (SqlCommand command = new SqlCommand(baseQuery, connection))
                    {
                        // Set the timeout to 300 seconds (5 minutes)
                        command.CommandTimeout = 300; 
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.Parameters.AddWithValue("@PageSize", pageSize);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                            {
                                hasMoreResults = false;
                                continue;
                            }

                            while (await reader.ReadAsync())
                            {
                                var result = new FetchQueryResult
                                {
                                    CollectedComputerMachineID = reader["MachineID"].ToString(),
                                    CollectionDatetime = Convert.ToDateTime(reader["CollectionDatetime00"]),
                                    CollectionData = new Dictionary<string, string>(),
                                    CollectedComputerSID = reader["SID0"].ToString(),
                                    CollectedComputerNetbiosName = reader["Netbios_Name0"].ToString(),
                                    CollectedComputerFullDomainName = reader["Full_Domain_Name0"].ToString()
                                };

                                foreach (string rowName in rowNames)
                                {
                                    result.CollectionData[rowName] = reader[rowName].ToString();
                                }

                                yield return result;
                            }
                        }
                    }

                    offset += pageSize;
                }
            }
        }

        public static async Task QueryDatabaseAndSendChunks(APIClient adminAPIClient, JToken sharpHoundClient,
            APIClient signedSharpHoundAPIClient, string tableName, Options options, ILogger logger)
        {
            try
            {
                // Create and start job for SharpHound client
                await adminAPIClient.CreateJobAsync(adminAPIClient, sharpHoundClient);
                JArray jobs = await signedSharpHoundAPIClient.GetJobsAsync();
                if (jobs.Count == 0)
                {
                    logger.LogError("No jobs found");
                    return;
                }
                JObject nextJob = jobs[0] as JObject;
                await signedSharpHoundAPIClient.StartJobAsync((int)nextJob["id"]);

                // Number of computers to fetch from the database and process in each chunk
                const int computersPerChunk = 300; 
                int totalComputersProcessed = 0;

                logger.LogInformation($"Querying table: {tableName}");

                bool hasMoreData = true;
                while (hasMoreData)
                {
                    var computerData = await FetchNextComputerChunk(options.SiteDatabase, options.SiteCode, options.TablePrefix, 
                        tableName, options.LookbackDays, computersPerChunk, totalComputersProcessed);

                    if (computerData.Count == 0)
                    {
                        hasMoreData = false;
                        continue;
                    }

                    await SendFormattedResults(signedSharpHoundAPIClient, computerData);
                    totalComputersProcessed += computerData.Count;
                    logger.LogInformation($"Processed {totalComputersProcessed} computers for {tableName}");
                }

                await signedSharpHoundAPIClient.EndJobAsync();
                logger.LogInformation($"Total computers processed for {tableName}: {totalComputersProcessed}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error processing or sending data: {ex.Message}");
            }
        }

        private static async Task<Dictionary<string, List<FetchQueryResult>>> FetchNextComputerChunk(
            string siteDatabaseFqdn, string siteCode, string tablePrefix, string collectionType,
            int lookbackDays, int computersPerChunk, int offset)
        {
            string connectionString = $"Server={siteDatabaseFqdn};Database=CM_{siteCode};Integrated Security=True;";
            var computerData = new Dictionary<string, List<FetchQueryResult>>();

            List<string> rowNames = GetRowNames(collectionType);

            string query = $@"
        WITH RankedComputers AS (
            SELECT 
                MachineID,
                ROW_NUMBER() OVER (ORDER BY MachineID) AS RowNum
            FROM (SELECT DISTINCT MachineID FROM {tablePrefix}{collectionType}_DATA) AS DistinctMachines
        ),
        TargetComputers AS (
            SELECT MachineID
            FROM RankedComputers
            WHERE RowNum > @Offset AND RowNum <= @Offset + @ComputersPerChunk
        )
        SELECT 
            FC.MachineID,
            FC.CollectionDatetime00,
            {string.Join(",\r\n", rowNames.Select(r => $"FC.{r}"))},
            SD.SID0,
            SD.Netbios_Name0,
            SD.Full_Domain_Name0
        FROM {tablePrefix}{collectionType}_DATA FC
        INNER JOIN TargetComputers TC ON FC.MachineID = TC.MachineID
        LEFT JOIN System_DISC SD ON FC.MachineID = SD.ItemKey
        WHERE FC.CollectionDatetime00 >= DATEADD(day, -@LookbackDays, GETDATE())
        ORDER BY FC.MachineID, FC.CollectionDatetime00 DESC";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    command.Parameters.AddWithValue("@Offset", offset);
                    command.Parameters.AddWithValue("@ComputersPerChunk", computersPerChunk);
                    command.Parameters.AddWithValue("@LookbackDays", lookbackDays);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var result = new FetchQueryResult
                            {
                                CollectedComputerMachineID = reader["MachineID"].ToString(),
                                CollectionDatetime = Convert.ToDateTime(reader["CollectionDatetime00"]),
                                CollectionData = new Dictionary<string, string>(),
                                CollectedComputerSID = reader["SID0"].ToString(),
                                CollectedComputerNetbiosName = reader["Netbios_Name0"].ToString(),
                                CollectedComputerFullDomainName = reader["Full_Domain_Name0"].ToString()
                            };

                            foreach (string rowName in rowNames)
                            {
                                result.CollectionData[rowName] = reader[rowName].ToString();
                            }

                            string computerSID = result.CollectedComputerSID;
                            if (string.IsNullOrEmpty(computerSID))
                            {
                                computerSID = $"S-1-5-21-1642199630-664550351-1777980924-{result.CollectedComputerMachineID}";
                                result.CollectedComputerSID = computerSID;
                                result.CollectedComputerNetbiosName = result.CollectedComputerMachineID;
                                result.CollectedComputerFullDomainName = "APERTURE.LOCAL";
                            }

                            if (!computerData.ContainsKey(computerSID))
                            {
                                computerData[computerSID] = new List<FetchQueryResult>();
                            }
                            computerData[computerSID].Add(result);
                        }
                    }
                }
            }

            return computerData;
        }

        private static List<string> GetRowNames(string collectionType)
        {
            switch (collectionType)
            {
                case "Sessions":
                case "TestSessions":
                    return new List<string> { "UserSID00", "LastSeen00", "ComputerSID00" };
                case "UserRights":
                case "TestUserRights":
                    return new List<string> { "Privilege00", "ObjectIdentifier00", "ObjectType00" };
                case "LocalGroups":
                case "TestLocalGroups":
                    return new List<string> { "GroupName00", "GroupSID00", "MemberType00", "MemberSID00" };
                default:
                    throw new ArgumentException("Invalid collection type");
            }
        }

        private static async Task SendFormattedResults(APIClient signedSharpHoundAPIClient, Dictionary<string, List<FetchQueryResult>> computerResults)
        {
            var formattedResults = new JObject
            {
                ["meta"] = new JObject
                {
                    ["count"] = computerResults.Count,
                    ["type"] = "computers",
                    ["version"] = 5,
                    ["methods"] = 107028
                },
                ["data"] = new JArray(computerResults.Select(kvp => FormatComputerData(kvp.Key, kvp.Value)))
            };

            await signedSharpHoundAPIClient.PostIngestAsync(Encoding.UTF8.GetBytes(formattedResults.ToString(Formatting.None)));
        }

        public static List<FetchQueryResult> ProcessCMPivotResults(JObject cmPivotResult)
        {
            var processedResults = new List<FetchQueryResult>();

            // Extract the "value" array from the JObject
            var value = cmPivotResult["value"] as JArray;
            if (value == null) return null;

            // Iterate through each item in the "value" array
            foreach (var item in value)
            {
                // Extract the "Result" array from each item
                var result = item["Result"] as JArray;
                if (result == null) continue;

                // Process each entry in the "Result" array
                foreach (var entry in result)
                {
                    // Extract CollectionDatetime and Device from the entry
                    var collectionDatetime = entry["CollectionDatetime"].ToString();
                    var device = entry["Device"].ToString();

                    // Check if a SiteDatabaseQueryResult already exists for this device and datetime
                    var existingResult = processedResults.FirstOrDefault(r =>
                        r.CollectedComputerNetbiosName == device &&
                        r.CollectionDatetime == DateTime.Parse(collectionDatetime));

                    // If no existing result, create a new SiteDatabaseQueryResult
                    if (existingResult == null)
                    {
                        existingResult = new FetchQueryResult
                        {
                            CollectedComputerNetbiosName = device,
                            CollectionDatetime = DateTime.Parse(collectionDatetime),
                            CollectionData = new Dictionary<string, string>()
                        };
                        processedResults.Add(existingResult);
                    }

                    // Populate the CollectionData dictionary with all properties
                    // except CollectionDatetime and Device
                    foreach (var property in entry.Children<JProperty>())
                    {
                        if (property.Name != "CollectionDatetime" && property.Name != "Device")
                        {
                            existingResult.CollectionData[property.Name] = property.Value.ToString();
                        }
                    }
                }
            }
            return processedResults;
        }


        public static JObject FormatQueryResults(List<FetchQueryResult> results)
        {
            var formattedResults = new JObject();

            // Group results by ComputerSID to process each computer's data together
            var groupedResults = results.GroupBy(r => r.CollectedComputerSID);
            
            // Only ingest results with one or more computers
            if (groupedResults.Count() > 0)
            {
                formattedResults = new JObject
                {
                    ["meta"] = new JObject
                    {
                        ["count"] = groupedResults.Count(),
                        ["type"] = "computers",
                        ["version"] = 5,
                        ["methods"] = 107028
                    },
                    ["data"] = new JArray(groupedResults.Select(group => FormatComputerData(group.Key, group)))
                };
            }

            return formattedResults;
        }

        private static JObject FormatComputerData(string objectIdentifier, IEnumerable<FetchQueryResult> computerResults)
        {
            var firstResult = computerResults.First();
            var computerData = new JObject
            {
                ["ObjectIdentifier"] = objectIdentifier,
                ["Properties"] = new JObject
                {
                    ["name"] = $"{firstResult.CollectedComputerNetbiosName}.{firstResult.CollectedComputerFullDomainName}"
                }
            };

            // Dictionary to store the most recent session for each unique UserSID and ComputerSID combination
            // Key: (UserSID, ComputerSID), Value: (LastSeen as DateTime, LastSeen as string)
            var sessionDict = new Dictionary<(string, string), (DateTime, string)>();

            // Dictionary to store unique UserRights
            // Key: Privilege, Value: Set of (ObjectIdentifier, ObjectType) tuples
            var userRights = new Dictionary<string, HashSet<(string, string)>>();

            // Dictionary to store LocalGroups
            // Key: GroupSID, Value: JObject representing the group
            var localGroups = new Dictionary<string, JObject>();

            foreach (var result in computerResults)
            {
                if (result.CollectionData.ContainsKey("UserSID00"))
                {
                    // Process Session data
                    var userSID = result.CollectionData["UserSID00"];
                    var computerSID = result.CollectionData["ComputerSID00"];
                    var lastSeenString = result.CollectionData["LastSeen00"];

                    // Parse the LastSeen string to DateTime for accurate comparison
                    var lastSeen = new DateTime();
                    DateTime.TryParseExact(lastSeenString, "M/d/yyyy h:mm:ss tt",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out lastSeen);

                    // Format the LastSeen back to the desired output format
                    var formattedLastSeen = lastSeen.ToString("yyyy-MM-dd HH:mm UTC");

                    var key = (userSID, computerSID);

                    // Update the dictionary if this is a new or more recent session
                    if (!sessionDict.ContainsKey(key) || lastSeen > sessionDict[key].Item1)
                    {
                        sessionDict[key] = (lastSeen, formattedLastSeen);
                    }
                }
            
                else if (result.CollectionData.ContainsKey("Privilege00"))
                {
                    // Process UserRights data
                    var privilege = result.CollectionData["Privilege00"];
                    var rightObjectIdentifier = result.CollectionData["ObjectIdentifier00"];
                    var objectType = result.CollectionData["ObjectType00"];

                    if (!userRights.ContainsKey(privilege))
                    {
                        userRights[privilege] = new HashSet<(string, string)>();
                    }
                    userRights[privilege].Add((rightObjectIdentifier, objectType));
                }

                else if (result.CollectionData.ContainsKey("GroupName00"))
                {
                    // Process LocalGroups data
                    var groupName = result.CollectionData["GroupName00"];
                    var groupSID = result.CollectionData["GroupSID00"];
                    var memberSID = result.CollectionData["MemberSID00"];
                    var memberType = result.CollectionData["MemberType00"];

                    if (!localGroups.ContainsKey(groupSID))
                    {
                        // Create a new group if it doesn't exist
                        localGroups[groupSID] = new JObject
                        {
                            ["LocalNames"] = new JArray(),
                            ["Name"] = groupName,
                            ["Collected"] = true,
                            ["FailureReason"] = null,
                            ["Results"] = new JArray(),
                            ["ObjectIdentifier"] = groupSID
                        };
                    }

                    var results = (JArray)localGroups[groupSID]["Results"];

                    // Add the member to the group if it's not already there
                    if (!results.Any(r => (string)r["ObjectIdentifier"] == memberSID))
                    {
                        results.Add(new JObject
                        {
                            ["ObjectIdentifier"] = memberSID,
                            ["ObjectType"] = memberType
                        });
                    }
                }
            }

            // Add Sessions to the computer data if any exist
            if (sessionDict.Count > 0)
            {
                computerData["Sessions"] = new JObject
                {
                    ["Collected"] = true,
                    ["FailureReason"] = null,
                    ["Results"] = new JArray(sessionDict.Select(kvp => new JObject
                    {
                        ["ComputerSID"] = kvp.Key.Item2,
                        ["LastSeen"] = kvp.Value.Item2,
                        ["UserSID"] = kvp.Key.Item1
                    }))
                };
            }

            // Add UserRights to the computer data if any exist
            if (userRights.Count > 0)
            {
                computerData["UserRights"] = new JArray(userRights.Select(kvp => new JObject
                {
                    ["LocalNames"] = new JArray(),
                    ["Collected"] = true,
                    ["FailureReason"] = null,
                    ["Privilege"] = kvp.Key,
                    ["Results"] = new JArray(kvp.Value.Select(v => new JObject
                    {
                        ["ObjectIdentifier"] = v.Item1,
                        ["ObjectType"] = v.Item2
                    }))
                }));
            }

            // Add LocalGroups to the computer data if any exist
            if (localGroups.Count > 0)
            {
                computerData["LocalGroups"] = new JArray(localGroups.Values);
            }

            return computerData;
        }

        public static List<JObject> FormatAndChunkQueryResults(List<FetchQueryResult> results, int chunkSize = 100)
        {
            var formattedResults = FormatQueryResults(results);

            // Chunk size testing
            //formattedResults = JObject.Parse(File.ReadAllText("../../../src/FETCH/computers.json"));

            var dataArray = (JArray)formattedResults["data"];

            // If there are chunkSize or fewer items, return the original result in a list
            if (dataArray.Count <= chunkSize)
            {
                return new List<JObject> { formattedResults };
            }

            // Split the data array into chunks
            var chunks = new List<JObject>();
            for (int i = 0; i < dataArray.Count; i += chunkSize)
            {
                var chunkArray = new JArray(dataArray.Skip(i).Take(chunkSize));
                var chunkObject = new JObject();

                // Copy the meta object instead of creating a new one with the original as content
                chunkObject["meta"] = formattedResults["meta"].DeepClone();
                chunkObject["data"] = chunkArray;

                // Update the count in the meta object for this chunk
                ((JObject)chunkObject["meta"])["count"] = chunkArray.Count;

                chunks.Add(chunkObject);
            }

            return chunks;
        }

        public class TrustAllCertsPolicy
        {
            public bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            }
        }
    }
}
