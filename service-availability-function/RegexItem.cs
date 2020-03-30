using System.Text.RegularExpressions;

namespace BAMCIS.ServiceAvailability
{
    /// <summary>
    /// Provides a conversion for a regex stored in a json config file to 
    /// an object in the config
    /// </summary>
    public class RegexItem
    {
        /// <summary>
        /// The regex options string 
        /// </summary>
        private string _Options;

        /// <summary>
        /// The RegexOptions object created from the string
        /// </summary>
        private RegexOptions _ROptions;

        /// <summary>
        /// The Regex string provided by the user
        /// </summary>
        public string Regex { get; set; }

        /// <summary>
        /// The regex options string provided by the user. This also
        /// creates the RegexOptions object
        /// </summary>
        public string Options
        {
            get
            {
                return this._Options;
            }
            set
            {
                this._Options = value;
                this._ROptions = new RegexOptions();

                char[] Items = this.Options.ToCharArray();

                foreach (char Item in Items)
                {
                    switch (this.Options.ToLower())
                    {
                        case "i":
                            {
                                this._ROptions = this._ROptions | RegexOptions.IgnoreCase;
                                break;
                            }
                        case "m":
                            {
                                this._ROptions = this._ROptions | RegexOptions.Multiline;
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }
            }
        }

        /// <summary>
        /// Default constructor, does not initialize anything
        /// </summary>
        public RegexItem()
        {
            //Empty body
        }

        /// <summary>
        /// Builds the item from the regex and options string
        /// </summary>
        /// <param name="regex">The regex to use</param>
        /// <param name="options">The options as a string, like "i" or "im"</param>
        public RegexItem(string regex, string options)
        {
            this.Regex = regex;
            this.Options = options;
        }

        /// <summary>
        /// Gets the options being used with the Regex
        /// </summary>
        /// <returns>A RegexOptions object</returns>
        public RegexOptions GetOptions()
        {
            return this._ROptions;
        }

        /// <summary>
        /// Gets the Regex object from the regex string and regex options string
        /// </summary>
        /// <returns>A Regex object</returns>
        public Regex GetRegex()
        {
            return new Regex(this.Regex, this.GetOptions());
        }
    }
}
