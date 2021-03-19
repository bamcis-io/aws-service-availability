using System;

namespace BAMCIS.ServiceAvailability
{
    public class ExtractBeginEndFailureException : Exception
    {
        #region Public Properties

        public string Input { get; }

        #endregion

        #region Constructors

        public ExtractBeginEndFailureException(string message, string input) : base(message) { this.Input = input; }

        public ExtractBeginEndFailureException(string message, string input, Exception innerException) : base(message, innerException) { this.Input = input; }

        public ExtractBeginEndFailureException(string message, Exception innerException) : base(message, innerException) {  }

        #endregion
    }
}
