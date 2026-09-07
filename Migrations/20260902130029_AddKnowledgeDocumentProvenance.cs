using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleAnalysisTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_reference",
                table: "knowledge_documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_version",
                table: "knowledge_documents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_documents_verified_source_reference",
                table: "knowledge_documents",
                sql: "\"source_status\" <> 2 OR NULLIF(BTRIM(\"source_reference\"), '') IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_documents_verified_source_reference",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "source_reference",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "source_version",
                table: "knowledge_documents");
        }
    }
}
