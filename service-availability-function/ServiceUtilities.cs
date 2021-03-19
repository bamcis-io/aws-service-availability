using System;
using System.Text.RegularExpressions;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// Utilities for the SHD service names
    /// </summary>
    public static class ServiceUtilities
    {
        private static Regex regionRegex = new Regex("((?:us|eu|cn|ap|ca|me|sa|af)(?:-gov|-isob?)?-(?:(?:(?:central|(?:north|south)?(?:east|west)?)-\\d)|standard))");

        /// <summary>
        /// Extracts the region from service name. If the region can't be found, the original string is returned.
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static string GetRegion(string service)
        {
            Match match = regionRegex.Match(service);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            else
            {
                return "global";
            }
        }

        /// <summary>
        /// Extracts the service short name from the service by removing the region
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static string GetServiceName(string service)
        {
            Match match = regionRegex.Match(service);

            if (match.Success)
            {
                // Subtract 1 to remove the last hyphen
                return service.Substring(0, service.IndexOf(match.Groups[1].Value) - 1);
            }
            else
            {
                return service;
            }
        }

        /// <summary>
        /// Converts the number of seconds from midnight 1/1/1970 to a DateTime object
        /// </summary>
        /// <param name="timestamp">The number of seconds past midnight 1/1/1970</param>
        /// <returns>A DateTime object of the provided timestamp</returns>
        public static DateTime ConvertFromUnixTimestamp(long timestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp);
        }

        /// <summary>
        /// Converts a DateTime object to seconds past 1 Jan 1970
        /// </summary>
        /// <param name="date">The DateTime object to convert</param>
        /// <returns>The number of seconds past midnight 1/1/1970</returns>
        public static long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return (long)Math.Floor(diff.TotalSeconds);
        }
    }
}
