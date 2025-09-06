/*  
 * FILE          : ClarityClient.cs
 * PROJECT       : IFIC.ClarityClient
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-09-03
 * DESCRIPTION   :
 *   Implements IClarityClient with parameterized UPDATEs. I accept keys as strings
 *   because the [ADMIN] metadata is not guaranteed numeric. I bind keys as NVARCHAR
 *   to avoid parse failures. If needed later, I can optimize with TRY_CONVERT logic.
 */

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace IFIC.ClarityClient
{
    public sealed class ClarityClient : IClarityClient, IDisposable
    {
        private readonly ClarityClientOptions clarityClientOptions;
        private SqlConnection? clarityConnection;

        //
        // FUNCTION      : ClarityClient (constructor)
        // DESCRIPTION   : Initializes the client with required options and validates the connection string.
        //
        public ClarityClient(ClarityClientOptions clarityClientOptions)
        {
            this.clarityClientOptions = clarityClientOptions
                ?? throw new ArgumentNullException(nameof(clarityClientOptions));

            if (string.IsNullOrWhiteSpace(this.clarityClientOptions.ConnectionString))
                throw new ArgumentException("Clarity/LTCF ConnectionString must be configured.");
        }

        //
        // FUNCTION      : UpdatePatientAsync
        // DESCRIPTION   : Updates dbo.fhirPatient.fhirPatID where fhirPatKey matches the supplied string key.
        // PARAMETERS    : string? fhirPatKey, string fhirPatId, CancellationToken cancellationToken
        // RETURNS       : Task<int>
        //
        public async Task<int> UpdatePatientAsync(string? fhirPatKey, string fhirPatId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fhirPatKey)) return 0;

            const string sql = @"
                UPDATE dbo.fhirPatient
                   SET fhirPatID = @fhirPatID
                 WHERE CAST(fhirPatKey AS NVARCHAR(64)) = @fhirPatKey;";

            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirPatID", SqlDbType.NVarChar, clarityClientOptions.FhirIdMaxLength) { Value = (object?)fhirPatId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@fhirPatKey", SqlDbType.NVarChar, 64) { Value = fhirPatKey!.Trim() });

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        //
        // FUNCTION      : UpdateEncounterAsync
        // DESCRIPTION   : Updates dbo.fhirEncounter.fhirEncID where fhirEncKey matches the supplied string key.
        // PARAMETERS    : string? fhirEncKey, string fhirEncId, CancellationToken cancellationToken
        // RETURNS       : Task<int>
        //
        public async Task<int> UpdateEncounterAsync(string? fhirEncKey, string fhirEncId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fhirEncKey)) return 0;

            const string sql = @"
                UPDATE dbo.fhirEncounter
                   SET fhirEncID = @fhirEncID
                 WHERE CAST(fhirEncKey AS NVARCHAR(64)) = @fhirEncKey;";

            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirEncID", SqlDbType.NVarChar, clarityClientOptions.FhirIdMaxLength) { Value = (object?)fhirEncId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@fhirEncKey", SqlDbType.NVarChar, 64) { Value = fhirEncKey!.Trim() });

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        //
        // FUNCTION      : UpdateAssessmentAsync
        // DESCRIPTION   : Updates dbo.Assessments.fhirAsmID where rec_id matches the supplied string key.
        // PARAMETERS    : string? recId, string fhirAsmId, CancellationToken cancellationToken
        // RETURNS       : Task<int>
        //
        public async Task<int> UpdateAssessmentAsync(string? recId, string fhirAsmId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(recId)) return 0;

            const string sql = @"
                UPDATE dbo.Assessments
                   SET fhirAsmID = @fhirAsmID
                 WHERE CAST(rec_id AS NVARCHAR(64)) = @rec_id;";

            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirAsmID", SqlDbType.NVarChar, clarityClientOptions.FhirIdMaxLength) { Value = (object?)fhirAsmId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.NVarChar, 64) { Value = recId!.Trim() });

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        //
        // FUNCTION      : UpdateSubmissionStatusAsync
        // DESCRIPTION   : Updates dbo.SubmissionStatus.status where rec_id matches the supplied string key.
        // PARAMETERS    : string? recId, string status, CancellationToken cancellationToken
        // RETURNS       : Task<int>
        //
        public async Task<int> UpdateSubmissionStatusAsync(string? recId, string status, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(recId)) return 0;

            const string sql = @"
                UPDATE dbo.SubmissionStatus
                   SET status = @status
                 WHERE CAST(rec_id AS NVARCHAR(64)) = @rec_id;";

            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, clarityClientOptions.StatusMaxLength) { Value = (object?)status ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.NVarChar, 64) { Value = recId!.Trim() });

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        //
        // FUNCTION      : PingDatabaseAsync
        // DESCRIPTION   : Connectivity smoke test (SELECT 1).
        //
        public async Task<bool> PingDatabaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (clarityConnection is null)
                {
                    clarityConnection = new SqlConnection(clarityClientOptions.ConnectionString);
                    await clarityConnection.OpenAsync(cancellationToken);
                }

                using var command = clarityConnection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandType = CommandType.Text;

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result != null && Convert.ToInt32(result) == 1;
            }
            catch
            {
                return false;
            }
        }

        //
        // FUNCTION      : CreateCommandAsync
        // DESCRIPTION   : Ensures a connection is open and returns a configured SqlCommand.
        //
        private async Task<SqlCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
        {
            if (clarityConnection is null)
            {
                clarityConnection = new SqlConnection(clarityClientOptions.ConnectionString);
                await clarityConnection.OpenAsync(cancellationToken);
            }

            var command = clarityConnection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = clarityClientOptions.CommandTimeoutSec;
            return command;
        }

        //
        // FUNCTION      : Dispose
        // DESCRIPTION   : Disposes the active SQL connection if one was opened.
        //
        public void Dispose()
        {
            clarityConnection?.Dispose();
            clarityConnection = null;
        }
    }
}
