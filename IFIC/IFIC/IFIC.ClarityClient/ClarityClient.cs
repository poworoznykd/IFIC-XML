/*
 *  FILE          : ClarityClient.cs
 *  PROJECT       : IFIC.ClarityClient
 *  DESCRIPTION   :
 *    Concrete implementation of IClarityClient for all LTCF DB write-backs.
 *    Includes PASS-path updaters and error-path helpers (notes/state/flags).
 *
 *  PASTE LOCATION : IFIC.ClarityClient/ClarityClient.cs (replace file)
 */

using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace IFIC.ClarityClient
{
    /// <summary>
    /// SQL Server implementation of <see cref="IClarityClient"/> using parameterized commands
    /// and strict validation for dynamic Section targets.
    /// </summary>
    public sealed class ClarityClient : IClarityClient, IDisposable
    {
        private readonly ClarityClientOptions clarityClientOptions;
        private SqlConnection? clarityConnection;

        // Validate dynamic Section_<X> references strictly (single A..Z)
        private static readonly Regex SectionRegex = new Regex("^[A-Z]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Creates a new client bound to the configured connection string and timeouts.
        /// </summary>
        /// <param name="clarityClientOptions">Connection/timeout/id-length configuration.</param>
        public ClarityClient(ClarityClientOptions clarityClientOptions)
        {
            this.clarityClientOptions = clarityClientOptions ?? throw new ArgumentNullException(nameof(clarityClientOptions));
            if (string.IsNullOrWhiteSpace(this.clarityClientOptions.ConnectionString))
                throw new ArgumentException("Clarity/LTCF ConnectionString must be configured.");
        }

        // -------------------------------------------------------------
        // PASS-path updaters (kept)
        // -------------------------------------------------------------

        /// <inheritdoc />
        public async Task<int> UpdatePatientAsync(string? fhirPatKey, string fhirPatId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fhirPatKey)) return 0;
            const string sql = @"UPDATE dbo.fhirPatient SET fhirPatID=@fhirPatID WHERE CAST(fhirPatKey AS NVARCHAR(64))=@fhirPatKey;";
            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirPatID", SqlDbType.NVarChar, SafeFhirIdLen()) { Value = (object?)fhirPatId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@fhirPatKey", SqlDbType.NVarChar, 64) { Value = fhirPatKey!.Trim() });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> UpdateEncounterAsync(string? fhirEncKey, string fhirEncId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fhirEncKey)) return 0;
            const string sql = @"UPDATE dbo.fhirEncounter SET fhirEncID=@fhirEncID WHERE CAST(fhirEncKey AS NVARCHAR(64))=@fhirEncKey;";
            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirEncID", SqlDbType.NVarChar, SafeFhirIdLen()) { Value = (object?)fhirEncId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@fhirEncKey", SqlDbType.NVarChar, 64) { Value = fhirEncKey!.Trim() });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> UpdateAssessmentAsync(string? recId, string fhirAsmId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(recId)) return 0;
            if (!int.TryParse(recId.Trim(), out var recIdInt)) return 0;
            const string sql = @"UPDATE dbo.Assessments SET fhirAsmID=@fhirAsmID WHERE rec_id=@rec_id;";
            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirAsmID", SqlDbType.NVarChar, SafeFhirIdLen()) { Value = (object?)fhirAsmId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recIdInt });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> UpdateSubmissionStatusAsync(string? recId, string status, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(recId)) return 0;
            if (!int.TryParse(recId.Trim(), out var recIdInt)) return 0;
            const string sql = @"UPDATE dbo.SubmissionStatus SET status=@status WHERE rec_id=@rec_id;";
            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 64) { Value = (object?)status ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recIdInt });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // -------------------------------------------------------------
        // Error-path helpers
        // -------------------------------------------------------------

        public async Task<int> AppendSectionNoteAsync(string sectionLetter, int recId, string noteHtml, CancellationToken cancellationToken)
        {
            string section = NormalizeSection(sectionLetter);
            if (!SectionRegex.IsMatch(section)) return 0; // security: constrain dynamic identifier

            // 1) Read existing note
            string selectSql = $"SELECT notes FROM dbo.Section_{section} WHERE rec_id=@rec_id";
            string? current = null;
            using (var selectCmd = await CreateCommandAsync(selectSql, cancellationToken))
            {
                selectCmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });
                var result = await selectCmd.ExecuteScalarAsync(cancellationToken);
                current = result == DBNull.Value ? null : result as string;
            }

            // 2) Append per spec: existing + "<br>+" + new
            string updated = string.IsNullOrWhiteSpace(current) ? noteHtml : (current + "<br>+" + noteHtml);

            // 3) Write back
            string updateSql = $"UPDATE dbo.Section_{section} SET notes=@notes WHERE rec_id=@rec_id";
            using var updateCmd = await CreateCommandAsync(updateSql, cancellationToken);
            updateCmd.Parameters.Add(new SqlParameter("@notes", SqlDbType.NVarChar, -1) { Value = (object?)updated ?? DBNull.Value });
            updateCmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });
            return await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> SetSectionStateAsync(string sectionLetter, int recId, string state, CancellationToken cancellationToken)
        {
            string section = NormalizeSection(sectionLetter);
            if (!SectionRegex.IsMatch(section)) return 0;

            string sql = $"UPDATE dbo.ccrsSectionState SET Section_{section}=@state WHERE rec_id=@rec_id";
            using var cmd = await CreateCommandAsync(sql, cancellationToken);
            cmd.Parameters.Add(new SqlParameter("@state", SqlDbType.NVarChar, 8) { Value = (object?)state ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> MarkAssessmentIncompleteNotTransmittedAsync(int recId, CancellationToken cancellationToken)
        {
            const string sql = @"UPDATE dbo.Assessments SET Status='Incomplete', transmit='NO' WHERE rec_id=@rec_id";
            using var cmd = await CreateCommandAsync(sql, cancellationToken);
            cmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> MarkPatientActiveByRecIdAsync(int recId, CancellationToken cancellationToken)
        {
            const string sql = @"UPDATE p SET p.Status='Active'
                                 FROM dbo.patients p
                                 INNER JOIN dbo.Assessments a ON a.uid = p.uid
                                 WHERE a.rec_id=@rec_id AND p.Status='discharged'";
            using var cmd = await CreateCommandAsync(sql, cancellationToken);
            cmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Executes SELECT 1 to verify connectivity.
        /// </summary>
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
            catch { return false; }
        }

        /// <summary>
        /// Creates a <see cref="SqlCommand"/> with configured timeout, opening the connection on first use.
        /// </summary>
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

        /// <summary>
        /// Disposes the underlying SQL connection.
        /// </summary>
        public void Dispose()
        {
            clarityConnection?.Dispose();
            clarityConnection = null;
        }

        /// <summary>
        /// Normalizes a section designator to a single uppercase letter.
        /// </summary>
        private static string NormalizeSection(string s)
            => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().ToUpperInvariant();

        /// <summary>
        /// Returns the configured FHIR ID length or a safe default of 60.
        /// </summary>
        private int SafeFhirIdLen() => clarityClientOptions.FhirIdMaxLength > 0 ? clarityClientOptions.FhirIdMaxLength : 60;
    }
}
