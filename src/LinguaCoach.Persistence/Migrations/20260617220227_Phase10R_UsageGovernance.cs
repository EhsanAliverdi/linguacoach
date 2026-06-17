using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10R_UsageGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetStudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OldValueJson = table.Column<string>(type: "text", nullable: true),
                    NewValueJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultEnforcementMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsExpensive = table.Column<bool>(type: "boolean", nullable: false),
                    IsStudentVisible = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabledByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentUsageDaily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    AiCallCount = table.Column<int>(type: "integer", nullable: false),
                    LiveAiMinutes = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    TtsCharacters = table.Column<int>(type: "integer", nullable: false),
                    SttMinutes = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    LessonGenerations = table.Column<int>(type: "integer", nullable: false),
                    PracticeGenerations = table.Column<int>(type: "integer", nullable: false),
                    WritingEvaluations = table.Column<int>(type: "integer", nullable: false),
                    SpeakingEvaluations = table.Column<int>(type: "integer", nullable: false),
                    PreparedActivitiesCompleted = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentUsageDaily", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentUsageDaily_student_profiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeatureKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UnitsUsed = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    AudioSeconds = table.Column<decimal>(type: "numeric", nullable: true),
                    TtsCharacters = table.Column<int>(type: "integer", nullable: true),
                    SttMinutes = table.Column<decimal>(type: "numeric", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageEvents_student_profiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsagePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ScopeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsagePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentPolicyAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsagePolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPolicyAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPolicyAssignments_UsagePolicies_UsagePolicyId",
                        column: x => x.UsagePolicyId,
                        principalTable: "UsagePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentPolicyAssignments_student_profiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsagePolicyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsagePolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TrackingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EnforcementMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DailyLimit = table.Column<long>(type: "bigint", nullable: true),
                    WeeklyLimit = table.Column<long>(type: "bigint", nullable: true),
                    MonthlyLimit = table.Column<long>(type: "bigint", nullable: true),
                    DailyCostLimit = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    MonthlyCostLimit = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    WarningThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsagePolicyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsagePolicyRules_UsagePolicies_UsagePolicyId",
                        column: x => x.UsagePolicyId,
                        principalTable: "UsagePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ActorAdminUserId_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "ActorAdminUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_TargetStudentId_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "TargetStudentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureDefinitions_Key",
                table: "FeatureDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPolicyAssignments_StudentProfileId_IsActive",
                table: "StudentPolicyAssignments",
                columns: new[] { "StudentProfileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPolicyAssignments_UsagePolicyId",
                table: "StudentPolicyAssignments",
                column: "UsagePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentUsageDaily_StudentProfileId_Date",
                table: "StudentUsageDaily",
                columns: new[] { "StudentProfileId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_StudentProfileId_CreatedAt",
                table: "UsageEvents",
                columns: new[] { "StudentProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_StudentProfileId_FeatureKey_CreatedAt",
                table: "UsageEvents",
                columns: new[] { "StudentProfileId", "FeatureKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UsagePolicyRules_UsagePolicyId_FeatureKey",
                table: "UsagePolicyRules",
                columns: new[] { "UsagePolicyId", "FeatureKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "FeatureDefinitions");

            migrationBuilder.DropTable(
                name: "StudentPolicyAssignments");

            migrationBuilder.DropTable(
                name: "StudentUsageDaily");

            migrationBuilder.DropTable(
                name: "UsageEvents");

            migrationBuilder.DropTable(
                name: "UsagePolicyRules");

            migrationBuilder.DropTable(
                name: "UsagePolicies");
        }
    }
}
