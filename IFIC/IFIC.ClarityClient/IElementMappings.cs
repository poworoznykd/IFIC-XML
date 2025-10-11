/*
 *  FILE          : IElementMapping.cs
 *  PROJECT       : IFIC.ClarityClient (shared contract)
 *  DESCRIPTION   :
 *    Resolves interRAI iCode tokens (e.g., iA5a) to:
 *      - DB Section letter (A..Z) used for Section_<X> writes
 *      - ElementCode for display in rewritten messages (e.g., R7, A9)
 *      - ElementName (friendly text)
 */

namespace IFIC.ClarityClient
{
    /// <summary>
    /// Resolves interRAI iCode tokens (e.g., <c>iA5a</c>) to a database Section letter,
    /// a human-facing ElementCode (e.g., <c>R7</c>/<c>A9</c>), and a friendly Element Name.
    /// </summary>
    public interface IElementMapping
    {
        /// <summary>
        /// Attempts to resolve an iCode (or plain element code) to mapping details.
        /// </summary>
        /// <param name="iCode">interRAI token such as <c>iU2</c> / <c>iA9</c>, or an element code like <c>B1</c>.</param>
        /// <param name="sectionLetter">Out: single letter <c>A..Z</c> designating <c>Section_&lt;X&gt;</c> in the DB.</param>
        /// <param name="elementCode">Out: display code such as <c>R7</c>/<c>A9</c> (empty if unavailable).</param>
        /// <param name="elementName">Out: human-friendly Element Name (empty if unavailable).</param>
        /// <returns><c>true</c> if a mapping exists; otherwise <c>false</c>.</returns>
        bool TryResolveICode(string iCode, out string sectionLetter, out string elementCode, out string elementName);
    }
}
