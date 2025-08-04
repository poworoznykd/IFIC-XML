/************************************************************************************
* FILE          : ParsedFlatFile.cs
* PROJECT       : IFIC-XML
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   : Represents parsed data from Clarity LTCF flat files with separate 
*                 dictionaries for Admin, Patient, Encounter, and Assessment sections.
************************************************************************************/

using System.Collections.Generic;

namespace IFIC.FileIngestor.Models
{
    /// <summary>
    /// Represents all parsed sections from a Clarity LTCF flat file.
    /// </summary>
    public class ParsedFlatFile
    {
        /// <summary>
        /// Contains key-value pairs from the ADMIN section.
        /// </summary>
        public Dictionary<string, string> Admin { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Contains key-value pairs from the PATIENT section.
        /// </summary>
        public Dictionary<string, string> Patient { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Contains key-value pairs from the ENCOUNTER section.
        /// </summary>
        public Dictionary<string, string> Encounter { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Contains key-value pairs grouped by Assessment section name (e.g., SECTION A, SECTION B).
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> AssessmentSections { get; set; } =
            new Dictionary<string, Dictionary<string, string>>();
    }
}
