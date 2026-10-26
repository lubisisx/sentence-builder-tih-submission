using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentenceBuilder.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sentence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 10099, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sentence", x => x.Id);
                    table.CheckConstraint("CK_Sentence_Text_Length", "DATALENGTH([Text]) <= 20198");
                    table.CheckConstraint("CK_Sentence_Text_NotBlank", "LEN(LTRIM(RTRIM([Text]))) > 0");
                    table.CheckConstraint("CK_Sentence_Timestamps", "[UpdatedAt] >= [CreatedAt]");
                });

            migrationBuilder.CreateTable(
                name: "WordType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordType", x => x.Id);
                    table.CheckConstraint("CK_WordType_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Word",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WordTypeId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Word", x => x.Id);
                    table.CheckConstraint("CK_Word_Value_NotBlank", "LEN(LTRIM(RTRIM([Value]))) > 0");
                    table.ForeignKey(
                        name: "FK_Word_WordType_WordTypeId",
                        column: x => x.WordTypeId,
                        principalTable: "WordType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SentenceWord",
                columns: table => new
                {
                    SentenceId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    WordId = table.Column<int>(type: "int", nullable: false),
                    WordText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentenceWord", x => new { x.SentenceId, x.Position });
                    table.CheckConstraint("CK_SentenceWord_Position", "[Position] >= 0");
                    table.CheckConstraint("CK_SentenceWord_WordText_NotBlank", "LEN(LTRIM(RTRIM([WordText]))) > 0");
                    table.ForeignKey(
                        name: "FK_SentenceWord_Sentence_SentenceId",
                        column: x => x.SentenceId,
                        principalTable: "Sentence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SentenceWord_Word_WordId",
                        column: x => x.WordId,
                        principalTable: "Word",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sentence_CreatedAt_Id",
                table: "Sentence",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_SentenceWord_WordId",
                table: "SentenceWord",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_Word_WordTypeId_Value",
                table: "Word",
                columns: new[] { "WordTypeId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WordType_Name",
                table: "WordType",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SentenceWord");

            migrationBuilder.DropTable(
                name: "Sentence");

            migrationBuilder.DropTable(
                name: "Word");

            migrationBuilder.DropTable(
                name: "WordType");
        }
    }
}
