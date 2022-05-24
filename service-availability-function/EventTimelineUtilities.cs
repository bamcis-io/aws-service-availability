using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// Provides the functions to extract start and end times 
    /// of service interruptions from an event's description
    /// </summary>
    public static class EventTimelineUtilities
    {
        #region Private Fields

        /// <summary>
        /// The months that are used in notifications with dates and times
        /// </summary>
        private static string months = "January|February|March|April|May|June|July|August|September|October|November|December";

        /// <summary>
        /// A standard, non-capturing timestamp string, will match 5:00, 13:00, 05:15, 6:20 PM, 12:10 AM EST
        /// </summary>
        private static string timeStamp = @$"(?:[0-1]?[0-9]|2[0-3]):[0-5][0-9](?:\s?(?:AM|PM))?(?:\s?(?:{String.Join("|", Config.Instance.TimeZoneMap.Select(x => x.Key))}))?";

        /// <summary>
        /// Captures the AM and PM from a timestamp like 12:10 AM or 3:53 PM
        /// </summary>
        private static Regex amOrPmRegex = new Regex($@"(?:[0-1]?[0-9]|2[0-3]):[0-5][0-9](?:\s?(AM|PM))?");

        /// <summary>
        /// Regex that divides the descriptions into <div> sections and captures the timestamp and text portion
        /// </summary>
        private static Regex splitInParts = new Regex(@"(?:<div[^>]*>)<span[^>]*>\s?(.*?)\s?</span>(.*?)(?:</div>)", RegexOptions.Singleline); // Use single line because the text may contain \r\n in it

        /// <summary>
        /// Some descriptions may not be boxed in a div, use this to split those
        /// </summary>
        private static Regex splitInPart2 = new Regex(@"<span[^>]*>\s?(.*?)\s?</span>(.*?)$");

        /// <summary>
        /// Used to evaluate if a string starts with a timestamp, this is useful to distinguish between 5:45 PM PDT and May 10, 5:45 PDT
        /// </summary>
        private static Regex timestampStartsWithTimeRegex = new Regex(@$"^\s?({timeStamp})");

        /// <summary>
        /// Captures the time zone text in a string
        /// </summary>
        private static Regex timeZoneRegex = new Regex(@$"\b({String.Join("|", Config.Instance.TimeZoneMap.Select(x => x.Key))})$");

        /*
         * This intends to capture these types of strings
        Between 5:10 PM on August 7th, and 3:50 AM PDT on August 8th,
        Between 5:39 PM and 5:49 PM PDT
        Between 7:26 AM and 7:34 AM PDT, and between 7:57 AM to 8:05 AM PDT, some 
        Between 7:00 AM and 8:15 AM PDT we exp
        Between 5:42 PM and 7:04 PM PDT, we
        Between 9:00 AM and 9:18 AM PST
        Between 09:00 and 9:18 AM PST
        Between 11:59 AM and 6:25 PM PST on November 10, 2020,
        we did observe two periods of impact, the first between 11:30 AM and 12:00 PM PST and the second between 1:00 PM and 1:45 PM PST.
         */
        private static Regex betweenXandYRegex = new Regex($@"\b[bB]etween\s+({timeStamp})(?:\son\s+(.*?),)?\s+(?:and|to)\s+({timeStamp})(?:\s+on\s+(.*?),)?"); // This has 4 capturing groups,
        // and could also match multiple times like example 3 and 8 above

        /*
         * This intends to caputre these types of strings
        Between January 29 9:12 PM and January 30 12:48 AM PST we experienced delays
        Between June 11 9:56 PM PDT and June 12 6:40 AM PDT, AWS IAM experienced
        Between October 8 10:35 PM and October 9 2:25 AM PDT we experienced
        Between October 13 8:45 PM and October 14 2:50 AM PDT, we
        Between November 25 5:15 AM PST and November 26 3:49 AM PST, we experienced 
        Between December 6, 2020 at 11:10 PM PST and December 7, 2020 at 5:45 AM PST
        Between June 4th at 10:25 PM PDT and June 5th at 12:45 AM PDT some VPN 
         */
        private static Regex betweenXandYWithDatesRegex = new Regex($@"\b[bB]etween\s+((?:{months})\s+(?:(?:3[0-1]|[1-2][0-9]|[0-9])(?:th|nd|st|rd)?),?(?:\s?\d{{4}})?(?:\s?at)?\s+{timeStamp})\s+and\s+((?:{months})\s+(?:(?:3[0-1]|[1-2][0-9]|[0-9])(?:th|nd|st|rd)?),?(?:\s?\d{{4}})?(?:\s?at)?\s+{timeStamp})"); // This has 2 capture groups

        /*
         * Captures start times like
         * Starting at 5:03 PM PDT, we
         * Starting at 9:37 PM PDT on October 8th,
         */
        private static Regex startingAt = new Regex($@"\b[sS]tarting\s+at\s+({timeStamp})(?:\s+on\s+(({months})\s+(3[0-1]|[1-2][0-9]|[0-9]))(?:th|nd|st|rd)?)?");

        /// <summary>
        /// Used to remove "st", "rd", "nd", "th" from dates like January 7th or October 23rd
        /// </summary>
        private static Regex removeDateOrdinalRegex = new Regex(@"\b(\d+)(?:st|nd|rd|th)\b");

        /// <summary>
        /// Used to determine if the description starts with a Month, so Jun 10, 5:45 PM PDT instead of just 5:45 PM PDT
        /// Make sure it is bounded so that
        /// </summary>
        private static Regex timestampStartsWithMonthRegex = new Regex(@"^<div><span[^>]*>(\s?[a-zA-Z]{3}.*?)?</span>");

        /// <summary>
        /// This is the expected format for the timestamps that start with a month
        /// </summary>
        private static string monthDayTimeFormat = "MMM d, h:mm tt zzz";

        #endregion

        #region Public Methods

        /// <summary>
        /// Converts the description field into a dictionary with timestamps as the keys and the text 
        /// description as the value. The dates are presented in UTC.
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="baseDate">The base date (date of posting) in UTC</param>
        /// <returns></returns>
        public static SortedDictionary<DateTime, EventUpdate> GetDatedUpdates(DashboardEventRaw ev, DateTime baseDate)
        {
            if (String.IsNullOrEmpty(ev.Description))
            {
                return new SortedDictionary<DateTime, EventUpdate>();
            }

            Dictionary<string, string> updates = SplitUpdates(ev.Description);

            SortedDictionary<DateTime, EventUpdate> datedUpdates = new SortedDictionary<DateTime, EventUpdate>();

            // Convert the base date to the same TZ as the other entries
            string tz = timeZoneRegex.Match(updates.First().Key).Value;
            DateTime baseDateInCorrectTZ = ConvertBaseDateFromUtc(baseDate, tz);
            string baseDateString = $"{baseDateInCorrectTZ.Year}-{baseDateInCorrectTZ.Month}-{baseDateInCorrectTZ.Day}";

            // All other updates will be relative to the base date
            foreach (KeyValuePair<string, string> item in updates)
            {
                Match match = timestampStartsWithTimeRegex.Match(item.Key);

                if (match.Success) // then was in h:mm tt zzz format, so we need to inject the year, month, day from the base date
                {
                    string wholeString = baseDateString + " " + ReplaceTimeZoneWithOffset(match.Groups[1].Value).Replace("  ", " "); // Replace any double spaces                                                                 

                    // If the last update was too late in the day, changing to universal time may roll forward a
                    // day, but by statically injecting from the base date, it wraps back around to the beginning
                    // of the day, so we

                    DateTime dt = DateTime.Parse(wholeString, CultureInfo.InvariantCulture).ToUniversalTime();

                    datedUpdates.Add(dt, new EventUpdate() { Update = item.Value, OriginalTimezone = tz, Timestamp = dt });
                }
                else // then it way in MMM d, h:mm tt zzz, so we need to inject the year from the base date 
                {
                    string wholeString = ReplaceTimeZoneWithOffset(item.Key).Replace("  ", " ").Replace(",", ""); // Replace any double spaces;
                    List<string> temp = wholeString.Split(" ").ToList();
                    temp.Insert(2, baseDateInCorrectTZ.Year.ToString());
                    string stringToParse = String.Join(" ", temp);
                    DateTime dt = DateTime.ParseExact(stringToParse, "MMM d yyyy h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite).ToUniversalTime();

                    datedUpdates.Add(dt, new EventUpdate() { Update = item.Value, OriginalTimezone = tz, Timestamp = dt });
                }
            }

            return datedUpdates;
        }

       

        /// <summary>
        /// Converts the description field into a dictionary with timestamps as the keys and the text 
        /// description as the value. The dates are presented in UTC.
        /// </summary>
        /// <param name="ev"></param>
        /// <returns></returns>
        public static SortedDictionary<DateTime, EventUpdate> GetDatedUpdates(DashboardEventRaw ev)
        {
            DateTime baseDate = GetBaseDate(ev);

            return GetDatedUpdates(ev, baseDate);
        }

        /// <summary>
        /// Gets the event timeline, which includes the set of updates, the start time,
        /// and the end time.
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="baseDate"></param>
        /// <returns></returns>
        public static EventTimeline GetEventTimeline(DashboardEventRaw ev, DateTime baseDate)
        {
            EventTimeline timeline = new EventTimeline();

            if (String.IsNullOrEmpty(ev.Description))
            {
                return timeline;
            }

            timeline.Updates = GetDatedUpdates(ev, baseDate);

            // Put base date into same TZ as other dates
            DateTime baseDateInCorrectTZ = ConvertBaseDateFromUtc(baseDate, timeline.Updates.First().Value.OriginalTimezone);

            string baseDateStringYearMonthDay = $"{baseDateInCorrectTZ.Year}-{baseDateInCorrectTZ.Month}-{baseDateInCorrectTZ.Day}";
            string baseDateStringMonthDayYear = $"{baseDateInCorrectTZ.Month}/{baseDateInCorrectTZ.Day}/{baseDateInCorrectTZ.Year}";

            bool found = false;

            foreach (KeyValuePair<DateTime, EventUpdate> item in timeline.Updates)
            {
                MatchCollection matchColl = betweenXandYRegex.Matches(item.Value.Update);
                Match match;

                if (matchColl.Count >= 1 && matchColl[0].Success) // Matches format like Between 9:00 AM and 9:18 AM PST, might be more than 1
                {
                    foreach (Match matchItem in matchColl.Where(x => x.Success))
                    {
                        string start = matchItem.Groups[1].Value; // Will pull out something like 5:10 PM
                        string end = matchItem.Groups[3].Value;   // Will pull out something like 7:04 PM PDT

                        DateTime startDt;
                        DateTime endDt;

                        // Add timezone to start, if not present
                        if (!timeZoneRegex.IsMatch(start))
                        {
                            Match tzMatch = timeZoneRegex.Match(end);

                            if (tzMatch.Success)
                            {
                                start += $" {tzMatch.Groups[1].Value}";
                            }
                            else
                            {
                                start += $" {Config.Instance.DefaultTimeZone}";
                            }
                        }

                        // Add timezone to end, if not present
                        if (!timeZoneRegex.IsMatch(end))
                        {
                            Match tzMatch = timeZoneRegex.Match(start);

                            if (tzMatch.Success)
                            {
                                end += $" {tzMatch.Groups[1].Value}";
                            }
                            else
                            {
                                end += $" {Config.Instance.DefaultTimeZone}";
                            }
                        }


                        // Date may have been provided for start and end, or just the end
                        // i.e.
                        // Between 5:10 PM on August 7th, and 3:50 AM PDT on August 8th,
                        // Between 11:59 AM and 6:25 PM PST on November 10, 2020,

                        // Do the end first, if it was successful, we'll use that day instead of
                        // the base date in case the start doesn't have a date
                        if (matchItem.Groups[4].Success)
                        {
                            string endDate = matchItem.Groups[4].Value;   // Will pull out something like August 8th
                            endDate = removeDateOrdinalRegex.Replace(endDate, "$1"); // Is now August 8

                            // construct parseable date string

                            // MMMM d yyyy h:mm tt zzz
                            end = ReplaceTimeZoneWithOffset($"{endDate} {baseDateInCorrectTZ.Year} {end}").Replace("  ", " "); // Replace any double spaces
                            endDt = DateTime.ParseExact(end, "MMMM d yyyy h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite);
                        }
                        else // There was no date provided, but the end date could still be the next day with the PST/PDT timezone (but not the next day
                        // in UTC) like Between 9:30 PM PST and 2:10 AM PST, this will be checked later after the start time is parsed
                        {
                            end = ReplaceTimeZoneWithOffset($"{baseDateStringMonthDayYear} {end}").Replace("  ", " "); // Replace any double spaces
                            endDt = DateTime.Parse(end);

                            // It's possible that the times may be something like Between 9:37 AM and 3:48 PST,
                            // this lacks an AM/PM, and the end time will be interpreted to be 3:48 AM, so make
                            // a check here to see if we need to add 12 hours to the time, we may also need to add 24 hours
                            // hours if the start is PM and the end is AM
                            if (DateTime.TryParse(ReplaceTimeZoneWithOffset($"{baseDateInCorrectTZ.ToString("MM/dd/yyyy")} {start}"), out DateTime temp1)
                                && temp1 > endDt)
                            {
                                Match amOrPm1 = amOrPmRegex.Match(start);
                                Match amOrPm2 = amOrPmRegex.Match(end);

                                // If we found AM or PM in both strings and the start was PM and end was AM
                                if (amOrPm1.Success && amOrPm2.Success && amOrPm1.Groups[1].Success && amOrPm2.Groups[1].Success &&
                                    amOrPm1.Groups[1].Value.Equals("PM") && amOrPm2.Groups[1].Value.Equals("AM"))
                                {
                                        // Move up the end by one day to make it correct
                                        endDt = endDt.AddDays(1);
                                }
                                else // The start wasn't PM and the end AM, so we interpret this to mean
                                // that the start is AM and the end is PM on the same day, move the end
                                // up 12 hours to put it in the right 24 hour format
                                {
                                    endDt = endDt.AddHours(12);
                                }
                            }
                        }

                        // If a date was provided for the start
                        if (matchItem.Groups[2].Success)
                        {
                            string startDate = matchItem.Groups[2].Value; // Will pull out something like August 7th
                            startDate = removeDateOrdinalRegex.Replace(startDate, "$1"); // Is now August 7

                            // construct parseable date string

                            // MMMM d yyyy h:mm tt zzz
                            start = ReplaceTimeZoneWithOffset($"{startDate} {baseDateInCorrectTZ.Year} {start}").Replace("  ", " "); // Replace any double spaces
                            startDt = DateTime.ParseExact(start, "MMMM d yyyy h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite);
                        }
                        else // start is only HH:mm tt zzz
                        {
                            if (matchItem.Groups[4].Success) // End had a day specified, use that instead of the base
                            {
                                // Have to make sure time zone differences don't affect this,
                                // If the end time is being tracked in GMT, but was originally PST
                                // the day may accidentially be a day later, so take the original string
                                // and build the DateTime struct again, instead of just using the day value
                                // from the endDt object
                                // MMMM d yyyy h:mm tt zzz
                                string endDate = matchItem.Groups[4].Value;   // Will pull out something like August 8th
                                endDate = removeDateOrdinalRegex.Replace(endDate, "$1"); // Is now August 8

                                start = ReplaceTimeZoneWithOffset($"{endDate} {baseDateInCorrectTZ.Year} {start}").Replace("  ", " "); // Replace any double spaces
                                startDt = DateTime.ParseExact(start, "MMMM d yyyy h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite);
                            }
                            else
                            {
                                start = ReplaceTimeZoneWithOffset($"{baseDateStringMonthDayYear} {start}").Replace("  ", " "); // Replace any double spaces
                                startDt = DateTime.Parse(start);
                            }
                        }

                        startDt = startDt.ToUniversalTime();
                        endDt = endDt.ToUniversalTime();

                        try
                        {
                            TimeInterval interval = new TimeInterval(startDt, endDt);
                            found = true;

                            if (!timeline.Intervals.ContainsKey(startDt))
                            {
                                timeline.Intervals.Add(startDt, interval);
                            }
                            else // Sometimes the message is repeated, so it might have a duplicate, check
                                 // to make sure the end time didn't change
                            {
                                DateTime originalEnd = timeline.Intervals[startDt].End;

                                if (endDt > originalEnd)
                                {
                                    timeline.Intervals[startDt] = interval;
                                }
                            }
                        }
                        catch (ArgumentOutOfRangeException e)
                        {
                            Console.WriteLine($"[ERROR] : Could not create time interval, {e.Message}.\r\b{ev.Description}");
                        }
                    }
                }
                else if ((match = betweenXandYWithDatesRegex.Match(item.Value.Update)).Success) // Might be a different format like Between January 29 9:12 PM and January 30 12:48 AM PST
                {
                    // March 27 11:20 PM
                    // June 11 9:56 PM PDT 
                    // December 6, 2020 at 11:10 PM PST
                    // Between September 13 11:55 PM and September 14 1:03 AM
                    string start = match.Groups[1].Value.Replace(" at ", " ").Replace("  ", " ").Replace(",", "");
                    start = removeDateOrdinalRegex.Replace(start, "$1");
                    string end = match.Groups[2].Value.Replace(" at ", " ").Replace("  ", " ").Replace(",", "");
                    end = removeDateOrdinalRegex.Replace(end, "$1");

                    if (!timeZoneRegex.IsMatch(start)) // Make sure start has a TZ as well
                    {
                        Match tzMatch = timeZoneRegex.Match(end);

                        if (tzMatch.Success) // If end has it, add TZ from there
                        {
                            string tz = tzMatch.Groups[1].Value;
                            start += $" {tz}";
                        }
                        else // end didn't have it, need to add TZ to both, use the update TZ
                        {
                            start += $" {item.Value.OriginalTimezone}";
                            end += $" {item.Value.OriginalTimezone}";
                        }
                    }

                    start = ReplaceTimeZoneWithOffset(start);
                    end = ReplaceTimeZoneWithOffset(end);

                    // Now all strings should be like June 11 9:56 PM -07:00 
                    // December 6 2020 11:10 PM -08:00
                    DateTime startDt;
                    DateTime endDt;

                    if (!DateTime.TryParseExact(start, "MMMM d h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out startDt))
                    {
                        startDt = DateTime.ParseExact(start, "MMMM d yyyy h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite);
                    }
                    else
                    {
                        // Parsed without a year, so we need to update to the base year
                        startDt = new DateTime(baseDateInCorrectTZ.Year, startDt.Month, startDt.Day, startDt.Hour, startDt.Minute, startDt.Second); // Don't set it to UTC
                    }

                    if (!DateTime.TryParseExact(end, "MMMM d h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out endDt))
                    {
                        endDt = DateTime.ParseExact(end, "MMMM d yyyy h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite);
                    }
                    else
                    {
                        // Parsed without a year, so we need to update to the base year
                        endDt = new DateTime(baseDateInCorrectTZ.Year, endDt.Month, endDt.Day, endDt.Hour, endDt.Minute, endDt.Second); // Don't set it to UTC
                    }

                    found = true;
                    timeline.Intervals.Add(startDt, new TimeInterval(startDt.ToUniversalTime(), endDt.ToUniversalTime()));
                }
                else if ((match = startingAt.Match(item.Value.Update)).Success) // It's possible we find this more than once
                {
                    // Starting at 12:15 AM PDT
                    string start = match.Groups[1].Value;

                    if (!timeZoneRegex.IsMatch(start)) // Make sure start has a TZ as well
                    {
                        // start += $" {item.Value.OriginalTimezone}"; Not sure the item will have original timezone, need
                        // to look into why i'm using the default in the first if, but the original in the second else if
                        start += $" {Config.Instance.DefaultTimeZone}";
                    }

                    if (match.Groups[2].Success) // There was a month and day
                    {
                        string month = match.Groups[3].Value;
                        string day = match.Groups[4].Value;

                        start = ReplaceTimeZoneWithOffset($"{month} {day} {baseDateInCorrectTZ.Year} {start}");
                    }
                    else
                    {
                        start = ReplaceTimeZoneWithOffset($"{baseDateStringMonthDayYear} {start}").Replace("  ", " "); // Replace any double spaces
                    }
                    
                    DateTime startDt = DateTime.Parse(start).ToUniversalTime();

                    if (!timeline.Intervals.ContainsKey(startDt))
                    {
                        timeline.Intervals.Add(startDt, new TimeInterval(startDt.ToUniversalTime(), timeline.Updates.Last().Key));
                    }

                    found = true;
                }
            }

            if (found)
            {
                timeline.StartTimeWasFoundInDescription = true;
                timeline.EndTimeWasFoundInDescription = true;
                timeline.Start = timeline.Intervals.First().Value.Start;
                timeline.End = timeline.Intervals.Last().Value.End;
            }
            else
            { 
                if (timeline.Updates.Any())
                {
                    timeline.Start = timeline.Updates.First().Key;
                    timeline.End = timeline.Updates.Last().Key;
                    timeline.Intervals.Add(timeline.Start, new TimeInterval(timeline.Start, timeline.End));
                }
                else
                {
                    timeline.Start = baseDate;
                    timeline.End = baseDate;
                    timeline.Intervals.Add(timeline.Start, new TimeInterval(timeline.Start, timeline.End));
                }
            }

            return timeline;
        }

        private static KeyValuePair<DateTime, TimeInterval> GetStartAtDate(string update)
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the event timeline, which includes the set of updates, the start time,
        /// and the end time.
        /// </summary>
        /// <param name="ev"></param>
        /// <returns></returns>
        public static EventTimeline GetEventTimeline(DashboardEventRaw ev)
        {
            DateTime baseDate = GetBaseDate(ev);

            return GetEventTimeline(ev, baseDate);
        }

        /// <summary>
        /// Provides a DateTime of the event posting in UTC/GMT.
        /// </summary>
        /// <param name="ev"></param>
        /// <returns></returns>
        public static DateTime GetBaseDate(DashboardEventRaw ev)
        {
            return ServiceUtilities.ConvertFromUnixTimestamp(Int64.Parse(ev.Date));

            /* This shouldn't be necessary, even if
             * this happens, we still capture the
             * update dates correctly and can still identify
             * the correct start and end, we just need something
             * to be relative to for non-dated updates, and they
             * are relative to the base date, not the first
             * update
             * 
            if (String.IsNullOrEmpty(ev.Description))
            {
                return date;
            }

            
            Match match = timestampStartsWithMonthRegex.Match(ev.Description);

            // If it  matches, then it wasn't just a time provided,
            // for example:
            //<div><span class="yellowfg">May 10, 11:21 AM PDT</span>
            //<div><span class="yellowfg">Oct 7, 7:00 PM PDT</span>
            //<div><span class="yellowfg">Nov 28, 12:05 AM PST</span>
            // This happens for this event, 1557569326, which is a timestamp for 5/11/2019 10:08:46 GMT, so the first update
            // is before

            if (match.Success)
            {
                string str = ReplaceTimeZoneWithOffset(match.Groups[1].Value.Replace("  ", " ").Replace(",", "")); // Gets the full date: May 10 11:21 AM -08:00
                List<string> temp = str.Split(" ").ToList();
                temp.Insert(2, date.Year.ToString());
                str = String.Join(" ", temp);
                date = DateTime.ParseExact(str, "MMM dd yyyy h:mm tt zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite).ToUniversalTime();
            }

            return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
            */
        }

        /// <summary>
        /// Converts the base date, which is expected in UTC, to the specified
        /// time zone. If the date time is in a different TZ, it is returned
        /// as is without conversion.
        /// </summary>
        /// <param name="baseDate">The base date in UTC</param>
        /// <param name="tz">A timezone abbreviation like PDT or EST</param>
        /// <returns></returns>
        public static DateTime ConvertBaseDateFromUtc(DateTime baseDate, string tz)
        {
            if (baseDate.Kind == DateTimeKind.Utc)
            {
                return baseDate.AddHours(Config.Instance.TimeZoneAbbreviationMap[tz]);
            }
            else
            {
                return baseDate;
            }
        }

        /// <summary>
        /// Provide a string with a time zone at the end of the string,
        /// like May 10, 5:45 PM PST, this will replace the time zone with
        /// the correct GMT offset
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ReplaceTimeZoneWithOffset(string input)
        {
            //Convert PDT, PST type time zones to GMT offset
            Match timezoneMatch = timeZoneRegex.Match(input.Trim());

            //If the time has a time zone, replace it with the offset from the timezone map object
            if (timezoneMatch.Success)
            {
                string tz = timezoneMatch.Groups[1].Value;

                input = input.Replace(tz, Config.Instance.TimeZoneMap[tz]).Trim();
            }

            return input;
        }

        /// <summary>
        /// Takes the description field and splits each div section into a time string and the corresponding description
        /// </summary>
        /// <param name="description"></param>
        /// <returns></returns>
        public static Dictionary<string, string> SplitUpdates(string description)
        {
            Dictionary<string, string> updates = new Dictionary<string, string>();

            if (String.IsNullOrEmpty(description))
            {
                return updates;
            }

            MatchCollection coll = splitInParts.Matches(description);

            if (coll.Count > 0)
            {
                foreach (Match match in coll)
                {
                    if (match.Groups.Count < 3)
                    {
                        throw new FormatException("The description was not in the expected format, did not find 2 capture groups in a match:\r\n" + description);
                    }

                    string time = match.Groups[1].Value.Trim();
                    string update = match.Groups[2].Value.Replace("&nbsp;", "").Replace("\r\n", "");

                    updates.Add(time, update);
                }
            }
            else
            {
                // Might not have <div> elements, will just have one update
                Match match = splitInPart2.Match(description);

                if (match.Success)
                {
                    string time = match.Groups[1].Value.Trim();
                    string update = match.Groups[2].Value.Replace("&nbsp;", "").Replace("\r\n", "");

                    updates.Add(time, update);
                }
            }


            return updates;
        }

        #endregion
    }
}
