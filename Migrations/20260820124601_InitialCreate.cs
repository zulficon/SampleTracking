using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SampleAnalysisTracking.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_codes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "\"role\" IN (1, 2, 3, 4)");
                });

            migrationBuilder.CreateTable(
                name: "analysis_parameters",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    analysis_code_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference_min = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    reference_max = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_parameters", x => x.id);
                    table.CheckConstraint("ck_analysis_parameters_reference_range", "\"reference_min\" IS NULL OR \"reference_max\" IS NULL OR \"reference_min\" <= \"reference_max\"");
                    table.ForeignKey(
                        name: "FK_analysis_parameters_analysis_codes_analysis_code_id",
                        column: x => x.analysis_code_id,
                        principalTable: "analysis_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "samples",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sample_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sample_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    location_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    assigned_to = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_samples", x => x.id);
                    table.CheckConstraint("ck_samples_status", "\"status\" IN (1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_samples_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_samples_users_assigned_to",
                        column: x => x.assigned_to,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_samples_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sample_analyses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sample_id = table.Column<long>(type: "bigint", nullable: false),
                    analysis_code_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    result_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_by = table.Column<long>(type: "bigint", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    started_by = table.Column<long>(type: "bigint", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<long>(type: "bigint", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sample_analyses", x => x.id);
                    table.CheckConstraint("ck_sample_analyses_status", "\"status\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_sample_analyses_analysis_codes_analysis_code_id",
                        column: x => x.analysis_code_id,
                        principalTable: "analysis_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sample_analyses_samples_sample_id",
                        column: x => x.sample_id,
                        principalTable: "samples",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sample_analyses_users_completed_by",
                        column: x => x.completed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sample_analyses_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sample_analyses_users_started_by",
                        column: x => x.started_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sample_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sample_id = table.Column<long>(type: "bigint", nullable: false),
                    old_status = table.Column<int>(type: "integer", nullable: true),
                    new_status = table.Column<int>(type: "integer", nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sample_history", x => x.id);
                    table.CheckConstraint("ck_sample_history_new_status", "\"new_status\" IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("ck_sample_history_old_status", "\"old_status\" IS NULL OR \"old_status\" IN (1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_sample_history_samples_sample_id",
                        column: x => x.sample_id,
                        principalTable: "samples",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sample_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "analysis_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sample_analysis_id = table.Column<long>(type: "bigint", nullable: false),
                    analysis_parameter_id = table.Column<long>(type: "bigint", nullable: false),
                    numeric_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference_min = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    reference_max = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    result_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    measured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    entered_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_results", x => x.id);
                    table.CheckConstraint("ck_analysis_results_reference_range", "\"reference_min\" IS NULL OR \"reference_max\" IS NULL OR \"reference_min\" <= \"reference_max\"");
                    table.ForeignKey(
                        name: "FK_analysis_results_analysis_parameters_analysis_parameter_id",
                        column: x => x.analysis_parameter_id,
                        principalTable: "analysis_parameters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_analysis_results_sample_analyses_sample_analysis_id",
                        column: x => x.sample_analysis_id,
                        principalTable: "sample_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analysis_results_users_entered_by",
                        column: x => x.entered_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sample_analysis_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sample_analysis_id = table.Column<long>(type: "bigint", nullable: false),
                    old_status = table.Column<int>(type: "integer", nullable: true),
                    new_status = table.Column<int>(type: "integer", nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sample_analysis_history", x => x.id);
                    table.CheckConstraint("ck_sample_analysis_history_new_status", "\"new_status\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("ck_sample_analysis_history_old_status", "\"old_status\" IS NULL OR \"old_status\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_sample_analysis_history_sample_analyses_sample_analysis_id",
                        column: x => x.sample_analysis_id,
                        principalTable: "sample_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sample_analysis_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_codes_code",
                table: "analysis_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_parameters_analysis_code_id_code",
                table: "analysis_parameters",
                columns: new[] { "analysis_code_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_analysis_parameter_id",
                table: "analysis_results",
                column: "analysis_parameter_id");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_entered_by",
                table: "analysis_results",
                column: "entered_by");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_sample_analysis_id_analysis_parameter_id",
                table: "analysis_results",
                columns: new[] { "sample_analysis_id", "analysis_parameter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_name",
                table: "locations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sample_analyses_analysis_code_id",
                table: "sample_analyses",
                column: "analysis_code_id");

            migrationBuilder.CreateIndex(
                name: "IX_sample_analyses_completed_by",
                table: "sample_analyses",
                column: "completed_by");

            migrationBuilder.CreateIndex(
                name: "IX_sample_analyses_requested_by",
                table: "sample_analyses",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_sample_analyses_sample_id_analysis_code_id",
                table: "sample_analyses",
                columns: new[] { "sample_id", "analysis_code_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sample_analyses_started_by",
                table: "sample_analyses",
                column: "started_by");

            migrationBuilder.CreateIndex(
                name: "IX_sample_analyses_status",
                table: "sample_analyses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_sample_analysis_history_changed_by",
                table: "sample_analysis_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_sample_analysis_history_sample_analysis_id_changed_at",
                table: "sample_analysis_history",
                columns: new[] { "sample_analysis_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sample_history_changed_by",
                table: "sample_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_sample_history_sample_id_changed_at",
                table: "sample_history",
                columns: new[] { "sample_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_samples_assigned_to",
                table: "samples",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "IX_samples_created_by",
                table: "samples",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_samples_location_id",
                table: "samples",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_samples_sample_code",
                table: "samples",
                column: "sample_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_samples_status",
                table: "samples",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_results");

            migrationBuilder.DropTable(
                name: "sample_analysis_history");

            migrationBuilder.DropTable(
                name: "sample_history");

            migrationBuilder.DropTable(
                name: "analysis_parameters");

            migrationBuilder.DropTable(
                name: "sample_analyses");

            migrationBuilder.DropTable(
                name: "analysis_codes");

            migrationBuilder.DropTable(
                name: "samples");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
