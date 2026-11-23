using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estudarias.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "exercise",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "alternative",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // As linhas que já existem ganham a posição pela ordem do id, a única que o banco tem: não é a ordem do YAML (o id não a
            // guarda), então recarregar o conteúdo (comando seed) a corrige. Sem este passo, o 0 do padrão violaria as CHECKs abaixo.
            migrationBuilder.Sql(
                """
                UPDATE exercise SET position = numbered.position
                FROM (SELECT id, row_number() OVER (PARTITION BY lesson_id ORDER BY id) AS position FROM exercise) AS numbered
                WHERE exercise.id = numbered.id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE alternative SET position = numbered.position
                FROM (SELECT id, row_number() OVER (PARTITION BY exercise_id ORDER BY id) AS position FROM alternative) AS numbered
                WHERE alternative.id = numbered.id;
                """);

            // O 0 era só para as linhas antigas: daqui em diante toda gravação informa a posição.
            migrationBuilder.AlterColumn<int>(
                name: "position",
                table: "exercise",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "position",
                table: "alternative",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_exercise_position",
                table: "exercise",
                sql: "position >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_alternative_position",
                table: "alternative",
                sql: "position >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_position",
                table: "exercise");

            migrationBuilder.DropCheckConstraint(
                name: "ck_alternative_position",
                table: "alternative");

            migrationBuilder.DropColumn(
                name: "position",
                table: "exercise");

            migrationBuilder.DropColumn(
                name: "position",
                table: "alternative");
        }
    }
}
