using System;
using System.Collections.Generic;
using System.Text;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// Represents an event update in the description
    /// </summary>
    public class EventUpdate
    {
        #region Public Properties

        /// <summary>
        /// The text of the update description
        /// </summary>
        public string Update { get; set; }

        /// <summary>
        /// The original timezone of the timestamp
        /// </summary>
        public string OriginalTimezone { get; set; }

        /// <summary>
        /// The UTC converted timestamp of the update
        /// </summary>
        public DateTime Timestamp { get; set; }

        #endregion

        #region Public Methods


        #endregion
    }
}
