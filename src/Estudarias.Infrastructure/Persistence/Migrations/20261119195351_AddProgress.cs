using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estudarias.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attempt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alternative_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_seconds = table.Column<int>(type: "integer", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempt", x => x.id);
                    table.CheckConstraint("ck_attempt_response_seconds", "response_seconds IS NULL OR response_seconds >= 0");
                    table.ForeignKey(
                        name: "FK_attempt_exercise_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempt_student_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_topic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mastered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_topic", x => x.id);
                    table.CheckConstraint("ck_student_topic_mastered_after_unlocked", "mastered_at IS NULL OR mastered_at >= unlocked_at");
                    table.ForeignKey(
                        name: "FK_student_topic_student_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_topic_topic_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_flashcard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flashcard_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    algorithm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduling_state = table.Column<string>(type: "jsonb", nullable: false),
                    next_review_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_flashcard", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_flashcard_attempt_origin_attempt_id",
                        column: x => x.origin_attempt_id,
                        principalTable: "attempt",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_flashcard_flashcard_flashcard_id",
                        column: x => x.flashcard_id,
                        principalTable: "flashcard",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_flashcard_student_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attempt_alternative_id",
                table: "attempt",
                column: "alternative_id");

            migrationBuilder.CreateIndex(
                name: "ix_attempt_exercise_id",
                table: "attempt",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ix_attempt_student_id_exercise_id",
                table: "attempt",
                columns: new[] { "student_id", "exercise_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_flashcard_flashcard_id",
                table: "student_flashcard",
                column: "flashcard_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_flashcard_origin_attempt_id",
                table: "student_flashcard",
                column: "origin_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_flashcard_student_id_next_review_at",
                table: "student_flashcard",
                columns: new[] { "student_id", "next_review_at" });

            migrationBuilder.CreateIndex(
                name: "ux_student_flashcard_student_id_flashcard_id",
                table: "student_flashcard",
                columns: new[] { "student_id", "flashcard_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_topic_topic_id",
                table: "student_topic",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ux_student_topic_student_id_topic_id",
                table: "student_topic",
                columns: new[] { "student_id", "topic_id" },
                unique: true);

            // Escrita à mão: a alternativa é parte do agregado Exercise (owned), e o EF Core não deixa outra entidade apontar para uma
            // entidade owned, então essa chave estrangeira não existe no modelo (veja o AttemptConfiguration). Por não estar no modelo,
            // ela também não entra no snapshot, e o MigrationsTests continua valendo.
            migrationBuilder.Sql(
                "ALTER TABLE attempt ADD CONSTRAINT fk_attempt_alternative_alternative_id " +
                "FOREIGN KEY (alternative_id) REFERENCES alternative (id) ON DELETE RESTRICT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_flashcard");

            migrationBuilder.DropTable(
                name: "student_topic");

            migrationBuilder.DropTable(
                name: "attempt");
        }
    }
}
