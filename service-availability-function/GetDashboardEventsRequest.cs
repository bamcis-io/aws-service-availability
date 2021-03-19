using System;
using System.Linq;
using Amazon;
using System.Collections.Generic;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// The query string parameters of the GET request translated into
    /// a class to filter the service health data
    /// </summary>
    public class GetDashboardEventsRequest
    {
        #region Private Fields

        private List<RegionEndpoint> _regions;
        private long _end;
        private string _output;

        #endregion

        #region Public Properties

        /// <summary>
        /// A comma delimited list of services to query on
        /// </summary>
        public IEnumerable<string> Services { get; set; }

        /// <summary>
        /// A comma delimited list of regions to query on
        /// </summary>
        public IEnumerable<string> Regions
        {
            get
            {
                if (this._regions != null)
                {
                    return this._regions.Select(x => x.SystemName);
                }
                else
                {
                    return Enumerable.Empty<string>();
                }
            }
            set
            {
                this._regions = new List<RegionEndpoint>();

                foreach (string part in value)
                {
                    if (RegionEndpoint.EnumerableAllRegions.Select(x => x.SystemName.ToLower()).Contains(part.ToLower()))
                    {
                        this._regions.Add(RegionEndpoint.GetBySystemName(part));
                    }
                    else
                    {
                        throw new ArgumentException($"Did not recognize the region {part}.");
                    }
                }
            }
        }

        /// <summary>
        /// Seconds past Jan 1st, 1970
        /// </summary>
        public long Start { get; set; }

        /// <summary>
        /// Seconds past Jan 1st, 1970
        /// </summary>
        public long End
        {
            get
            {
                return this._end;
            }
            set
            {
                if (this.Start > 0)
                {
                    if (value >= this.Start)
                    {
                        this._end = value;
                    }
                    else
                    {
                        throw new ArgumentException("The End time must be greater than the Start time");
                    }
                }
                else
                {
                    this._end = value;
                }
            }
        }

        /// <summary>
        /// Specifies the output type, this can either be json, the default, or csv
        /// </summary>
        public string Output
        {
            get
            {
                return this._output;
            }
            set
            {
                if (value != null)
                {
                    switch (value.ToLower())
                    {
                        case "csv":
                            {
                                this._output = "csv";
                                break;
                            }
                        default:
                        case "json":
                            {
                                this._output = "json";
                                break;
                            }
                    }
                }
                else
                {
                    this._output = String.Empty;
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor for the request
        /// </summary>
        public GetDashboardEventsRequest()
        {
            this.Services = Enumerable.Empty<string>();
            this.Regions = Enumerable.Empty<string>();
            this.Start = 0;
            this.End = 0;
            this._regions = new List<RegionEndpoint>();
        }

        /// <summary>
        /// Creates the request object from the dictionary mapping of the request
        /// query string parameters
        /// </summary>
        /// <param name="request">The query string parameters</param>
        public GetDashboardEventsRequest(IDictionary<string, string> request) : this()
        {
            if (request != null)
            {
                foreach (string key in request.Keys)
                {
                    switch (key.ToLower())
                    {
                        case "services":
                            {
                                this.Services = request[key].Split(',');
                                break;
                            }
                        case "regions":
                            {
                                this.Regions = request[key].Split(',');
                                break;
                            }
                        case "start":
                            {
                                if (Int64.TryParse(request[key], out long temp))
                                {
                                    this.Start = temp;
                                }

                                break;
                            }
                        case "end":
                            {
                                if (Int64.TryParse(request[key], out long temp))
                                {
                                    this.End = temp;
                                }

                                break;
                            }
                        case "output":
                            {
                                this.Output = request[key];
                                break;
                            }
                    }
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the specified region endpoints in the request
        /// </summary>
        /// <returns></returns>
        public List<RegionEndpoint> GetRegionEndpoints()
        {
            return this._regions;
        }

        /// <summary>
        /// Get a list of the specified regions as string values
        /// using the region endpoint system name
        /// </summary>
        /// <returns>A list of regions in the us-east-1 type format</returns>
        public List<string> GetRegions()
        {
            return this._regions.Select(x => x.SystemName.ToLower()).ToList();
        }

        #endregion
    }
}
