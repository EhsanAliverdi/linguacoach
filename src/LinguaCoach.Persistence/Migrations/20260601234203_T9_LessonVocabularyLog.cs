using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    // Well-known seed GUIDs (must match SeedData.cs)
    file static class SeedIds
    {
        internal static readonly Guid FaEnPairId = new("20000000-0000-0000-0000-000000000001");
        internal static readonly Guid DocumentControllerProfileId = new("40000000-0000-0000-0000-000000000001");
        internal static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <inheritdoc />
    public partial class T9_LessonVocabularyLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lesson_vocabulary_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vocabulary_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_number = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_vocabulary_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_lesson_vocabulary_logs_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_vocabulary_logs_vocabulary_entries_vocabulary_entry_~",
                        column: x => x.vocabulary_entry_id,
                        principalTable: "vocabulary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_vocabulary_logs_student_lesson",
                table: "lesson_vocabulary_logs",
                columns: new[] { "student_profile_id", "lesson_number" });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_vocabulary_logs_student_occurred",
                table: "lesson_vocabulary_logs",
                columns: new[] { "student_profile_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_vocabulary_logs_vocabulary_entry_id",
                table: "lesson_vocabulary_logs",
                column: "vocabulary_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_vocabulary_logs_student_entry_lesson",
                table: "lesson_vocabulary_logs",
                columns: new[] { "student_profile_id", "vocabulary_entry_id", "lesson_number" },
                unique: true);

            // SM-2 repetition counter on vocabulary_entries
            migrationBuilder.AddColumn<int>(
                name: "repetition_count",
                table: "vocabulary_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ── Seed CurriculumWordList for Document Controller (Persian→English) ──
            var words = new[]
            {
                // (id, word, definition, exampleSentence, priority, tags)
                (new Guid("60000000-0000-0000-0000-000000000001"), "approval", "Official agreement or permission for something", "The drawing requires manager approval before construction begins.", 1, "email,formal"),
                (new Guid("60000000-0000-0000-0000-000000000002"), "submittal", "A formal document or package submitted for review", "Please review the submittal package attached to this email.", 2, "document-control,formal"),
                (new Guid("60000000-0000-0000-0000-000000000003"), "revision", "A corrected or updated version of a document", "Revision 3 incorporates the client's latest comments.", 3, "document-control"),
                (new Guid("60000000-0000-0000-0000-000000000004"), "pending", "Awaiting action or decision; not yet resolved", "The approval is still pending from the project manager.", 4, "status,email"),
                (new Guid("60000000-0000-0000-0000-000000000005"), "outstanding", "Not yet done or resolved; still requiring attention", "There are two outstanding items on the review register.", 5, "status,formal"),
                (new Guid("60000000-0000-0000-0000-000000000006"), "transmittal", "A cover document that records what is being sent", "Please find the transmittal letter enclosed with the drawings.", 6, "document-control,formal"),
                (new Guid("60000000-0000-0000-0000-000000000007"), "compliance", "Meeting required standards, rules, or specifications", "The installation must be in full compliance with ISO 9001.", 7, "formal,technical"),
                (new Guid("60000000-0000-0000-0000-000000000008"), "RFI", "Request for Information — a formal query raised on a project", "We raised an RFI to clarify the foundation depth specification.", 8, "document-control,abbreviation"),
                (new Guid("60000000-0000-0000-0000-000000000009"), "document controller", "A professional responsible for managing project documents", "The document controller maintains the master document register.", 9, "role,document-control"),
                (new Guid("60000000-0000-0000-0000-000000000010"), "follow up", "To make contact again to check on progress", "I am writing to follow up on my email sent last week.", 10, "email,phrase"),
                (new Guid("60000000-0000-0000-0000-000000000011"), "clarification", "An explanation that makes something clearer", "Could you provide clarification on the scope of work?", 11, "email,formal"),
                (new Guid("60000000-0000-0000-0000-000000000012"), "superseded", "Replaced by a newer version; no longer current", "Drawing Rev A is superseded by Rev B issued today.", 12, "document-control,status"),
                (new Guid("60000000-0000-0000-0000-000000000013"), "distribution list", "The list of people who receive a copy of a document", "Please add the site engineer to the distribution list.", 13, "document-control"),
                (new Guid("60000000-0000-0000-0000-000000000014"), "scope of work", "A detailed description of the work to be performed", "The scope of work is defined in Appendix A of the contract.", 14, "contract,formal"),
                (new Guid("60000000-0000-0000-0000-000000000015"), "punch list", "A list of tasks remaining before project completion", "The contractor must close all punch list items by Friday.", 15, "project-management"),
                (new Guid("60000000-0000-0000-0000-000000000016"), "as-built", "Drawings that reflect actual construction as completed", "As-built drawings must be submitted within 30 days of completion.", 16, "document-control,technical"),
                (new Guid("60000000-0000-0000-0000-000000000017"), "stakeholder", "A person or group with an interest in a project", "All key stakeholders must sign off on the design.", 17, "project-management,formal"),
                (new Guid("60000000-0000-0000-0000-000000000018"), "milestone", "A significant event or stage in a project's progress", "Handover of the completed building is the final milestone.", 18, "project-management"),
                (new Guid("60000000-0000-0000-0000-000000000019"), "correspondence", "Letters, emails, and official communications", "All project correspondence must be logged in the DMS.", 19, "document-control,formal"),
                (new Guid("60000000-0000-0000-0000-000000000020"), "drawing register", "A log tracking all project drawings and their revision status", "Please check the drawing register before issuing new copies.", 20, "document-control"),
                (new Guid("60000000-0000-0000-0000-000000000021"), "specification", "A detailed technical description of requirements", "The specification calls for stainless-steel fixings throughout.", 21, "technical,document-control"),
                (new Guid("60000000-0000-0000-0000-000000000022"), "endorsement", "A signature or mark indicating official approval", "The drawing requires endorsement from the structural engineer.", 22, "formal,approval"),
                (new Guid("60000000-0000-0000-0000-000000000023"), "interim", "Temporary or provisional, until something permanent is in place", "An interim payment certificate was issued this week.", 23, "formal,project-management"),
                (new Guid("60000000-0000-0000-0000-000000000024"), "variation order", "A formal instruction to change the contracted scope or cost", "A variation order was raised to cover the additional earthworks.", 24, "contract,formal"),
                (new Guid("60000000-0000-0000-0000-000000000025"), "handover", "The formal transfer of a completed project to the client", "Handover documentation must be complete before practical completion.", 25, "project-management,formal"),
            };

            foreach (var (id, word, definition, example, priority, tags) in words)
            {
                migrationBuilder.InsertData(
                    table: "curriculum_word_lists",
                    columns: new[] { "id", "career_profile_id", "language_pair_id", "word", "definition", "example_sentence", "priority", "tags", "created_at" },
                    values: new object[]
                    {
                        id,
                        SeedIds.DocumentControllerProfileId,
                        SeedIds.FaEnPairId,
                        word,
                        definition,
                        example,
                        priority,
                        tags,
                        SeedIds.SeedDate
                    });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "repetition_count", table: "vocabulary_entries");
            migrationBuilder.DropTable(name: "lesson_vocabulary_logs");

            for (var i = 1; i <= 25; i++)
                migrationBuilder.DeleteData("curriculum_word_lists", "id", new Guid($"60000000-0000-0000-0000-{i:D12}"));
        }
    }
}
