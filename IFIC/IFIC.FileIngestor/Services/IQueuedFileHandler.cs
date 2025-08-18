/************************************************************************************
* FILE          : IQueuedFileHandler.cs
* PROJECT       : IFIC.FileIngestor
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-17
* DESCRIPTION   :
*   Interface seam for your pass/fail logic. The processor calls this per file.
************************************************************************************/

using IFIC.FileIngestor.Models;
using System.Threading;
using System.Threading.Tasks;

namespace IFIC.FileIngestor.Services
{
    /// <summary>
    /// Result of processing a queued submission file.
    /// </summary>
    public enum ProcessingResult
    {
        Passed,
        Failed,
        Skipped // Leave file in place (no move).
    }

    /// <summary>
    /// User-implemented handler that performs the core work (e.g., build/submit bundle).
    /// You will implement the pass/fail decision later.
    /// </summary>
    public interface IQueuedFileHandler
    {
        /// <summary>
        /// Execute the core processing for a given submission.
        /// Return Passed or Failed to route files, or Skipped to leave in Queued.
        /// </summary>
        ProcessingResult Process(SubmissionContext context, CancellationToken ct = default);
        // If you prefer async, change to: Task<ProcessingResult> ProcessAsync(SubmissionContext context, CancellationToken ct = default);
    }
}
