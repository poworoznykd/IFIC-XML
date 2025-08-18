/************************************************************************************
* FILE          : QueueOptions.cs
* PROJECT       : IFIC.FileIngestor
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-17
* DESCRIPTION   :
*   Strongly-typed configuration for the file queue processor.
************************************************************************************/

namespace IFIC.FileIngestor.Options
{
    /// <summary>
    /// Options for queue processing. Bind from appsettings.json or environment.
    /// </summary>
    public sealed class QueueOptions
    {
        /// <summary>
        /// Root folder of the LTCF Transmit tree. Must contain a "Queued" subfolder.
        /// Example: \\share\LTCF Transmit
        /// </summary>
        public string RootFolder { get; set; } = string.Empty;

        /// <summary>
        /// Search pattern for queued data files. Example: "*.dat"
        /// </summary>
        public string SearchPattern { get; set; } = "*.dat";
    }
}
