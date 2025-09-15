/*
 *  FILE          : IElementMapping.cs
 *  PROJECT       : IFIC.ClarityClient (shared contract)
 *  DESCRIPTION   :
 *    Canonical mapping contract for interRAI element identifiers (iCodes)
 *    to their Element Name and Section letter.
 *
 *  PASTE LOCATION : IFIC.ClarityClient/IElementMapping.cs (new file)
 */

namespace IFIC.ClarityClient
{
    /// <summary>
    /// Resolves interRAI iCode tokens (e.g., iA5a) to a Section letter and Element Name.
    /// </summary>
    public interface IElementMapping
    {
        /// <summary>
        /// Attempts to resolve an interRAI iCode to (Section, Element Name).
        /// </summary>
        /// <param name="iCode">interRAI element token (e.g., "iA5a").</param>
        /// <param name="sectionLetter">Out: single letter A..Z designating Section_&lt;X&gt;.</param>
        /// <param name="elementName">Out: human‑friendly Element Name from the mapping.</param>
        /// <returns><c>true</c> when a mapping exists; otherwise <c>false</c>.</returns>
        bool TryResolveICode(string iCode, out string sectionLetter, out string elementName);
    }
}
