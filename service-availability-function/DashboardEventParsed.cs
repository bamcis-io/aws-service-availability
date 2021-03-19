using Amazon.DynamoDBv2.DataModel;
using BAMCIS.ServiceAvailability.DDBConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// The parsed data entry item with begin and end times
    /// extracted from the description
    /// </summary>
    [DynamoDBTable("ServiceHealthDashboardData", LowerCamelCaseProperties = true)]
    public class DashboardEventParsed
    {
        #region Public Properties

        [JsonIgnore]
        [DynamoDBHashKey]
        public string Id { 
            get
            {
                return $"{this.Service}::{this.Region}";
            }
            private set
            {

            }
        }

        /// <summary>
        /// The friendly service name
        /// </summary>
        [DynamoDBProperty]
        public string Service { get; set; }

        /// <summary>
        /// A unix timestamp in seconds of when the event was posted
        /// </summary>
        [DynamoDBRangeKey(typeof(TimestampConverter))]
        public long Date { get; set; }

        /// <summary>
        /// The unix timestamp in seconds of the time the event began
        /// </summary>
        [DynamoDBProperty(typeof(TimestampConverter))]
        public long Start { get; set; }

        /// <summary>
        /// The unix timestamp in seconds of the time the event ended
        /// </summary>
        [DynamoDBProperty(typeof(TimestampConverter))]
        public long End { get; set; }

        /// <summary>
        /// The elapsed time in seconds of the event
        /// </summary>
        [DynamoDBProperty]
        public long EventDuration
        {
            get
            {
                return this.Timeline != null ? this.Timeline.GetEventDurationInSeconds() : 0;
            }
            private set
            { }
        }

        /// <summary>
        /// The region the event occured in
        /// </summary>
        [DynamoDBProperty]
        public string Region { get; set; }

        /// <summary>
        /// The description of the event and updates to the status
        /// </summary>
        [DynamoDBProperty]
        public string Description { get; set; }

        /// <summary>
        /// The summary of the event
        /// </summary>
        [DynamoDBProperty]
        public string Summary { get; set; }

        /// <summary>
        /// The event status
        /// </summary>
        [DynamoDBProperty(typeof(EnumConverter<DashboardEventStatus>))]
        public DashboardEventStatus Status { get; set; }

        /// <summary>
        /// A dictionary where the key is year-month (YYYY-mm) of outage and the value is the number
        /// of seconds this outage lasted during that YYYY-mm. The outage might span more than 1 month, or
        /// more than 1 year
        /// </summary>
        [DynamoDBProperty(typeof(DictionaryConverter<string, long>))]
        public Dictionary<string, long> MonthlyOutageDurations
        {
            get
            {
                return this.GetMonthlyOutageDurations();
            }
            private set { }
        }
        

        /// <summary>
        /// The timeline of the event
        /// </summary>
        [DynamoDBProperty(typeof(TimelineConverter))]
        public EventTimeline Timeline { get; set; }

        #endregion

        #region Constructors

        public DashboardEventParsed() { }

        #endregion

        #region Public Methods

        public static DashboardEventParsed FromRawEvent(DashboardEventRaw rawEvent)
        {
            try
            {
                DashboardEventParsed parsed = new DashboardEventParsed();
                parsed.Service = rawEvent.GetServiceShortName();
                parsed.Region = rawEvent.GetRegion();
                parsed.Timeline = EventTimelineUtilities.GetEventTimeline(rawEvent);

                if (Int32.TryParse(rawEvent.Status, out int status))
                {
                    parsed.Status = (DashboardEventStatus)status;
                }
                else
                {
                    parsed.Status = 0;
                }

                if (parsed.Region.Equals("global"))
                {
                    // If the returned region was global, it means the listed service name
                    // didn't contain a region element, like ec2-us-east-1, but it may not
                    // be an expected global service, like resourcegroups
                    if (!Config.Instance.GlobalServices.Contains(parsed.Service))
                    {
                        parsed.Region = parsed.Service;
                    }
                }

                parsed.Description = parsed.GetDescriptionStringFromUpdates();
                parsed.Summary = rawEvent.Summary;
                parsed.Date = Int64.Parse(rawEvent.Date);

                parsed.Start = ServiceUtilities.ConvertToUnixTimestamp(parsed.Timeline.Start);
                parsed.End = ServiceUtilities.ConvertToUnixTimestamp(parsed.Timeline.End);

                return parsed;
            }
            catch (Exception e)
            {
                throw new Exception($"Could not parse event:\r\n{JsonConvert.SerializeObject(rawEvent)}", e);
            }
        }

        /// <summary>
        /// Converts the dictionary of updates to a single string
        /// </summary>
        /// <returns></returns>
        public string GetDescriptionStringFromUpdates()
        {
            return String.Join("\r\n", this.Timeline.Updates.Select(x => $"{x.Key} : {x.Value.Update}"));
        }

        #endregion

        #region Private Methods

        private Dictionary<string, long> GetMonthlyOutageDurations()
        {
            Dictionary<string, long> downtimes = new Dictionary<string, long>();

            DateTime start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(this.Start);
            DateTime end = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(this.End);

            double totaltime = (end - start).TotalSeconds;

            DateTime current = start;

            while (totaltime > 0)
            {
                string key = $"{current.Year}-{current.Month.ToString("D2")}";
                DateTime endOfMonth = new DateTime(current.Year, current.Month, DateTime.DaysInMonth(current.Year, current.Month), 23, 59, 59, DateTimeKind.Utc);

                // Since the endOfMonth is 23:59:59, there's actually 1 more second in that month
                double remainingTimeInMonth = (endOfMonth - current).TotalSeconds + 1;

                if (remainingTimeInMonth > totaltime)
                {
                    downtimes.Add(key, (long)Math.Round(totaltime));
                }
                else
                {
                    downtimes.Add(key, (long)Math.Round(remainingTimeInMonth));
                }

                totaltime = totaltime - remainingTimeInMonth;

                // Move to the first day of the next month
                current = current.AddMonths(1);
                current = new DateTime(current.Year, current.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            return downtimes;
        }

        /// <summary>
        /// Gets the beginning and end time from an outage description
        /// </summary>
        /// <param name="match">The regex match of the description</param>
        /// <param name="defaultDate">The default date provided with the outage item</param>
        /// <param name="description">The original description</param>
        /// <returns>An Tuple with a begin DateTime and an end DateTime</returns>   
        private static Tuple<DateTime, DateTime> GetBeginEnd(Match match, DateTime defaultDate, string description)
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
                        Console.WriteLine($"There is no end slash date, using beginning {date1} : {description}.");
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
                    throw new ExtractBeginEndFailureException("Could not parse begin date to DateTime.", $"{date1} {time1}", e);
                }

                try
                {
                    end = DateTime.Parse($"{date2} {time2}").ToUniversalTime();
                }
                catch (Exception e)
                {
                    throw new ExtractBeginEndFailureException("Could not parse end date to DateTime.", $"{date2} {time2}", e);
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
                        throw new ExtractBeginEndFailureException("The End date resulted in being earlier than the start date although an AM/PM indicator was present.", description);
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
                            throw new ExtractBeginEndFailureException("Could not parse date to DateTime", time, e);
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
                    throw new ExtractBeginEndFailureException("Did not match any regex to extract the begin and end times.", description);
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

        #endregion
    }
}
