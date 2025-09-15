/*  
 * FILE          : ClarityClientOptions.cs
 * PROJECT       : IFIC.ClarityClient
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-09-03
 * DESCRIPTION   :
 *   Central configuration for my Clarity/LTCF DB client. I keep the connection
 *   string and size/time limits here so I can tune them per environment without
 *   editing code. Defaults align with my LTCFTest DDL (IDs nvarchar(60), status nvarchar(10)).
 */

using System.ComponentModel.DataAnnotations;

namespace IFIC.ClarityClient
{
    public sealed class ClarityClientOptions
    {
        [Required, MinLength(1)]
        public string ConnectionString { get; set; } = string.Empty;

        // Defaults match LTCFTest: fhirPatID/fhirEncID/fhirAsmID are nvarchar(60)
        [Range(1, 4000)]
        public int FhirIdMaxLength { get; set; } = 60;

        // SubmissionStatus.status is nvarchar(10) → fits PASS/FAIL cleanly
        [Range(1, 4000)]
        public int StatusMaxLength { get; set; } = 10;

        // Short UPDATEs should complete quickly; adjust if test boxes are slow
        [Range(1, 600)]
        public int CommandTimeoutSec { get; set; } = 15;

        [Required, MinLength(1)]
        public string ElementMappingPath { get; set; } = string.Empty;
    }
}
