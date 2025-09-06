/*  
 * FILE          : ClarityLTCFUpdateService.cs
 * PROJECT       : IFIC.FileIngestor
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-09-03
 * DESCRIPTION   :
 *   Applies my IFIC post-submission update rules for the Clarity LTCF database.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using IFIC.ClarityClient;
using IFIC.FileIngestor.Models;

namespace IFIC.FileIngestor
{
    public sealed class ClarityLTCFUpdateService
    {
        private readonly IClarityClient clarityClient;

        public ClarityLTCFUpdateService(IClarityClient clarityClient)
        {
            this.clarityClient = clarityClient ?? throw new ArgumentNullException(nameof(clarityClient));
        }

        //
        // FUNCTION      : ApplyUpdatesAsync
        // DESCRIPTION   : Applies all LTCF updates based on AdminMetadata and the final outcome (PASS/FAIL).
        //
        public async Task ApplyUpdatesAsync(AdminMetadata admin, string resultStatus, CancellationToken cancellationToken)
        {
            if (admin == null) throw new ArgumentNullException(nameof(admin));
            if (string.IsNullOrWhiteSpace(resultStatus)) throw new ArgumentException("Result status is required.", nameof(resultStatus));

            var isPass = string.Equals(resultStatus, "PASS", StringComparison.OrdinalIgnoreCase);

            // Patient → CREATE + PASS
            if (isPass &&
                string.Equals(admin.PatOper, "CREATE", StringComparison.OrdinalIgnoreCase) &&
                AdminMetadataKeys.TryGetFhirPatKey(admin, out var patientKey) &&
                !string.IsNullOrWhiteSpace(admin.FhirPatID))
            {
                _ = await clarityClient.UpdatePatientAsync(patientKey, admin.FhirPatID!, cancellationToken);
            }

            // Encounter → CREATE + PASS
            if (isPass &&
                string.Equals(admin.EncOper, "CREATE", StringComparison.OrdinalIgnoreCase) &&
                AdminMetadataKeys.TryGetFhirEncKey(admin, out var encounterKey) &&
                !string.IsNullOrWhiteSpace(admin.FhirEncID))
            {
                _ = await clarityClient.UpdateEncounterAsync(encounterKey, admin.FhirEncID!, cancellationToken);
            }

            // Assessment → CREATE + PASS
            if (isPass &&
                string.Equals(admin.AsmOper, "CREATE", StringComparison.OrdinalIgnoreCase) &&
                AdminMetadataKeys.TryGetRecId(admin, out var assessmentRecId) &&
                !string.IsNullOrWhiteSpace(admin.FhirAsmID))
            {
                _ = await clarityClient.UpdateAssessmentAsync(assessmentRecId, admin.FhirAsmID!, cancellationToken);
            }

            // Assessment (any of CREATE/CORRECTION/DELETE) → always write status if rec_id present
            if ((string.Equals(admin.AsmOper, "CREATE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(admin.AsmOper, "CORRECTION", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(admin.AsmOper, "DELETE", StringComparison.OrdinalIgnoreCase)) &&
                 AdminMetadataKeys.TryGetRecId(admin, out var statusRecId))
            {
                _ = await clarityClient.UpdateSubmissionStatusAsync(statusRecId, isPass ? "PASS" : "FAIL", cancellationToken);
            }
        }
    }
}
