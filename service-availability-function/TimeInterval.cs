using System;
using System.Collections.Generic;
using System.Text;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// Represents a start and end time for an event
    /// </summary>
    public class TimeInterval
    {
        #region Private Fields

        private TimeSpan _interval;

        #endregion

        #region Public Properties

        /// <summary>
        /// The start time of the interval
        /// </summary>
        public DateTime Start { get; private set; }

        /// <summary>
        /// The end time of the interval
        /// </summary>
        public DateTime End { get; private set; }

        #endregion

        #region Constructors

        public TimeInterval(DateTime start, DateTime end)
        {
            if (end < start) { throw new ArgumentOutOfRangeException("end", $"The end time, {end}, was before the start time, {start}."); }
            this.Start = start;
            this.End = end;
            this._interval = end - start;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the total seconds in the interval
        /// </summary>
        /// <returns></returns>
        public long GetTimeIntervalInSeconds()
        {
            return (long)Math.Floor(this._interval.TotalSeconds);
        }

        /// <summary>
        /// Gets the total milliseconds in the interval
        /// </summary>
        /// <returns></returns>
        public long GetTimeIntervalInMilliseconds()
        {
            return (long)Math.Floor(_interval.TotalMilliseconds);
        }

        #endregion
    }
}
