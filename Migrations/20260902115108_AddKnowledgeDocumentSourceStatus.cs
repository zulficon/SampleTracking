using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleAnalysisTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentSourceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "source_status",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_documents_source_status",
                table: "knowledge_documents",
                sql: "\"source_status\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_documents_source_status",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "source_status",
                table: "knowledge_documents");
        }
    }
}
