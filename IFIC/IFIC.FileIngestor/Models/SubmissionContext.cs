/************************************************************************************
* FILE          : SubmissionContext.cs
* PROJECT       : IFIC.FileIngestor
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-17
* DESCRIPTION   :
*   Context passed to your handler containing file info and parsed ADMIN data.
************************************************************************************/

using System;
using System.IO;

namespace IFIC.FileIngestor.Models
{
    /// <summary>
    /// Encapsulates the file and metadata for a queued submission.
    /// </summary>
    public sealed class SubmissionContext
    {
        /// <summary>The .dat (or primary) file to process.</summary>
        public FileInfo DataFile { get; }

        /// <summary>Parsed [ADMIN] metadata.</summary>
        public AdminMetadata Admin { get; }

        /// <summary>Basename without extension, used to find matching .xml.</summary>
        public string BaseFileNameWithoutExtension { get; }

        public SubmissionContext(FileInfo dataFile, AdminMetadata admin)
        {
            DataFile = dataFile ?? throw new ArgumentNullException(nameof(dataFile));
            Admin = admin ?? throw new ArgumentNullException(nameof(admin));
            BaseFileNameWithoutExtension = Path.GetFileNameWithoutExtension(DataFile.Name);
        }
    }
}
