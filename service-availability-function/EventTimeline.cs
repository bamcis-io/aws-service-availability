using System;
using System.Collections.Generic;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// Represents the timeline of events extracted from the event's 
    /// description field
    /// </summary>
    public class EventTimeline
    {
        #region Public Properties

        /// <summary>
        /// The ordered, by time, set of updates provided about the event
        /// </summary>
        public SortedDictionary<DateTime, EventUpdate> Updates { get; set; }

        /// <summary>
        /// The start time of the event, if it could be extracted, otherwise
        /// it is the earliest update
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// The end time of the event, if it could be extracted, otherwise
        /// it is the last update
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// The set of service interruptions during the event
        /// </summary>
        public SortedList<DateTime, TimeInterval> Intervals { get; set; }

        /// <summary>
        /// Used to indicate that the start time was extracted from the 
        /// description, if this is false, the start time represents
        /// the first update made
        /// </summary>
        public bool StartTimeWasFoundInDescription { get; set; }

        /// <summary>
        /// Used to indicate that the end time was extracted from the 
        /// description, if this is false, the start time represents
        /// the first update made
        /// </summary>
        public bool EndTimeWasFoundInDescription {get; set;}

        #endregion

        #region Constructors

        public EventTimeline()
        {
            this.Updates = new SortedDictionary<DateTime, EventUpdate>();
            this.Intervals = new SortedList<DateTime, TimeInterval>();
            this.StartTimeWasFoundInDescription = false;
            this.EndTimeWasFoundInDescription = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the amount of time, in seconds that
        /// the event lasted, this is the sum of time
        /// in the intervals, not necessarily the entire
        /// time between the start and end time.
        /// </summary>
        /// <returns></returns>
        public long GetEventDurationInSeconds()
        {
            long time = 0;

            foreach (TimeInterval item in this.Intervals.Values)
            {
                if (item != null)
                {
                    time += item.GetTimeIntervalInSeconds();
                }
                else
                {
                    throw new ArgumentNullException($"One of the TimeIntervals in the EventTimeline was null.");
                }
            }

            return time;
        }

        #endregion
    }
}
