using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// A dynamic configuration for the class that uses a config.json file
    /// </summary>
    public class Config
    {
        #region Public Constants

        public static readonly string COMBINED = "Combined";
        public static readonly string TIMEZONE = "TimeZone";
        public static readonly string PERIODIC = "Periodic";
        public static readonly string REGION = "Region";

        #endregion

        #region Private Fields

        /// <summary>
        /// The singleton instance of the config
        /// </summary>
        private static volatile Config _instance;

        /// <summary>
        /// An object to conduct blocking on the singleton to make
        /// it thread-safe for multi-threaded environments
        /// </summary>
        private static object _syncRoot = new Object();

        #region Default Values

        /// <summary>
        /// The default Url to get the data from
        /// </summary>
        private static readonly string _DEFAULT_URL = "http://status.aws.amazon.com/data.json";

        /// <summary>
        /// The default map of time zones to their UTC offset
        /// </summary>
        private static readonly Dictionary<string, string> _DEFAULT_TIME_ZONE_MAP = new Dictionary<string, string>()
        {
            {"HAST", "-10:00" },
            {"HADT", "-09:00" },
            {"AKST", "-09:00" },
            {"AKDT", "-08:00" },
            {"PST", "-08:00" },
            {"PDT", "-07:00" },
            {"MST", "-07:00" },
            {"MDT", "-06:00" },
            {"CST", "-06:00" },
            {"CDT", "-05:00" },
            {"EST", "-05:00" },
            {"EDT", "-04:00" },
            {"GMT", "-00:00" },
            {"UTC", "-00:00" }
        };

        /// <summary>
        /// The regex to match a time zone in a description
        /// </summary>
        private static readonly string _DEFAULT_TIME_ZONE_REGEX_STR = $@"(?:\b(?:{String.Join("|", _DEFAULT_TIME_ZONE_MAP.Select(x => x.Key))})\b)";

        /// <summary>
        /// The regex to extract the region from the description for pseudo-global services.
        /// </summary>
        private static readonly string _DEFAULT_REGION_IN_DESCRIPTION_REGEX = "^.*?in\\s+the\\s+([a-zA-Z]{2}-[a-zA-Z]{1,}-[0-9]{1,})\\s+Region.*$";

        /// <summary>
        /// The default time zone used
        /// </summary>
        private static readonly string _DEFAULT_TIME_ZONE = "PDT";

        /// <summary>
        /// These services do not have a region specified
        /// </summary>
        private static readonly List<string> _DEFAULT_GLOBAL_SERVICES = new List<string>() { "awswaf", "billingconsole", "chatbot", "chime", "cloudfront", "fps", "globalaccelerator", "health", "iam", "import-export", "interregionvpcpeering", "management-console", "marketplace", "organizations", "route53", "route53domainregistration", "spencer" /*ecr public*/, "supportcenter", "trustedadvisor" };
        private static readonly List<string> _DEFAULT_SERVICES_WITHOUT_REGION = new List<string>() { "resourcegroups" };


        #endregion

        #region New Combined Defaults

        /// <summary>
        /// Matches calendar dates like Sept 15th or March 9
        /// </summary>
        private static readonly string _DEFAULT_CALENDAR = @"([a-zA-Z]+\s+(?:[1-2][0-9]|3[0-1]|[1-9])(?:th|st|nd|rd)?)"; //{0}

        /// <summary>
        /// Matches times like 11:29 PM or 10:11 PM PDT or 02:31 or 00:31:29 PM GMT
        /// </summary>
        private static readonly string _DEFAULT_TIME = $@"([0-9]{{1,2}}\:[0-5][0-9](?:\:[0-9]{{2}})?(?:\s*(?:AM|PM))?(?:\s+{_DEFAULT_TIME_ZONE_REGEX_STR})?)"; //{1}

        /// <summary>
        /// Matches dates like 7/30 or 8.27
        /// </summary>
        private static readonly string _DEFAULT_SLASH_DATE = @"((?:1[0-2]|[1-9])(?:\/|\.)(?:[1-2][0-9]|3[0-1]|[1-9]))"; //{2}

        /*
         * Group 1 = Calendar date like Sept 14th or March 9
         * Group 2 = Time like 11:29 PM or 10:11 PM PDT or 02:31
         * Group 3 = Slash date like 7/30 or 8/27
         * Group 4 = Group 1
         * Group 5 = Group 2
         * Group 6 = Group 3
         */
        /// <summary>
        /// String representation of the regex that will match all of the AWS descriptions for how they describe
        /// outage time
        /// </summary>
        private static readonly string _DEFAULT_SINGLE_REGEX_STR =
            $@"(?:Between|From)\s+(?:{_DEFAULT_CALENDAR},?(?:\s+at)?)?\s*{_DEFAULT_TIME}(?:\s+on\s+{_DEFAULT_SLASH_DATE})?(?:\s+(?:and|on|to)\s+)(?:{_DEFAULT_CALENDAR},?(?:\s+at)?)?\s*{_DEFAULT_TIME}(?:\s+on\s+{_DEFAULT_SLASH_DATE})?";

        /// <summary>
        /// The regex string to match periodic update notices in the description
        /// </summary>
        private static readonly string _DEFAULT_PERIODIC_UPDATE_STR = $@"<span.*?>\s*(?:{_DEFAULT_CALENDAR},?\s*)?{_DEFAULT_TIME}\s*<\/span>";

        #endregion

        #region Old Defaults - NOT USED

        /*
        * MATCHES ALL OF THESE
        * 
        * Between 02:55 AM PDT and 03:35 AM PDT
        * Between 1:31 PM and 1:45 PM PDT
        * Between 9:18 AM PDT and 9:38 AM PDT
        * Between 02:31 and 07:30 PDT
        * Between 2:34 PDT and 8:29 PDT
        *
        * From 1:40 AM to 4:25 AM PST
        * From 1:40 AM to 4:25 AM PST
        * From 5:10 PM PDT to 9:45 PM PDT
        * From 5:30 PM PDT to 9:45 PM PDT
        *
        * Between 11:40 PM on 8/27 and 2:05 AM on 8/28 PDT
        * Between 10:11 PM on 7/30 and 12:14 AM PDT on 7/31
        * Between 10:12 PM PDT on 7/30 and 12:50 AM PDT on 7/31
       */
        /// <summary>
        /// The default regex to match most time and date statements
        /// </summary>
        private static readonly Regex _DEFAULT_SERVICE_TIME_AND_DATE_REGEX =
            new Regex($@"(?:From|Between)\s({_DEFAULT_TIME}{_DEFAULT_CALENDAR}\s(?:and|to)\s{_DEFAULT_TIME}{_DEFAULT_CALENDAR}\s?{_DEFAULT_TIME_ZONE_REGEX_STR}?)",
                RegexOptions.IgnoreCase
            );


        /*
        * MATCHES BOTH THESE TYPES
        * Between Sept 14th 4:09 PM and Sept 15 2:16 AM PDT
        * Between March 9 11:29 PM and March 10 1:46 AM PST
        * Between June 4th at 10:25 PM PDT and June 5th at 03:43 AM PDT
        */
        /// <summary>
        /// The default regex string to match a month, day, time statement
        /// </summary>
        private static readonly string _DEFAULT_STRING_DATE_PART = @"(?:.+?\s(?:[1-9]|[1-2][0-9]|3[0-1])(?:th|st|nd|rd)?(?:\sat)?)";
        /// <summary>
        /// The default regex to match month, day, and time statements
        /// </summary>
        private static readonly Regex _DEFAULT_SERVICE_DATE_AND_TIME_REGEX =
            new Regex($@"(?:From|Between)\s({_DEFAULT_STRING_DATE_PART}\s{_DEFAULT_TIME}\s(?:and|to)\s{_DEFAULT_STRING_DATE_PART}\s{_DEFAULT_TIME})", RegexOptions.IgnoreCase);


        #endregion

        #endregion

        #region Public Properties

        /// <summary>
        /// The url to the service availability data
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// The SNS topic used to notify an admin when a regex doesn't match
        /// </summary>
        public string SNSTopic { get; set; }

        /// <summary>
        /// The default time zone to use with times
        /// </summary>
        public string DefaultTimeZone { get; set; }

        /// <summary>
        /// A list of regex's to use to match the data
        /// </summary>
        public Dictionary<string, RegexItem> Regex { get; set; }

        /// <summary>
        /// A list of services that are not region specific
        /// </summary>
        public List<string> GlobalServices { get; set; }

        /// <summary>
        /// Some services, like resourcegroups, don't have a region indicated
        /// in their name, but are regional
        /// </summary>
        public List<string> ServicesWithoutRegion { get; set; }

        /// <summary>
        /// A map of timezones to GMT offsets
        /// </summary>
        public Dictionary<string, string> TimeZoneMap { get; set; }
        
        /// <summary>
        /// When the singleton is accessed the first time, it reads the config file
        /// and loads it as a new object to the private instance
        /// </summary>
        public static Config Instance
        {
            get
            {
                if (_instance == null)
                {
                    
                    if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConfigBucket")) && 
                        !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConfigKey")))
                    {
                        Task<Config> task = ReadConfig(Environment.GetEnvironmentVariable("ConfigBucket"), Environment.GetEnvironmentVariable("ConfigKey"));

                        lock (_syncRoot)
                        {
                            if (_instance == null)
                            {
                                task.Wait();
                                _instance = task.Result;
                                SetDefaults();
                            }
                        }
                    }
                    else
                    {
                        _instance = new Config();
                        SetDefaults();
                    }
                }

                return _instance;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Private constructor for the JSON convert to use
        /// </summary>
        private Config()
        {
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Evaluates what was read in from the config file and sets
        /// any defaults that are needed
        /// </summary>
        private static void SetDefaults()
        {
            if (String.IsNullOrEmpty(_instance.Url))
            {
                _instance.Url = _DEFAULT_URL;
            }

            if (_instance.GlobalServices == null || _instance.GlobalServices.Count == 0)
            {
                _instance.GlobalServices = _DEFAULT_GLOBAL_SERVICES;
            }

            if (_instance.ServicesWithoutRegion == null || _instance.GlobalServices.Count == 0)
            {
                _instance.ServicesWithoutRegion = _DEFAULT_SERVICES_WITHOUT_REGION;
            }

            if (String.IsNullOrEmpty(_instance.DefaultTimeZone))
            {
                _instance.DefaultTimeZone = _DEFAULT_TIME_ZONE;
            }

            if (_instance.Regex == null || _instance.Regex.Count == 0)
            {
                _instance.Regex = new Dictionary<string, RegexItem>(){
                    { COMBINED, new RegexItem(_DEFAULT_SINGLE_REGEX_STR, "i")},
                    { TIMEZONE, new RegexItem($"({_DEFAULT_TIME_ZONE_REGEX_STR})", "i") },
                    { PERIODIC, new RegexItem(_DEFAULT_PERIODIC_UPDATE_STR, "i") },
                    { REGION, new RegexItem(_DEFAULT_REGION_IN_DESCRIPTION_REGEX, "i")  }
                };
            }

            if (_instance.TimeZoneMap == null)
            {
                _instance.TimeZoneMap = _DEFAULT_TIME_ZONE_MAP;
            }

            if (String.IsNullOrEmpty(_instance.SNSTopic))
            {
                _instance.SNSTopic = Environment.GetEnvironmentVariable("SNSTopic");
            }
        }

        /// <summary>
        /// Reads the configuration file from a local file or url
        /// </summary>
        /// <param name="filePath">The path to the configuration file, this can be a url or local path</param>
        /// <returns>
        /// A new config object is returned if the config file
        /// doesn't exist, or a new object with the values specified 
        /// in the config file if it does exist
        /// </returns>
        private static async Task<Config> ReadConfig(string filePath)
        {
            Stream stream = null;

            if (Uri.TryCreate(filePath, UriKind.Absolute, out Uri temp))
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    try
                    {
                        stream = await httpClient.GetStreamAsync(temp);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"[ERROR] : Error retrieving confile file from {filePath} : {e.Message}");
                    }
                }
            }
            else
            {
                if (File.Exists(filePath))
                {
                    stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                }
            }

            using (stream)
            {
                return ReadConfig(stream);
            }
        }

        /// <summary>
        /// Reads a config file from an S3 location
        /// </summary>
        /// <param name="bucket">The S3 bucket</param>
        /// <param name="key">The key of the S3 object</param>
        /// <returns></returns>
        private static async Task<Config> ReadConfig(string bucket, string key)
        {
            using (IAmazonS3 client = new AmazonS3Client())
            {
                using (GetObjectResponse response = await client.GetObjectAsync(bucket, key))
                {
                    if ((int)response.HttpStatusCode >= 200 && (int)response.HttpStatusCode <= 299)
                    {
                        using (response.ResponseStream)
                        {
                            return ReadConfig(response.ResponseStream);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ERROR] : Error retrieving config file from S3: {(int)response.HttpStatusCode} {response.HttpStatusCode}");
                        return new Config();
                    }
                }
            }
        }

        /// <summary>
        /// Reads the config data from an open stream. The caller must dispose the provided stream.
        /// </summary>
        /// <param name="stream">The stream to read the config text from.</param>
        /// <returns>A Config object parsed from the stream, or a new Config object is the stream is null or encountered a problem parsing the config text</returns>
        private static Config ReadConfig(Stream stream)
        {
            if (stream != null)
            {
                using (stream)
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        try
                        {
                            return JsonConvert.DeserializeObject<Config>(reader.ReadToEnd());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"[ERROR] : Could not parse config from stream: {JsonConvert.SerializeObject(e)}");
                            return new Config();
                        }
                    }
                }
            }
            else
            {
                return new Config();
            }
        }

        #endregion
    }
}
