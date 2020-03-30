using System.Collections.Generic;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// The data.json contents from the service health dashboard
    /// </summary>
    public class SlaData
    {
        /// <summary>
        /// Previously occured events
        /// </summary>
        public List<DataEntry> Archive { get; set; }

        /// <summary>
        /// Current events affecting services
        /// </summary>
        public List<DataEntry> Current { get; set; }
    }
}
