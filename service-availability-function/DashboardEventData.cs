using System.Collections.Generic;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// The data.json contents from the service health dashboard
    /// </summary>
    public class DashboardEventData
    {
        /// <summary>
        /// Previously occured events
        /// </summary>
        public IEnumerable<DashboardEventRaw> Archive { get; set; }

        /// <summary>
        /// Current events affecting services
        /// </summary>
        public IEnumerable<DashboardEventRaw> Current { get; set; }
    }
}
