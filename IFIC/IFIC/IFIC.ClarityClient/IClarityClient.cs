/*  
 * FILE          : IClarityClient.cs
 * PROJECT       : IFIC.ClarityClient
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-09-03
 * DESCRIPTION   :
 *   Contract for writing CIHI results back to LTCF. Keys are accepted as strings
 *   because the [ADMIN] metadata may not be strictly numeric. The database layer
 *   handles binding safely as NVARCHAR.
 */

using System.Threading;
using System.Threading.Tasks;

namespace IFIC.ClarityClient
{
    /*
     * NAME    : IClarityClient
     * PURPOSE : Provide the focused API my update service calls after a CIHI submission.
     */
    public interface IClarityClient
    {
        //
        // FUNCTION      : UpdatePatientAsync
        // DESCRIPTION   : Sets dbo.fhirPatient.fhirPatID for the specified key.
        // PARAMETERS    : string? fhirPatKey, string fhirPatId, CancellationToken cancellationToken
        // RETURNS       : Task<int> : rows affected
        //
        Task<int> UpdatePatientAsync(string? fhirPatKey, string fhirPatId, CancellationToken cancellationToken);

        //
        // FUNCTION      : UpdateEncounterAsync
        // DESCRIPTION   : Sets dbo.fhirEncounter.fhirEncID for the specified key.
        //
        Task<int> UpdateEncounterAsync(string? fhirEncKey, string fhirEncId, CancellationToken cancellationToken);

        //
        // FUNCTION      : UpdateAssessmentAsync
        // DESCRIPTION   : Sets dbo.Assessments.fhirAsmID for the specified rec_id.
        //
        Task<int> UpdateAssessmentAsync(string? recId, string fhirAsmId, CancellationToken cancellationToken);

        //
        // FUNCTION      : UpdateSubmissionStatusAsync
        // DESCRIPTION   : Sets dbo.SubmissionStatus.status (PASS/FAIL) for the specified rec_id.
        //
        Task<int> UpdateSubmissionStatusAsync(string? recId, string status, CancellationToken cancellationToken);

        //
        // FUNCTION      : PingDatabaseAsync
        // DESCRIPTION   : Connectivity smoke test (SELECT 1).
        //
        Task<bool> PingDatabaseAsync(CancellationToken cancellationToken);
    }
}
