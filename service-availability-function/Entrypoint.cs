using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Amazon.Lambda.Core;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using BAMCIS.Lambda.Common;
using Newtonsoft.Json;
using System.Net.Http;
using System.Linq;
using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BAMCIS.ServiceAvailability
{
    public class Entrypoint
    {
        #region Private Fields

        private ILambdaContext _context;

        private static HttpClient httpClient = new HttpClient();

        private static IAmazonDynamoDB ddbClient;
        private static DynamoDBContext ddbContext;
        private static IAmazonCloudWatch cwClient;
        private static IAmazonSimpleSystemsManagement ssmClient;
        private static IAmazonS3 s3Client;

        private static string defaultDataUrl;
        private static Dictionary<string, string> timeZoneMap;
        private static List<string> globalServices;
        private static string defaultTimeZone;

        #endregion

        #region Constructors

        static Entrypoint()
        {
#if DEBUG
            var chain = new CredentialProfileStoreChain();
            chain.TryGetAWSCredentials("aws-service-availability", out AWSCredentials profile);
            ddbClient = new AmazonDynamoDBClient(profile);
            cwClient = new AmazonCloudWatchClient(profile);
            ssmClient = new AmazonSimpleSystemsManagementClient(profile);
            s3Client = new AmazonS3Client(profile);
#else
            ddbClient = new AmazonDynamoDBClient();
            cwClient = new AmazonCloudWatchClient();
            ssmClient = new AmazonSimpleSystemsManagementClient();
            s3Client = new AmazonS3Client();
#endif
            ddbContext = new DynamoDBContext(ddbClient);

            GetParameterResponse url = ssmClient.GetParameterAsync(new GetParameterRequest()
            {
                Name = "ServiceHealthDashboardDataUrl"
            }).Result;

            defaultDataUrl = url.Parameter.Value;

            GetParameterResponse tzParam = ssmClient.GetParameterAsync(new GetParameterRequest()
            {
                Name = "TimeZoneMap"
            }).Result;

            timeZoneMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(tzParam.Parameter.Value);

            GetParameterResponse globalServicesParam = ssmClient.GetParameterAsync(new GetParameterRequest()
            {
                Name = "GlobalServicesList"
            }).Result;

            globalServices = globalServicesParam.Parameter.Value.Split(',').ToList();

            GetParameterResponse defaultTzParam = ssmClient.GetParameterAsync(new GetParameterRequest()
            {
                Name = "DefaultTimeZone"
            }).Result;

            defaultTimeZone = defaultTzParam.Parameter.Value;
        }

        public Entrypoint()
        { }

        #endregion

        #region Public Methods

        /// <summary>
        /// Method to respond to an API request and retrieve the data from DynamoDB with 
        /// possible filters included
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetData(APIGatewayProxyRequest request, ILambdaContext context)
        {
            this._context = context;
            context.LogInfo($"Get data request\r\n{JsonConvert.SerializeObject(request)}");

            try
            {
                GetDashboardEventsRequest req = new GetDashboardEventsRequest(request.QueryStringParameters);
                List<ScanCondition> conditions = new List<ScanCondition>();

                if (req.Start > 0)
                {
                    conditions.Add(new ScanCondition("Date", ScanOperator.GreaterThanOrEqual, ServiceUtilities.ConvertFromUnixTimestamp(req.Start)));
                }

                if (req.End > 0)
                {
                    conditions.Add(new ScanCondition("Date", ScanOperator.LessThanOrEqual, ServiceUtilities.ConvertFromUnixTimestamp(req.End)));
                }

                if (req.Regions != null && req.Regions.Any())
                {
                    conditions.Add(new ScanCondition("Region", ScanOperator.In, req.Regions.ToArray())); // Casting to Array is important
                }

                if (req.Services != null && req.Services.Any())
                {
                    conditions.Add(new ScanCondition("Service", ScanOperator.In, req.Services.ToArray())); // Casting to Array is important
                }

                AsyncSearch<DashboardEventParsed> search = ddbContext.ScanAsync<DashboardEventParsed>(conditions);
                IEnumerable<DashboardEventParsed> data = await search.GetRemainingAsync();

                return CreateResponse(data, req);
            }
            catch (AggregateException e)
            {
                this._context.LogError(e);

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = FlattenToJsonString(e),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
            }
            catch (Exception e)
            {
                this._context.LogError(e);

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = JsonConvert.SerializeObject(e, new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                    }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
            }
        }

        /// <summary>
        /// Loads the data from the source into a dynamodb table
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> ManualLoadDataFromSource(APIGatewayProxyRequest request, ILambdaContext context)
        {
            this._context = context;
            context.LogInfo($"Load data request\r\n{JsonConvert.SerializeObject(request)}");

            try
            {
                if (request.QueryStringParameters != null &&
                    request.QueryStringParameters.ContainsKey("bucket") &&
                    request.QueryStringParameters.ContainsKey("key"))
                {

                    if (String.IsNullOrEmpty(request.QueryStringParameters["bucket"]))
                    {
                        throw new ArgumentException("bucket", "The parameter 'bucket' was specified, but no value was provided.");
                    }

                    if (String.IsNullOrEmpty(request.QueryStringParameters["key"]))
                    {
                        throw new ArgumentException("key", "The parameter 'key' was specified, but no value was provided.");
                    }

                    string bucket = request.QueryStringParameters["bucket"];
                    string key = request.QueryStringParameters["key"];
                    this._context.LogInfo($"Loading from custom location: s3://{bucket}/{key}");
                    await this.GetAndLoadData(bucket, key);
                }
                else if (request.QueryStringParameters != null &&
                    request.QueryStringParameters.ContainsKey("source"))
                {
                    if (String.IsNullOrEmpty(request.QueryStringParameters["source"]))
                    {
                        throw new ArgumentException("source", "The parameter 'source' was specified, but no value was provided.");
                    }

                    string source = request.QueryStringParameters["source"];

                    this._context.LogInfo($"Loading from custom location: {source}.");
                    await this.GetAndLoadData(source);
                }
                else
                {
                    this._context.LogInfo($"Loading from default location: {defaultDataUrl}.");
                    await this.GetAndLoadData(defaultDataUrl);
                }

                return new APIGatewayProxyResponse()
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = "{\"message\":\"complete\"}",
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
            }
            catch (Exception e)
            {
                this._context.LogError(e);

                return new APIGatewayProxyResponse()
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = $"{{\"message\":\"{e.Message}\"}}",
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
            }
        }

        /// <summary>
        /// Loads the data from the source into a dynamodb table
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task ScheduledLoadDataFromSource(ScheduledEvent request, ILambdaContext context)
        {
            this._context = context;
            context.LogInfo($"Load data request\r\n{JsonConvert.SerializeObject(request)}");

            try
            {
                await this.GetAndLoadData(defaultDataUrl);
            }
            catch (Exception e)
            {
                this._context.LogError(e);
            }
        }

        /// <summary>
        /// Begins an export job to move the data from DynamoDB to S3
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task ScheduledExportDataFromDynamoDB(ScheduledEvent request, ILambdaContext context)
        {
            this._context = context;
            context.LogInfo($"Load data request\r\n{JsonConvert.SerializeObject(request)}");

            ExportTableToPointInTimeResponse response = await ExportDataFromDynamoDB(Environment.GetEnvironmentVariable("DYNAMODB_TABLE_ARN"), Environment.GetEnvironmentVariable("EXPORT_BUCKET"));

            this._context.LogInfo(JsonConvert.SerializeObject(response));
        }

        /// <summary>
        /// Begins an export job to move the data from DynamoDB to S3
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> ManualExportDataFromDynamoDB(APIGatewayProxyRequest request, ILambdaContext context)
        {
            this._context = context;
            context.LogInfo($"Load data request\r\n{JsonConvert.SerializeObject(request)}");

            ExportTableToPointInTimeResponse response = await ExportDataFromDynamoDB(Environment.GetEnvironmentVariable("DYNAMODB_TABLE_ARN"), Environment.GetEnvironmentVariable("EXPORT_BUCKET"));

            string text = JsonConvert.SerializeObject(response);
            this._context.LogInfo(text);

            return new APIGatewayProxyResponse()
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = text,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the data from the source and loads it into DynamoDB
        /// </summary>
        /// <returns></returns>
        private async Task GetAndLoadData(string bucket, string key)
        {
            IEnumerable<DashboardEventRaw> data = await GetDashboardEventDataFromS3(bucket, key);
            await LoadData(data, $"s3://{bucket}/{key}");
        }

        /// <summary>
        /// Gets the data from the source and loads it into DynamoDB
        /// </summary>
        /// <returns></returns>
        private async Task GetAndLoadData(string source)
        {
            IEnumerable<DashboardEventRaw> data = await GetDashboardEventDataFromSource(source);
            await LoadData(data, source);
        }

        private async Task LoadData(IEnumerable<DashboardEventRaw> data, string source)
        {
            if (data == null)
            {
                this._context.LogError("Null was provided to LoadData, no data to load.");
                return;
            }

            BatchWrite<DashboardEventParsed> batch = ddbContext.CreateBatchWrite<DashboardEventParsed>();

            int misses = 0;

            foreach (DashboardEventRaw item in data)
            {
                DashboardEventParsed parsed;

                try
                {
                    parsed = DashboardEventParsed.FromRawEvent(item);
                    batch.AddPutItem(parsed);

                    if (parsed.Timeline.StartTimeWasFoundInDescription == false)
                    {
                        this._context.LogError($"Did not find start/end in description: {item.Description}");
                        misses += 1;
                    }
                }
                catch (Exception e)
                {
                    this._context.LogError(e);
                }
            }

            try
            {
                await batch.ExecuteAsync();
            }
            catch (Exception e)
            {
                this._context.LogError(e);
            }

            await cwClient.PutMetricDataAsync(new PutMetricDataRequest()
            {
                MetricData = new List<MetricDatum>() {
                            new MetricDatum() {
                              Value = misses,
                              MetricName = Environment.GetEnvironmentVariable("ExtractionFailureMetricName"),
                              TimestampUtc = DateTime.UtcNow,
                              Unit = StandardUnit.Count,
                              Dimensions = new List<Dimension>()
                              {
                                  new Dimension()
                                  {
                                      Name = "source",
                                      Value = source
                                  }
                              }
                            }
                        },
                Namespace = "SHD"
            });
        }

        /// <summary>
        /// Exports the data from DynamoDB to S3
        /// </summary>
        /// <param name="tableArn"></param>
        /// <param name="bucket"></param>
        /// <returns></returns>
        private async Task<ExportTableToPointInTimeResponse> ExportDataFromDynamoDB(string tableArn, string bucket)
        {
            DescribeContinuousBackupsResponse latestBackup = await ddbClient.DescribeContinuousBackupsAsync(new DescribeContinuousBackupsRequest()
            {
                TableName = tableArn.Split('/').Last()
            });

            this._context.LogInfo($"Restoring table ${tableArn} to {bucket} at restore point {latestBackup.ContinuousBackupsDescription.PointInTimeRecoveryDescription.LatestRestorableDateTime}.");

            return await ddbClient.ExportTableToPointInTimeAsync(new ExportTableToPointInTimeRequest()
            {
                ExportFormat = ExportFormat.ION,
                TableArn = tableArn,
                S3Bucket = bucket,
                ExportTime = latestBackup.ContinuousBackupsDescription.PointInTimeRecoveryDescription.LatestRestorableDateTime
            });
        }

        /// <summary>
        /// Retrieves the data.json file contents from the service health dashboard
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<DashboardEventRaw>> GetDashboardEventDataFromSource(string source)
        {
            try
            {
                string data = await httpClient.GetStringAsync(source);

                try
                {
                    DashboardEventData parsedData = JsonConvert.DeserializeObject<DashboardEventData>(data);
                    return parsedData.Archive; // .Concat(parsedData.Current) -> don't want to do this since current events may not have a finish time
                }
                catch (Exception e)
                {
                    this._context.LogError($"Failed to deserialize retrieved Service Health Dashboard data.", e);
                    return null;
                }
            }
            catch (Exception e)
            {
                this._context.LogError($"Failed to retrieve data file from {source}", e);
                return null;
            }
        }

        /// <summary>
        /// Retrieves the data.json file contents from the service health dashboard
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<DashboardEventRaw>> GetDashboardEventDataFromS3(string bucket, string key)
        {
            try
            {
                using (GetObjectResponse data = await s3Client.GetObjectAsync(new GetObjectRequest()
                {
                    BucketName = bucket,
                    Key = key
                }))
                {
                    using (Stream stream = data.ResponseStream)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            try
                            {
                                string content = reader.ReadToEnd();
                                DashboardEventData parsedData = JsonConvert.DeserializeObject<DashboardEventData>(content);
                                return parsedData.Archive; // .Concat(parsedData.Current) -> don't want to do this since current events may not have a finish time
                            }
                            catch (Exception e)
                            {
                                this._context.LogError($"Failed to deserialize retrieved Service Health Dashboard data.", e);
                                return null;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this._context.LogError($"Failed to retrieve data file from s3://{bucket}/{key}", e);
                return null;
            }
        }


        /// <summary>
        /// Filters the provided data based on the request
        /// </summary>
        /// <param name="data">The retrieved data to filter</param>
        /// <param name="request">The filter settings</param>
        /// <returns>Whether the item should be filtered out</returns>
        private bool ShouldInclude(DashboardEventParsed data, GetDashboardEventsRequest request)
        {
            bool include = true;

            if (request != null && data != null)
            {
                // If an end time was specified, but if before the start,
                // it is possible for End to be 0 and a start time specified, so 
                // don't throw for that case
                if (request.End > 0 && request.End < request.Start)
                {
                    throw new ArgumentException("The end date must be greater than or equal to the start.");
                }

                if (request.Services != null && request.Services.Any())
                {
                    include = request.Services.Contains(data.Service);

                    if (include == false)
                    {
                        return false;
                    }
                }

                if (request.Regions != null && request.Regions.Any())
                {
                    include = request.Regions.Contains(data.Region);

                    if (include == false)
                    {
                        return false;
                    }
                }

                // This comparison is against the posted Date property of the service health dashboard event, it is not comparing it to the beginning or
                // end dates,
                if (request.Start > 0)
                {
                    include = data.Start >= request.Start;

                    if (include == false)
                    {
                        return false;
                    }
                }

                if (request.End > 0)
                {
                    include = data.End <= request.End;

                    if (include == false)
                    {
                        return false;
                    }
                }
            }

            return include;
        }

        private APIGatewayProxyResponse CreateResponse(IEnumerable<DashboardEventParsed> data, GetDashboardEventsRequest request)
        {
            try
            {
                string body = String.Empty;
                string contentType = String.Empty;
                string contentDisposition = String.Empty;

                if (request.Output == "json")
                {
                    body = JsonConvert.SerializeObject(data);
                    contentType = "application/json";
                }
                else //Otherwise it's csv
                {
                    StringBuilder buffer = new StringBuilder();

                    buffer.AppendLine(String.Join(",", typeof(DashboardEventParsed).GetTypeInfo().GetProperties().Select(x => "\"" + x.Name + "\"")));

                    foreach (DashboardEventParsed item in data)
                    {
                        buffer.AppendLine(String.Join(",", item.GetType().GetProperties().Select(x => "\"" + (x.Name == "MonthlyOutageDurations" ? JsonConvert.SerializeObject((Dictionary<string, long>)x.GetValue(item)).Replace("\"", "\"\"") : x.GetValue(item).ToString().Replace("\"", "\"\"")) + "\"")));
                    }

                    //Move back 2 to remove the \r\n from the last AppendLine
                    buffer.Length += -2;
                    body = buffer.ToString();

                    contentType = "application/octet-stream";
                    contentDisposition = "attachment; filename='serviceavailability.csv'";
                }

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = body,
                    Headers = new Dictionary<string, string> { { "Content-Type", contentType }, { "Content-Disposition", contentDisposition }, { "Access-Control-Allow-Origin", "*" } }
                };
            }
            catch (Exception e)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = JsonConvert.SerializeObject(e, new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
            }
        }

        private static string FlattenToJsonString(AggregateException ex)
        {
            StringBuilder buffer = new StringBuilder();
            buffer.AppendLine("[");
            Stack<Exception> Exceptions = new Stack<Exception>(ex.InnerExceptions);
            while (Exceptions.Count > 0)
            {
                Exception Current = Exceptions.Pop();

                buffer.AppendFormat("{0},", JsonConvert.SerializeObject(Current, new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                }));

                if (Current.InnerException != null)
                {
                    Exceptions.Push(Current.InnerException);
                }
            }

            // Move it back 1 to get rid of the last comma
            buffer.Length = buffer.Length - 1;
            buffer.Append("]");

            return buffer.ToString();
        }
        private static string FlattenToJsonString(Exception ex)
        {
            StringBuilder buffer = new StringBuilder();
            bool hasMultiple = ex.InnerException != null;

            if (hasMultiple)
            {
                buffer.AppendLine("[");
            }

            while (ex != null)
            {
                buffer.AppendFormat("{0},", JsonConvert.SerializeObject(ex, new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                }));

                ex = ex.InnerException;
            }

            if (hasMultiple)
            {
                // Move it back 1 to get rid of the last comma
                buffer.Length = buffer.Length - 1;
                buffer.Append("]");
            }

            return buffer.ToString();
        }

        #endregion
    }
}
