using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleAnalysisTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentAnalysisCodeLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_document_analysis_codes",
                columns: table => new
                {
                    knowledge_document_id = table.Column<long>(type: "bigint", nullable: false),
                    analysis_code_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_document_analysis_codes", x => new { x.knowledge_document_id, x.analysis_code_id });
                    table.ForeignKey(
                        name: "FK_knowledge_document_analysis_codes_analysis_codes_analysis_c~",
                        column: x => x.analysis_code_id,
                        principalTable: "analysis_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_document_analysis_codes_knowledge_documents_knowl~",
                        column: x => x.knowledge_document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_analysis_codes_analysis_code_id",
                table: "knowledge_document_analysis_codes",
                column: "analysis_code_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_document_analysis_codes");
        }
    }
}
