using System;
using System.Collections.Generic;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// The parsed data entry item with begin and end times
    /// extracted from the description
    /// </summary>
    public class SLAResponse
    {
        /// <summary>
        /// The friendly service name
        /// </summary>
        public string Service { get; set; }

        /// <summary>
        /// A unix timestamp in seconds of when the event was posted
        /// </summary>
        public long Date { get; set; }

        /// <summary>
        /// The unix timestamp in seconds of the time the event began
        /// </summary>
        public long Began { get; set; }

        /// <summary>
        /// The unix timestamp in seconds of the time the event ended
        /// </summary>
        public long Ended { get; set; }

        /// <summary>
        /// The elapsed time in seconds of the event
        /// </summary>
        public long ElapsedTime
        {
            get
            {
                return this.Ended - this.Began;
            }
        }

        /// <summary>
        /// The region the event occured in
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// The description of the event and updates to the status
        /// </summary>
        public string Description { get; set;}

        /// <summary>
        /// The summary of the event
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// A dictionary where the key is year-month (YYYY-mm) of outage and the value is the number
        /// of seconds this outage lasted during that YYYY-mm. The outage might span more than 1 month, or
        /// more than 1 year
        /// </summary>
        public Dictionary<string, long> MonthlyOutageDurations
        {
            get
            {
                return this.GetMonthlyOutageDurations();
            }
        }

        private Dictionary<string, long> GetMonthlyOutageDurations()
        {
            Dictionary<string, long> Downtimes = new Dictionary<string, long>();

            DateTime Start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(this.Began);
            DateTime End = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(this.Ended);

            double Totaltime = (End - Start).TotalSeconds;

            DateTime Current = Start;

            while (Totaltime > 0)
            {
                string Key = $"{Current.Year}-{Current.Month.ToString("D2")}";
                DateTime EndOfMonth = new DateTime(Current.Year, Current.Month, DateTime.DaysInMonth(Current.Year, Current.Month), 23, 59, 59, DateTimeKind.Utc);

                // Since the EndOfMonth is 23:59:59, there's actually 1 more second in that month
                double RemainingTimeInMonth = (EndOfMonth - Current).TotalSeconds + 1;

                if (RemainingTimeInMonth > Totaltime)
                {
                    Downtimes.Add(Key, (long)Math.Round(Totaltime));
                }
                else
                {
                    Downtimes.Add(Key, (long)Math.Round(RemainingTimeInMonth));
                }

                Totaltime = Totaltime - RemainingTimeInMonth;

                // Move to the first day of the next month
                Current = Current.AddMonths(1);
                Current = new DateTime(Current.Year, Current.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            return Downtimes;
        }
    }
}
