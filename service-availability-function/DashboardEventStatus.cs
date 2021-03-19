using System;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// The status of the event
    /// </summary>
    public enum DashboardEventStatus : Int32
    {
        /// <summary>
        /// Service is operating normally
        /// </summary>
        GREEN = 0,

        /// <summary>
        /// Informational message
        /// </summary>
        BLUE = 1,

        /// <summary>
        /// Service degradation
        /// </summary>
        YELLOW = 2,

        /// <summary>
        /// Service disruption
        /// </summary>
        RED = 3
    }
}
