using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estudarias.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "topic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "error_type",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    feedback = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_error_type", x => x.id);
                    table.ForeignKey(
                        name: "FK_error_type_topic_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson", x => x.id);
                    table.CheckConstraint("ck_lesson_position", "position >= 1");
                    table.ForeignKey(
                        name: "FK_lesson_topic_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "topic_prerequisite",
                columns: table => new
                {
                    prerequisite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_prerequisite", x => new { x.topic_id, x.prerequisite_id });
                    table.CheckConstraint("ck_topic_prerequisite_not_self", "topic_id <> prerequisite_id");
                    table.ForeignKey(
                        name: "FK_topic_prerequisite_topic_prerequisite_id",
                        column: x => x.prerequisite_id,
                        principalTable: "topic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_topic_prerequisite_topic_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "flashcard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_type_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flashcard", x => x.id);
                    table.CheckConstraint("ck_flashcard_error_type", "(origin = 'generated_by_error') = (error_type_id IS NOT NULL)");
                    table.CheckConstraint("ck_flashcard_origin", "origin IN ('manual', 'generated_by_error')");
                    table.ForeignKey(
                        name: "FK_flashcard_error_type_error_type_id",
                        column: x => x.error_type_id,
                        principalTable: "error_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flashcard_topic_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise", x => x.id);
                    table.CheckConstraint("ck_exercise_difficulty", "difficulty IN ('easy', 'medium', 'hard')");
                    table.ForeignKey(
                        name: "FK_exercise_lesson_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lesson",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alternative",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    error_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alternative", x => x.id);
                    table.CheckConstraint("ck_alternative_error_type", "(is_correct AND error_type_id IS NULL) OR (NOT is_correct AND error_type_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_alternative_error_type_error_type_id",
                        column: x => x.error_type_id,
                        principalTable: "error_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alternative_exercise_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alternative_error_type_id",
                table: "alternative",
                column: "error_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_alternative_exercise_id",
                table: "alternative",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ux_alternative_exercise_id_correct",
                table: "alternative",
                column: "exercise_id",
                unique: true,
                filter: "is_correct");

            migrationBuilder.CreateIndex(
                name: "ux_error_type_topic_id_code",
                table: "error_type",
                columns: new[] { "topic_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exercise_lesson_id",
                table: "exercise",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_flashcard_topic_id",
                table: "flashcard",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ux_flashcard_error_type_id_generated",
                table: "flashcard",
                column: "error_type_id",
                unique: true,
                filter: "origin = 'generated_by_error'");

            migrationBuilder.CreateIndex(
                name: "ux_lesson_topic_id_position",
                table: "lesson",
                columns: new[] { "topic_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_topic_slug",
                table: "topic",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topic_prerequisite_prerequisite_id",
                table: "topic_prerequisite",
                column: "prerequisite_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alternative");

            migrationBuilder.DropTable(
                name: "flashcard");

            migrationBuilder.DropTable(
                name: "topic_prerequisite");

            migrationBuilder.DropTable(
                name: "exercise");

            migrationBuilder.DropTable(
                name: "error_type");

            migrationBuilder.DropTable(
                name: "lesson");

            migrationBuilder.DropTable(
                name: "topic");
        }
    }
}
