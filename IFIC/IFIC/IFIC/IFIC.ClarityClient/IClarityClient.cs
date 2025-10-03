/*
 *  FILE          : IClarityClient.cs
 *  PROJECT       : IFIC.ClarityClient
 *  DESCRIPTION   :
 *    Contract for database write-backs into the LTCF (Clarity) DB.
 *    These methods support both PASS and FAIL (error) paths when handling
 *    CIHI submissions and OperationOutcome responses.
 *
 *  PASTE LOCATION : IFIC.ClarityClient/IClarityClient.cs (replace file)
 */

using System.Threading;
using System.Threading.Tasks;

namespace IFIC.ClarityClient
{
    /// <summary>
    /// Abstraction over the LTCF (Clarity) database updates used by the IFIC flow.
    /// </summary>
    public interface IClarityClient
    {
        Task<int> DeleteFhirEncounterIDAsync(string fhirEncId, CancellationToken cancellationToken);
        /// <summary>
        /// Writes the FHIR Patient ID back to dbo.fhirPatient for the record identified by <paramref name="fhirPatKey"/>.
        /// </summary>
        /// <param name="fhirPatKey">Row key (often INT stringified upstream). Ignored if null/empty.</param>
        /// <param name="fhirPatId">FHIR Patient resource ID to store (nullable treated as DB NULL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated.</returns>
        Task<int> UpdatePatientAsync(string? fhirPatKey, string fhirPatId, CancellationToken cancellationToken);

        /// <summary>
        /// Writes the FHIR Encounter ID back to dbo.fhirEncounter for the record identified by <paramref name="fhirEncKey"/>.
        /// </summary>
        /// <param name="fhirEncKey">Row key (often INT stringified upstream). Ignored if null/empty.</param>
        /// <param name="fhirEncId">FHIR Encounter resource ID to store (nullable treated as DB NULL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated.</returns>
        Task<int> UpdateEncounterAsync(string? fhirEncKey, string fhirEncId, CancellationToken cancellationToken);

        /// <summary>
        /// Writes the FHIR Assessment ID back to dbo.Assessments for the provided <paramref name="recId"/>.
        /// </summary>
        /// <param name="recId">Assessment record id (string upstream; implementation parses INT).</param>
        /// <param name="fhirAsmId">FHIR Assessment resource ID to store.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated.</returns>
        Task<int> UpdateAssessmentAsync(string? recId, string fhirAsmId, CancellationToken cancellationToken);

        /// <summary>
        /// Updates SubmissionStatus.status for the supplied <paramref name="recId"/> (e.g., PASS/FAIL).
        /// </summary>
        /// <param name="recId">Assessment record id (string upstream; implementation parses INT).</param>
        /// <param name="status">New status value to store (e.g., "PASS", "FAIL").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated.</returns>
        Task<int> UpdateSubmissionStatusAsync(string? recId, string status, CancellationToken cancellationToken);

        /// <summary>
        /// Lightweight connectivity check (SELECT 1).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the database can be reached and responds correctly.</returns>
        Task<bool> PingDatabaseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Appends an HTML line to dbo.Section_&lt;X&gt;.notes for <paramref name="recId"/>.
        /// The implementation concatenates <c>existing + "&lt;br&gt;+" + noteHtml</c>.
        /// </summary>
        /// <param name="sectionLetter">Target section letter (A..Z). Implementations must validate.</param>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="noteHtml">Pre-formatted message to append (HTML allowed).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated.</returns>
        Task<int> AppendSectionNoteAsync(string sectionLetter, int recId, string noteHtml, CancellationToken cancellationToken);

        /// <summary>
        /// Sets ccrsSectionState.Section_&lt;X&gt; to <paramref name="state"/> for <paramref name="recId"/>.
        /// </summary>
        /// <param name="sectionLetter">Target section letter (A..Z). Implementations must validate.</param>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="state">State code (e.g., "2" for yellow checkmark).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated.</returns>
        Task<int> SetSectionStateAsync(string sectionLetter, int recId, string state, CancellationToken cancellationToken);

        /// <summary>
        /// Marks Assessments row for <paramref name="recId"/> as Status='Incomplete', transmit='NO'.
        /// </summary>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated.</returns>
        Task<int> MarkAssessmentIncompleteNotTransmittedAsync(int recId, CancellationToken cancellationToken);

        /// <summary>
        /// If the associated patient is currently 'discharged', set Status='Active'.
        /// Joins via Assessments.uid → patients.uid. No-op when already Active.
        /// </summary>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows updated (0 or 1 expected).</returns>
        Task<int> MarkPatientActiveByRecIdAsync(int recId, CancellationToken cancellationToken);
    }
}
