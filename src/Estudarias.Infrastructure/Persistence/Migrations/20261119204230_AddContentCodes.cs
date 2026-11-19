using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estudarias.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_flashcard_topic_id",
                table: "flashcard");

            migrationBuilder.DropIndex(
                name: "ix_exercise_lesson_id",
                table: "exercise");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "flashcard",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "exercise",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ux_flashcard_topic_id_code",
                table: "flashcard",
                columns: new[] { "topic_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_exercise_lesson_id_code",
                table: "exercise",
                columns: new[] { "lesson_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_flashcard_topic_id_code",
                table: "flashcard");

            migrationBuilder.DropIndex(
                name: "ux_exercise_lesson_id_code",
                table: "exercise");

            migrationBuilder.DropColumn(
                name: "code",
                table: "flashcard");

            migrationBuilder.DropColumn(
                name: "code",
                table: "exercise");

            migrationBuilder.CreateIndex(
                name: "ix_flashcard_topic_id",
                table: "flashcard",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_exercise_lesson_id",
                table: "exercise",
                column: "lesson_id");
        }
    }
}
