using Autodesk.Revit.UI;
using Rca.Contracts.Logging;

namespace Rca.UI
{
    /// <summary>
    /// Holds the current UIApplication and services for use by modeless windows and panels.
    /// </summary>
    public static class RevitContext
    {
        private static ILoggingService _loggingService;

        /// <summary>
        /// Gets or sets the current UIApplication instance.
        /// </summary>
        public static UIApplication CurrentUIApplication { get; set; }

        /// <summary>
        /// Gets the logging service instance.
        /// </summary>
        public static ILoggingService LoggingService 
        { 
            get 
            {
                if (_loggingService == null)
                    _loggingService = new LoggingService();
                return _loggingService;
            }
        }
    }
}
