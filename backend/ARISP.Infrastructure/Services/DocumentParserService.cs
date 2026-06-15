using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace ARISP.Infrastructure.Services
{
    public class DocumentParserService : IDocumentParserService
    {
        public async Task<string> ParseDocumentAsync(Stream stream, string fileExtension)
        {
            if (stream == null || stream.Length == 0)
            {
                return string.Empty;
            }

            // Ensure the stream is seekable since PdfPig and OpenXml require random access to read ZIP/PDF headers
            Stream seekableStream = stream;
            bool isTempStream = false;

            if (!stream.CanSeek)
            {
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                seekableStream = memoryStream;
                isTempStream = true;
            }
            else
            {
                seekableStream.Position = 0;
            }

            try
            {
                var normalizedExtension = fileExtension.ToLowerInvariant().Trim();

                switch (normalizedExtension)
                {
                    case ".txt":
                        return await ParseTxtAsync(seekableStream);

                    case ".pdf":
                        return ParsePdf(seekableStream);

                    case ".docx":
                        return ParseDocx(seekableStream);

                    default:
                        throw new NotSupportedException($"Định dạng file '{fileExtension}' không được hỗ trợ. Chỉ hỗ trợ .pdf, .docx, .txt");
                }
            }
            finally
            {
                if (isTempStream)
                {
                    await seekableStream.DisposeAsync();
                }
            }
        }

        private async Task<string> ParseTxtAsync(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private string ParsePdf(Stream stream)
        {
            using var document = PdfDocument.Open(stream);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }

            return sb.ToString();
        }

        private string ParseDocx(Stream stream)
        {
            using var wordDoc = WordprocessingDocument.Open(stream, isEditable: false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            // Descendants<Paragraph>() handles paragraphs in body, tables, and lists.
            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                var text = paragraph.InnerText;
                if (!string.IsNullOrEmpty(text))
                {
                    sb.AppendLine(text);
                }
            }

            return sb.ToString();
        }
    }
}