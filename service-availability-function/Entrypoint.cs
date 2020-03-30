using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using BAMCIS.Lambda.Common;
using BAMCIS.Parallel.Interleaved;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// The functions that are utilized by the serverless application model via API Gateway
    /// </summary>
    public class Entrypoint
    {
        private ILambdaContext context;
        private List<Task<PublishResponse>> snsResponses;
        private List<PublishRequest> snsRequests;

        private static HttpClient httpClient;
        private static AmazonSimpleNotificationServiceClient snsClient;

        static Entrypoint()
        {
            httpClient = new HttpClient();
            snsClient = new AmazonSimpleNotificationServiceClient();
        }

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Entrypoint()
        {
            this.snsResponses = new List<Task<PublishResponse>>();
            this.snsRequests = new List<PublishRequest>();
        }

        /// <summary>
        /// A Lambda function to respond to HTTP Get methods from API Gateway
        /// </summary>
        /// <param name="request">The request for service availability metrics</param>
        /// <returns>The transformed service availability data</returns>
        public async Task<APIGatewayProxyResponse> Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            this.context = context;
            this.context.LogInfo($"Get Request\r\n{JsonConvert.SerializeObject(request)}");

            try
            {
                SLARequest slaRequest = new SLARequest(request.QueryStringParameters);
                IEnumerable<SLAResponse> Results = ParseData(FilterData(await GetSlaData(), slaRequest));

                string body = String.Empty;
                string contentType = String.Empty;
                string contentDisposition = String.Empty;

                if (slaRequest.Output == "json")
                {
                    body = JsonConvert.SerializeObject(Results);
                    contentType = "application/json";
                }
                else //Otherwise it's csv
                {
                    StringBuilder buffer = new StringBuilder();

                    buffer.AppendLine(String.Join(",", typeof(SLAResponse).GetTypeInfo().GetProperties().Select(x => "\"" + x.Name + "\"")));

                    foreach (SLAResponse Item in Results)
                    {
                        buffer.AppendLine(String.Join(",", Item.GetType().GetProperties().Select(x => "\"" + (x.Name == "MonthlyOutageDurations" ? JsonConvert.SerializeObject((Dictionary<string, long>)x.GetValue(Item)).Replace("\"", "\"\"") : x.GetValue(Item).ToString().Replace("\"", "\"\"")) + "\"")));
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
            catch (AggregateException e)
            {
                this.context.LogError(e);

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = FlattenToJsonString(e),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
            }
            catch (Exception e)
            {
                this.context.LogError(e);

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
            finally
            {
                //If any SNS messages need to be sent, send them
                if (this.snsRequests.Any())
                {
                    this.context.LogInfo("There are SNS messages to send.");
                    
                    this.snsRequests.ForEach((Action<PublishRequest>)(x =>
                    {
                        try
                        {
                            this.snsResponses.Add((Task<PublishResponse>)snsClient.PublishAsync(x));
                        }
                        catch (Exception e)
                        {
                            this.context.LogError($"There was a problem publishing SNS message:\r\n{JsonConvert.SerializeObject(x)}", e);
                        }
                    }));

                    foreach (Task<PublishResponse> Bucket in this.snsResponses.Interleaved())
                    {
                        try
                        {
                            PublishResponse Response = await Bucket;

                            if ((int)Response.HttpStatusCode < 200 || (int)Response.HttpStatusCode > 299)
                            {
                                this.context.LogError($"SNS publishing failed for message {Response.MessageId} : {(int)Response.HttpStatusCode} {Response.HttpStatusCode}");
                            }
                        }
                        catch (OperationCanceledException ex)
                        {
                            this.context.LogError(ex);
                        }
                        catch (Exception ex)
                        {
                            this.context.LogError(ex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the data.json file from the service health dashboard and converts
        /// it to a single IEnumerable of entries
        /// </summary>
        /// <returns>A concatenated enumerable of current and archive data entries</returns>
        private async Task<IEnumerable<DataEntry>> GetSlaData()
        {
            /* Example entry from the sla data
             {
                "service_name": "AWS Identity and Access Management (N. Virginia)",
                "summary": "[RESOLVED] Delays for User and Policy Updates",
                "date": "1481033166",
                "status": "0",
                "details": "",
                "description": "<div><span class=\"yellowfg\"> 6:06 AM PST</span>&nbsp;Between 4:25 AM to 5:25 AM PST we experienced increased propagation delays in delivering changes made to IAM. Previously created credentials were not impacted during this period. The issue has been resolved and the service is operating normally.</div>",
                "service": "iam-us-east-1"
            }
            */

            try
            {
               
                string data = await httpClient.GetStringAsync(Config.Instance.Url);

                try
                {
                    SlaData parsedData = JsonConvert.DeserializeObject<SlaData>(data);
                    return parsedData.Archive.Concat(parsedData.Current);
                }
                catch (Exception e)
                {
                    this.context.LogError($"Failed to deserialize retrieved Service Health Dashboard data.", e);
                    return null;
                }
            }
            catch (Exception e)
            {
                this.context.LogError($"Failed to retrieve data file from {Config.Instance.Url}", e);
                return null;
            }
        }

        /// <summary>
        /// Filters the provided data based on the SLARequest settings
        /// </summary>
        /// <param name="data">The retrieved data to filter</param>
        /// <param name="request">The filter settings</param>
        /// <returns>The modified data object is returned</returns>
        private IEnumerable<DataEntry> FilterData(IEnumerable<DataEntry> data, SLARequest request)
        {
            if (request != null && data != null)
            {
                if (!String.IsNullOrEmpty(request.Services))
                {
                    IEnumerable<string> services = request.Services.Split(',').Select(x => x.ToLower());
                    //We want to split the region off the service, except for management console which uses a hyphen
                    data = data.Where(x => (x.Service == "management-console" ? services.Contains(x.Service.ToLower()) : services.Contains(x.Service.Split('-')[0].ToLower())));
                }

                //If regions are specified, return items that have that region in the service name
                //or are considered global services and have no associated region
                if (!String.IsNullOrEmpty(request.Regions))
                {
                    data = data.Where(x => (Config.Instance.GlobalServices.Contains(x.Service.ToLower()) ? true : request.GetRegions().Contains(x.Service.Substring(x.Service.IndexOf('-') + 1).ToLower())));
                }

                // This comparison is against the posted Date property of the service health dashboard event, it is not comparing it to the beginning or
                // end dates,
                if (request.Start > 0)
                {
                    DateTime start = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(request.Start);
                    data = data.Where(x => x.GetDate() >= start);
                }

                if (request.End > 0)
                {
                    if (request.End >= request.Start)
                    {
                        DateTime end = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(request.End);
                        data = data.Where(x => x.GetDate() <= end);
                    }
                    else
                    {
                        throw new ArgumentException("The end date must be greater than or equal to the start.");
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Parses the filtered data and converts the DataEntry objects to SLAResponse objects
        /// that have the region, begin, end, and elapsed attributes added
        /// </summary>
        /// <param name="data">The enumerable of raw data entries from the service health dashboard</param>
        /// <returns>An enumerable of parsed SLAResponse objects</returns>
        private IEnumerable<SLAResponse> ParseData(IEnumerable<DataEntry> data)
        {
            if (data != null)
            {
                return data.Select(x =>
                {
                    try
                    {
                        SLAResponse response = new SLAResponse();
                        response.Service = (x.Service == "management-console" ? x.Service.ToLower() : x.Service.Split('-')[0].ToLower());

                        if (Config.Instance.GlobalServices.Contains(response.Service))
                        {
                            Match regionMatch = Config.Instance.Regex[Config.REGION].GetRegex().Match(x.Description);
                            if (regionMatch.Success)
                            {
                                response.Region = regionMatch.Groups[1].Value.ToLower();
                            }
                            else
                            {
                                response.Region = "global";
                            }
                        }
                        else
                        {
                            response.Region = x.Service.Substring(x.Service.IndexOf('-') + 1).ToLower();
                        }


                        // Remove all open tags, make close div tags new lines, then replace all other close tags with a space, then remove nbsp characters
                        response.Description = Regex.Replace(Regex.Replace(Regex.Replace(x.Description, "<[a-zA-Z]+.*?[^?]>", ""), "<\\/div.*?[^?]>", "\r\n"), "<\\/.*?[^?]>", " ").Replace("&nbsp;", "");
                        response.Summary = x.Summary;
                        response.Date = Int64.Parse(x.Date);

                        Match descriptionMatch = Config.Instance.Regex[Config.COMBINED].GetRegex().Match(x.Description);
                        Tuple<DateTime, DateTime> result = this.GetBeginEnd(descriptionMatch, ConvertFromUnixTimestamp(response.Date), x.Description);

                        response.Began = ConvertToUnixTimestamp(result.Item1);
                        response.Ended = ConvertToUnixTimestamp(result.Item2);

                        return response;
                    }
                    catch (Exception e)
                    {
                        this.context.LogError($"Could not parse data entry {JsonConvert.SerializeObject(x)}", e);

                        if (!String.IsNullOrEmpty(Config.Instance.SNSTopic))
                        {
                            //These will all get awaited later and we'll log their success
                            this.snsRequests.Add(new PublishRequest(Config.Instance.SNSTopic, FlattenToJsonString(e), e.Message));
                        }

                        return null;
                    }
                }).Where(x => x != null);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the beginning and end time from an outage description
        /// </summary>
        /// <param name="match">The regex match of the description</param>
        /// <param name="defaultDate">The default date provided with the outage item</param>
        /// <param name="description">The original description</param>
        /// <returns>An Tuple with a begin DateTime and an end DateTime</returns>
        private Tuple<DateTime, DateTime> GetBeginEnd(Match match, DateTime defaultDate, string description)
        {
            /*
             * Group 1 = Calendar date like Sept 14th or March 9
             * Group 2 = Time like 11:29 PM or 10:11 PM PDT or 02:31
             * Group 3 = Slash date like 7/30 or 8/27
             * Group 4 = Group 1
             * Group 5 = Group 2
             * Group 6 = Group 3
             */

            DateTime begin = DateTime.MinValue;
            DateTime end = DateTime.MinValue;
            defaultDate = defaultDate.ToUniversalTime();

            /*
             * There are 3 options
             * 1) Just a time
             * 2) Time then slashed date
             * 3) Date then time
             */

            // All combinations will have a time

            // If the normal regex worked, use this sequence
            if (match.Groups.Cast<Group>().Any(x => x.Success))
            {
                // Get the matching time strings from the description
                string time1 = match.Groups[2].Value;
                string time2 = match.Groups[5].Value;

                Regex tzRegex = Config.Instance.Regex[Config.TIMEZONE].GetRegex();

                // Does the end time have a time zone attached, which is where it is normally found
                Match time2Match = tzRegex.Match(time2);

                // If we found a timezone on the second time, and there's not a time zone on the first, add the time zone to it
                if (time2Match.Success && !tzRegex.IsMatch(time1))
                {
                    time1 += $" {time2Match.Groups[1].Value}";
                }

                // These functions will replace any found time zone like PST, PDT, etc with a GMT offset value, which can
                // be successfully parsed by DateTime

                // This will be the begin time
                time1 = FormatTimeZone(time1);

                // This will be the end time
                time2 = FormatTimeZone(time2);

                string date1 = String.Empty;
                string date2 = String.Empty;

                if (match.Groups[1].Success) //Date then time
                {
                    // DateTime can only parse this info in the form of dd MMM yyyy, so the month can only be three letters max

                    date1 = match.Groups[1].Value.ToLower().Replace("th", "").Replace("st", "").Replace("nd", "").Replace("rd", "");
                    string[] parts = date1.Split(' ');
                    date1 = $"{parts[1]} {parts[0].Substring(0, 3)} {defaultDate.Year}";

                    date2 = match.Groups[4].Value.ToLower().Replace("th", "").Replace("st", "").Replace("nd", "").Replace("rd", "");
                    parts = date2.Split(' ');
                    date2 = $"{parts[1]} { parts[0].Substring(0, 3)} {defaultDate.Year}";
                }
                else if (match.Groups[3].Success) //Time then slashed date
                {
                    date1 = $"{match.Groups[3].Value}/{defaultDate.Year}";

                    if (match.Groups[6].Success)
                    {
                        date2 = $"{match.Groups[6].Value}/{defaultDate.Year}";
                    }
                    else
                    {
                        this.context.LogInfo($"There is no end slash date, using beginning {date1} : {description}.");
                        date2 = date1;
                    }
                }
                else if (match.Groups[2].Success) //Just a time
                {
                    date1 = defaultDate.ToString("MM/dd/yyyy");
                    date2 = defaultDate.ToString("MM/dd/yyyy");
                }

                try
                {
                    begin = DateTime.Parse($"{date1} {time1}").ToUniversalTime();
                }
                catch (Exception e)
                {
                    throw new Exception($"Could not parse begin date, {date1} {time1}, to DateTime.", e);
                }

                try
                {
                    end = DateTime.Parse($"{date2} {time2}").ToUniversalTime();
                }
                catch (Exception e)
                {
                    throw new Exception($"Could not parse end date, {date2} {time2}, to DateTime.", e);
                }

                // This will possibly happen if the end time was missing an AM/PM indicator
                if (end < begin)
                {
                    // Let's look at the original value to see if it was missing
                    if (!Regex.IsMatch(time2, "(?:AM|PM)"))
                    {
                        // It was missing, so it must have been a PM time that was read as an AM time, lets add 12 hours to the time
                        end = end.AddHours(12);
                    }
                    else
                    {
                        throw new Exception($"The End date resulted in being earlier than the start date although an AM/PM indicator was present: {description}.");
                    }
                }
            }
            else
            {
                // Otherwise, we need to match based on the periodic time listing and extract the latest date
                IEnumerable<Match> periodicMatch = Config.Instance.Regex[Config.PERIODIC].GetRegex().Matches(description).Cast<Match>();

                if (periodicMatch.Any(x => x.Success))
                {
                    List<DateTime> dates = new List<DateTime>();

                    foreach (Match date in periodicMatch)
                    {
                        bool goBackADay = false;
                        string dateString = String.Empty;

                        string time = FormatTimeZone(date.Groups[2].Value);

                        if (date.Groups[1].Success)
                        {
                            dateString = date.Groups[1].Value.ToLower().Replace("th", "").Replace("st", "").Replace("nd", "").Replace("rd", "");
                            string[] parts = dateString.Split(' ');
                            dateString = $"{parts[1]} {parts[0].Substring(0, 3)} {defaultDate.Year}";
                        }
                        else
                        {
                            // If any of the Date matches have a date, that means the outage extended over 1 day or more
                            // and the default date could be from the date AWS posted its final update to the case

                            Match temp = periodicMatch.FirstOrDefault(x => x.Groups[1].Success);

                            // There is a following match that has a date, check what it is
                            if (temp != null)
                            {
                                int day = defaultDate.Day;

                                // The date is usually like Jun 5, so test the second part first, if
                                // not successful, then test the first in case it is 5 Jun
                                if (!Int32.TryParse(temp.Groups[1].Value.Split(' ')[1], out day))
                                {
                                    if (Int32.TryParse(temp.Groups[1].Value.Split(' ')[0], out day))
                                    {
                                        goBackADay = true;
                                    }
                                }
                                else
                                {
                                    goBackADay = true;
                                }


                                DateTime tempDate = new DateTime(defaultDate.Year, defaultDate.Month, day).ToUniversalTime();

                                // If the periodic updates spanned multiple days, and this date didn't have a calendar day attached
                                // we found the next entry that had a date and need to go 1 day prior to that.
                                if (goBackADay)
                                {
                                    tempDate = tempDate.AddDays(-1);
                                }

                                dateString = tempDate.ToString("MM/dd/yyyy");
                            }
                            else
                            {
                                dateString = defaultDate.ToString("MM/dd/yyyy");
                            }
                        }

                        try
                        {
                            DateTime newDate = DateTime.Parse($"{dateString} {time}").ToUniversalTime();

                            dates.Add(newDate);
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"Could not parse date, {time}, to DateTime", e);
                        }
                    }

                    if (dates.Count >= 1)
                    {
                        dates.Sort();
                        begin = dates.First();
                        end = dates.Last();
                    }
                }
                else
                {
                    // Missed the regex
                    throw new Exception($"{description} - MISSED REGEX");
                }
            }

            return new Tuple<DateTime, DateTime>(begin, end);
        }

        /// <summary>
        /// Formats a time string into a string that DateTime can correctly parse
        /// </summary>
        /// <param name="time">The time string to format</param>
        /// <returns>A formatted time string that DateTime can parse</returns>
        private static string FormatTimeZone(string time)
        {
            //Convert PDT, PST type time zones to GMT offset
            Match timezoneMatch = Config.Instance.Regex[Config.TIMEZONE].GetRegex().Match(time);

            //If the time has a time zone, replace it with the offset from the timezone map object
            if (timezoneMatch.Success)
            {
                string tz = timezoneMatch.Groups[1].Value;
                if (Config.Instance.TimeZoneMap.ContainsKey(tz))
                {
                    time = time.Replace(tz, Config.Instance.TimeZoneMap[tz]);
                }
                else
                {
                    //The time zone map doesn't have the time zone, use the default time zone
                    //Make sure the user didn't put an invalid time zone in as the default
                    if (Config.Instance.TimeZoneMap.ContainsKey(Config.Instance.DefaultTimeZone))
                    {
                        time = time.Replace(tz, Config.Instance.TimeZoneMap[Config.Instance.DefaultTimeZone]);
                    }
                    else
                    {
                        //Just drop the time zone and use the local time
                        time = time.Replace(tz, "");
                    }
                }
            }

            return time.Trim();
        }

        /// <summary>
        /// Converts the number of seconds from midnight 1/1/1970 to a DateTime object
        /// </summary>
        /// <param name="timestamp">The number of seconds past midnight 1/1/1970</param>
        /// <returns>A DateTime object of the provided timestamp</returns>
        private static DateTime ConvertFromUnixTimestamp(long timestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp);
        }

        /// <summary>
        /// Converts a DateTime object to seconds past 1 Jan 1970
        /// </summary>
        /// <param name="date">The DateTime object to convert</param>
        /// <returns>The number of seconds past midnight 1/1/1970</returns>
        private static long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return (long)Math.Floor(diff.TotalSeconds);
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
    }
}
