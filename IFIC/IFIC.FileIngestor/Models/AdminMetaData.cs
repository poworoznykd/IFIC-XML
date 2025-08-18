/************************************************************************************
* FILE          : AdminMetadata.cs
* PROJECT       : IFIC.FileIngestor
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-17
* DESCRIPTION   :
*   Strongly-typed view of [ADMIN] metadata used for routing and context.
************************************************************************************/

namespace IFIC.FileIngestor.Models
{
    /// <summary>
    /// Parsed values from the [ADMIN] section of the queued data file.
    /// Only Fiscal and Quarter are required for routing; others are optional.
    /// </summary>
    public sealed class AdminMetadata
    {
        // Patient
        public string? FhirPatID { get; init; }
        public string? FhirPatKey { get; init; }
        public string? PatOper { get; init; }

        // Encounter
        public string? FhirEncID { get; init; }
        public string? FhirEncKey { get; init; }
        public string? EncOper { get; init; }

        // Assessment
        public string? FhirAsmID { get; init; }
        public string? RecId { get; init; }
        public string? AsmOper { get; init; }

        // Assessment type and routing
        public string? AsmType { get; init; }

        /// <summary>Required for routing. Example: "2025".</summary>
        public string? Fiscal { get; init; }

        /// <summary>Required for routing. Example: "Q3-2025".</summary>
        public string? Quarter { get; init; }
    }
}
