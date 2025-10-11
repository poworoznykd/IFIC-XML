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
        // PASS-path updaters
        // -------------------------------------------------------------

        public async Task<int> DeleteFhirEncounterIDAsync(string fhirEncId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fhirEncId)) return 0;
            const string sql = @"UPDATE dbo.fhirEncounter SET fhirEncID='' WHERE fhirEncId = @fhirEncId;";
            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirEncID", SqlDbType.NVarChar, SafeFhirIdLen()) { Value = (object?)fhirEncId ?? DBNull.Value });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Writes the FHIR Patient ID to <c>dbo.fhirPatient</c> for the specified key.
        /// </summary>
        /// <param name="fhirPatKey">FHIR patient row key (string form). Ignored if null/empty.</param>
        /// <param name="fhirPatId">FHIR Patient resource ID to store (null becomes DB NULL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected (0 or 1 expected).</returns>
        public async Task<int> UpdatePatientAsync(string? fhirPatKey, string fhirPatId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fhirPatKey)) return 0;
            const string sql = @"UPDATE dbo.fhirPatient SET fhirPatID=@fhirPatID WHERE CAST(fhirPatKey AS NVARCHAR(64))=@fhirPatKey;";
            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirPatID", SqlDbType.NVarChar, SafeFhirIdLen()) { Value = (object?)fhirPatId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@fhirPatKey", SqlDbType.NVarChar, 64) { Value = fhirPatKey!.Trim() });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Writes the FHIR Encounter ID to <c>dbo.fhirEncounter</c> for the specified key.
        /// </summary>
        /// <param name="fhirEncKey">FHIR encounter row key (string form). Ignored if null/empty.</param>
        /// <param name="fhirEncId">FHIR Encounter resource ID to store (null becomes DB NULL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected (0 or 1 expected).</returns>
        public async Task<int> UpdateEncounterAsync(string? fhirEncKey, string fhirEncId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fhirEncKey)) return 0;
            const string sql = @"UPDATE dbo.fhirEncounter SET fhirEncID=@fhirEncID WHERE CAST(fhirEncKey AS NVARCHAR(64))=@fhirEncKey;";
            using var command = await CreateCommandAsync(sql, cancellationToken);
            command.Parameters.Add(new SqlParameter("@fhirEncID", SqlDbType.NVarChar, SafeFhirIdLen()) { Value = (object?)fhirEncId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@fhirEncKey", SqlDbType.NVarChar, 64) { Value = fhirEncKey!.Trim() });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Writes the FHIR Assessment ID to <c>dbo.Assessments</c> for the provided <paramref name="recId"/>.
        /// </summary>
        /// <param name="recId">Assessment record id (string form; parsed to INT).</param>
        /// <param name="fhirAsmId">FHIR Assessment resource ID to store (null becomes DB NULL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected (0 or 1 expected).</returns>
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

        /// <summary>
        /// Updates <c>dbo.SubmissionStatus.status</c> for the supplied assessment id.
        /// </summary>
        /// <param name="recId">Assessment record id (string form; parsed to INT).</param>
        /// <param name="status">New submission status value (e.g., "PASS", "FAIL").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected (0 or 1 expected).</returns>
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

        /// <summary>
        /// If the assessment's patient currently has <c>Status='discharged'</c>, set it to <c>'Active'</c>.
        /// </summary>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected (0 or 1 expected).</returns>
        /// <exception cref="InvalidOperationException">Thrown if more than one row is updated.</exception>
        public async Task<int> MarkPatientActiveByRecIdAsync(int recId, CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE dbo.patients
                SET    Status = 'Active'
                WHERE  Status = 'discharged'
                AND  uid = (SELECT uid FROM dbo.Assessments WHERE rec_id = @rec_id);";

            using var cmd = await CreateCommandAsync(sql, cancellationToken);
            cmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (affected > 1)
                throw new InvalidOperationException($"Expected to update at most 1 row, but updated {affected} for rec_id {recId}.");

            return affected; // 0 (no change) or 1 (reactivated)
        }

        /// <summary>
        /// Appends (or creates) an HTML note to <c>dbo.Section_&lt;X&gt;.notes</c> for the given <paramref name="recId"/>.
        /// </summary>
        /// <param name="sectionLetter">Target section letter (A..Z).</param>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="noteHtml">HTML snippet to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected by the update.</returns>
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

            // 2) Append per spec: existing + "<br>" + new
            // NOTE:  Do not need to append a <br> before the existing "current" as that string will already have the 
            //        required <br>, etc that it needs
            // ** I believe that not only do you need a <br> to start the note from CIHI, but the note must
            //    also begin with the phrase "Item: {section}" 
            //    for example   "<br>Item: G2b - blah blah blah  <--- where blah blah is the text from CIHI
            //
            // ** if you don't know or have the actual item (element) name - then use the "preamble" string
            //
            // Sorry Darryl - Clarity is a piece of shit and the errors originally were all coded by me, by hand
            //                and I didn't realize until now that I formatted them in the :
            //        "<br>Item: {section}" format  -- YIKES!!!
            // I built my "generic" error display around the fact that all errors must begin with this format!!
            // This is the way they are parsed out !! <<GULP>>  (Not too generic, eh?)
            //    I'm so sorry!! 
            //
            string preamble = "<br>Item: " + section + " - ";
            string updated = string.IsNullOrWhiteSpace(current) ? preamble + noteHtml : (current + preamble + noteHtml);

            // 3) Write back
            string updateSql = $"UPDATE dbo.Section_{section} SET notes=@notes WHERE rec_id=@rec_id";
            using var updateCmd = await CreateCommandAsync(updateSql, cancellationToken);
            updateCmd.Parameters.Add(new SqlParameter("@notes", SqlDbType.NVarChar, -1) { Value = (object?)updated ?? DBNull.Value });
            updateCmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });
            return await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Sets <c>dbo.ccrsSectionState.Section_&lt;X&gt;</c> to the supplied state for the given <paramref name="recId"/>.
        /// </summary>
        /// <param name="sectionLetter">Target section letter (A..Z).</param>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="state">State value to store (e.g., "2").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected by the update.</returns>
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

        /// <summary>
        /// Marks the assessment as <c>Status='Incomplete'</c> and <c>transmit='NO'</c> for the given <paramref name="recId"/>.
        /// </summary>
        /// <param name="recId">Assessment record id (INT).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Rows affected by the update.</returns>
        public async Task<int> MarkAssessmentIncompleteNotTransmittedAsync(int recId, CancellationToken cancellationToken)
        {
            const string sql = @"UPDATE dbo.Assessments SET Status='Incomplete', transmit='NO' WHERE rec_id=@rec_id";
            using var cmd = await CreateCommandAsync(sql, cancellationToken);
            cmd.Parameters.Add(new SqlParameter("@rec_id", SqlDbType.Int) { Value = recId });
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Executes SELECT 1 to verify connectivity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the database responds correctly; otherwise <c>false</c>.</returns>
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
        /// <param name="sql">SQL text to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An initialized <see cref="SqlCommand"/> bound to the shared connection.</returns>
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
        /// <param name="s">Input section string.</param>
        /// <returns>Uppercase single-letter representation, or empty string if input is null/whitespace.</returns>
        private static string NormalizeSection(string s)
            => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().ToUpperInvariant();

        /// <summary>
        /// Returns the configured FHIR ID length or a safe default of 60.
        /// </summary>
        /// <returns>Max NVARCHAR length for FHIR IDs.</returns>
        private int SafeFhirIdLen() => clarityClientOptions.FhirIdMaxLength > 0 ? clarityClientOptions.FhirIdMaxLength : 60;
    }
}
