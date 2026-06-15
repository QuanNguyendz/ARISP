using System.IO;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    /// <summary>
    /// Service to parse document text from CVs or Job Descriptions (PDF, DOCX, TXT).
    /// </summary>
    public interface IDocumentParserService
    {
        /// <summary>
        /// Parses text content from a document stream based on its file extension.
        /// </summary>
        /// <param name="stream">The document file stream.</param>
        /// <param name="fileExtension">The file extension, including the dot (e.g., ".pdf", ".docx", ".txt").</param>
        /// <returns>The extracted plain text.</returns>
        Task<string> ParseDocumentAsync(Stream stream, string fileExtension);
    }
}
