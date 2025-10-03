/*  
 * FILE          : AdminMetadataKeys.cs
 * PROJECT       : IFIC.FileIngestor
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-09-03
 * DESCRIPTION   :
 *   TryGet-style helpers that treat keys as strings. I only verify that a key
 *   is present/non-whitespace; callers decide how to use it.
 */

using IFIC.FileIngestor.Models;

namespace IFIC.FileIngestor
{
    public static class AdminMetadataKeys
    {
        //
        // FUNCTION      : TryGetFhirPatKey
        // DESCRIPTION   : Extracts AdminMetadata.FhirPatKey as a non-empty string.
        //
        public static bool TryGetFhirPatKey(AdminMetadata admin, out string key)
        {
            key = admin?.FhirPatKey?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(key);
        }

        //
        // FUNCTION      : TryGetFhirEncKey
        // DESCRIPTION   : Extracts AdminMetadata.FhirEncKey as a non-empty string.
        //
        public static bool TryGetFhirEncKey(AdminMetadata admin, out string key)
        {
            key = admin?.FhirEncKey?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(key);
        }

        //
        // FUNCTION      : TryGetRecId
        // DESCRIPTION   : Extracts AdminMetadata.RecId as a non-empty string.
        //
        public static bool TryGetRecId(AdminMetadata admin, out string key)
        {
            key = admin?.RecId?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(key);
        }
    }
}
