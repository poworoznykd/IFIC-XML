/*
 *  FILE          : ClarityLTCFUpdateService.cs
 *  PROJECT       : IFIC.FileIngestor
 *  DESCRIPTION   :
 *    Applies IFIC post‑submission update rules for the Clarity LTCF database.
 *    This version DOES NOT use IOptions<>. It relies on DI to provide the
 *    concrete ClarityClientOptions instance you already register in Program.cs
 *    (services.AddSingleton(new ClarityClientOptions { ... })).
 */

using DocumentFormat.OpenXml.Spreadsheet;
using IFIC.ClarityClient;
using IFIC.FileIngestor.Models;
using IFIC.Outcome; // OperationOutcomeProcessor_v2
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IFIC.FileIngestor
{
    /// <summary>
    /// Orchestrates PASS/FAIL write‑backs to LTCF and applies CIHI error details to sections.
    /// </summary>
    public sealed class ClarityLTCFUpdateService
    {
        private readonly IClarityClient clarityClient;
        private readonly string elementMappingPath;
        private readonly Lazy<IElementMapping> mapping; // built on first FAIL that needs it

        /// <summary>
        /// DI entrypoint. Takes the concrete options instance you register in the host builder
        /// and pulls ElementMappingPath directly from it (no IOptions used).
        /// </summary>
        /// <param name="clarityClient">DB client used for all LTCF updates.</param>
        /// <param name="clarityOptions">Concrete options object (registered as AddSingleton in Program.cs).</param>
        public ClarityLTCFUpdateService(IClarityClient clarityClient, ClarityClientOptions clarityOptions)
        {
            this.clarityClient = clarityClient ?? throw new ArgumentNullException(nameof(clarityClient));
            if (clarityOptions == null) throw new ArgumentNullException(nameof(clarityOptions));

            elementMappingPath = clarityOptions.ElementMappingPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(elementMappingPath))
                throw new ArgumentException("ElementMappingPath must be configured in ClarityClientOptions.");

            mapping = new Lazy<IElementMapping>(() => new ElementMapping(elementMappingPath), isThreadSafe: true);
        }

        /// <summary>
        /// Original signature kept for compatibility (no OperationOutcome XML).
        /// Routes to the 4‑parameter overload with a null XML.
        /// </summary>
        public Task ApplyUpdatesAsync(AdminMetadata admin, string resultStatus, CancellationToken cancellationToken)
            => ApplyUpdatesAsync(admin, resultStatus, operationOutcomeXml: null, cancellationToken);

        /// <summary>
        /// Applies all LTCF updates based on admin metadata, the CIHI result (PASS/FAIL),
        /// and the raw OperationOutcome XML when present (required for FAIL notes mapping).
        /// </summary>
        /// <param name="admin">Admin metadata for this submission (contains operations & IDs).</param>
        /// <param name="resultStatus">"PASS" or "FAIL".</param>
        /// <param name="operationOutcomeXml">OperationOutcome XML from CIHI (use on FAIL to map iCodes).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ApplyUpdatesAsync(
            AdminMetadata admin,
            string resultStatus,
            string? operationOutcomeXml,
            CancellationToken cancellationToken)
        {
            if (admin == null) throw new ArgumentNullException(nameof(admin));
            if (string.IsNullOrWhiteSpace(resultStatus)) throw new ArgumentException("Result status is required.", nameof(resultStatus));

            bool isPass = string.Equals(resultStatus, "PASS", StringComparison.OrdinalIgnoreCase);
            if (!isPass)
            {

            }
            //HAPPY PASS PATH
            if (isPass)
            {
                // Patient → CREATE + PASS
                if (string.Equals(admin.PatOper, "CREATE", StringComparison.OrdinalIgnoreCase)
                    && AdminMetadataKeys.TryGetFhirPatKey(admin, out var patientKey)
                    && !string.IsNullOrWhiteSpace(admin.FhirPatID))
                {
                    _ = await clarityClient.UpdatePatientAsync(patientKey, admin.FhirPatID!, cancellationToken);
                }

                // Encounter → CREATE + PASS
                if (string.Equals(admin.EncOper, "CREATE", StringComparison.OrdinalIgnoreCase)
                    && AdminMetadataKeys.TryGetFhirEncKey(admin, out var encounterKey)
                    && !string.IsNullOrWhiteSpace(admin.FhirEncID))
                {
                    _ = await clarityClient.UpdateEncounterAsync(encounterKey, admin.FhirEncID!, cancellationToken);
                }

                // Assessment → CREATE + PASS
                if (string.Equals(admin.AsmOper, "CREATE", StringComparison.OrdinalIgnoreCase)
                    && AdminMetadataKeys.TryGetRecId(admin, out var assessmentRecId)
                    && !string.IsNullOrWhiteSpace(admin.FhirAsmID))
                {
                    _ = await clarityClient.UpdateAssessmentAsync(assessmentRecId, admin.FhirAsmID!, cancellationToken);
                }

                // Always write submission status when we have rec_id
                if (AdminMetadataKeys.TryGetRecId(admin, out var passRecId))
                {
                    _ = await clarityClient.UpdateSubmissionStatusAsync(passRecId, "PASS", cancellationToken);
                }

                return; 
            }
            //FAIL PATH
            if (!AdminMetadataKeys.TryGetRecId(admin, out var failRecId))
                throw new InvalidOperationException("rec_id is required to persist FAIL details.");

            // If this is a discharge-type submission and it FAILED, ensure patient is reactivated.
            // We only touch patients currently marked as 'discharged' and only ever update 0 or 1 row.
            if (!string.IsNullOrWhiteSpace(failRecId)
                && !string.IsNullOrWhiteSpace(admin.AsmType)
                && admin.AsmType.IndexOf("discharg", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (int.TryParse(failRecId, out var failRecIdInt) && failRecIdInt > 0)
                {
                    try
                    {
                        _ = await clarityClient.MarkPatientActiveByRecIdAsync(failRecIdInt, cancellationToken);
                    }
                    catch
                    {
                        // Non-fatal safeguard: do not block the rest of the FAIL write-backs
                    }
                }
            }

            _ = await clarityClient.UpdateSubmissionStatusAsync(failRecId, "FAIL", cancellationToken);

            // If we have CIHI OperationOutcome XML, parse and write section updates
            if (!string.IsNullOrWhiteSpace(operationOutcomeXml))
            {
                await OperationOutcomeProcessor.ApplyErrorsAsync(
                    db: clarityClient,
                    map: mapping.Value,             // built on first use using ElementMappingPath from options
                    recIdString: failRecId,
                    asmOper: admin.AsmOper,
                    operationOutcomeXml: operationOutcomeXml,
                    ct: cancellationToken);
            }
        }
    }
}
