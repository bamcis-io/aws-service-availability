using System;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// Represents an entry from the data.json file from the
    /// service health dashboard
    /// </summary>
    public class DataEntry
    {
        /// <summary>
        /// The friendly service name
        /// </summary>
        public string Service_Name { get; set; }

        /// <summary>
        /// The summary of the event
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// A unix timestamp of when the event was posted
        /// </summary>
        public string Date { get; set; }

        /// <summary>
        /// The status of the event
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The details of the event, this is usually blank
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// The description of the event and updates to the status
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The service affected by the event, this is represented as the service
        /// name with the AWS region, like ec2-us-east-1
        /// </summary>
        public string Service { get; set; }

        /// <summary>
        /// Gets a DateTime object from the Date attribute
        /// </summary>
        /// <returns>
        /// The DateTime object representation of the Date string, or a DateTime.MinValue if the string
        /// was not valid to parse to an int
        /// </returns>
        public DateTime GetDate()
        {
            int temp;

            if (Int32.TryParse(this.Date, out temp))
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(temp);
            }
            else
            {
                return DateTime.MinValue;
            }
        }
    }
}
