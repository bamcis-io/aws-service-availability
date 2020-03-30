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
    public class SLARequest
    {
        private List<RegionEndpoint> _Regions;
        private long _End;
        private string _Output;

        /// <summary>
        /// A comma delimited list of services to query on
        /// </summary>
        public string Services { get; set; }

        /// <summary>
        /// A comma delimited list of regions to query on
        /// </summary>
        public string Regions
        {
            get
            {
                if (this._Regions != null)
                {
                    return String.Join(",", this._Regions.Select(x => x.SystemName));
                }
                else
                {
                    return String.Empty;
                }
            }
            set
            {
                IEnumerable<string> Parts = value.Split(',').Where(x => !String.IsNullOrEmpty(x));
                this._Regions = new List<RegionEndpoint>();

                foreach (string Part in Parts)
                {
                    if (RegionEndpoint.EnumerableAllRegions.Select(x => x.SystemName.ToLower()).Contains(Part.ToLower()))
                    {
                        this._Regions.Add(RegionEndpoint.GetBySystemName(Part));
                    }
                    else
                    {
                        throw new ArgumentException($"Did not recognize the region {Part}.");
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
                return this._End;
            }
            set
            {
                if (this.Start > 0)
                {
                    if (value >= this.Start)
                    {
                        this._End = value;
                    }
                    else
                    {
                        throw new ArgumentException("The End time must be greater than the Start time");
                    }
                }
                else
                {
                    this._End = value;
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
                return this._Output;
            }
            set
            {
                if (value != null)
                {
                    switch (value.ToLower())
                    {
                        case "csv":
                            {
                                this._Output = "csv";
                                break;
                            }
                        default:
                        case "json":
                            {
                                this._Output = "json";
                                break;
                            }
                    }
                }
                else
                {
                    this._Output = String.Empty;
                }
            }
        }

        /// <summary>
        /// Default constructor for the request
        /// </summary>
        public SLARequest()
        {
            this.Services = String.Empty;
            this.Regions = String.Empty;
            this.Start = 0;
            this.End = 0;
            this._Regions = new List<RegionEndpoint>();
        }

        /// <summary>
        /// Creates the request object from the dictionary mapping of the request
        /// query string parameters
        /// </summary>
        /// <param name="request">The query string parameters</param>
        public SLARequest(IDictionary<string, string> request) : this()
        {
            if (request != null)
            {
                foreach (string Key in request.Keys)
                {
                    switch (Key.ToLower())
                    {
                        case "services":
                            {
                                this.Services = request[Key];
                                break;
                            }
                        case "regions":
                            {
                                this.Regions = request[Key];
                                break;
                            }
                        case "start":
                            {
                                if (Int64.TryParse(request[Key], out long temp))
                                {
                                    this.Start = temp;
                                }

                                break;
                            }
                        case "end":
                            {
                                if (Int64.TryParse(request[Key], out long temp))
                                {
                                    this.End = temp;
                                }

                                break;
                            }
                        case "output":
                            {
                                this.Output = request[Key];
                                break;
                            }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the specified region endpoints in the request
        /// </summary>
        /// <returns></returns>
        public List<RegionEndpoint> GetRegionEndpoints()
        {
            return this._Regions;
        }

        /// <summary>
        /// Get a list of the specified regions as string values
        /// using the region endpoint system name
        /// </summary>
        /// <returns>A list of regions in the us-east-1 type format</returns>
        public List<string> GetRegions()
        {
            return this._Regions.Select(x => x.SystemName.ToLower()).ToList();
        }
    }
}
