using ARISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    /// <summary>
    /// GIN full-text index trên document_chunks.chunk_text — phục vụ nhánh SPARSE của Hybrid RAG
    /// (RAG service Python dùng to_tsvector('simple', chunk_text) @@ to_tsquery + ts_rank).
    /// Chỉ là index (không đổi model) nên không cần cập nhật ModelSnapshot. Idempotent.
    /// </summary>
    [DbContext(typeof(ARISPDbContext))]
    [Migration("20260626000000_AddDocumentChunksFtsIndex")]
    public partial class AddDocumentChunksFtsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_document_chunks_chunk_text_fts " +
                "ON document_chunks USING GIN (to_tsvector('simple', chunk_text));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_document_chunks_chunk_text_fts;");
        }
    }
}
