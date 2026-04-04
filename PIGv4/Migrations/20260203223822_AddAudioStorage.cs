using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIGv4.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Config",
                columns: table => new
                {
                    ConfigId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    MusicDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    LogFile = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PlayListDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Editor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Edited = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Config", x => x.ConfigId);
                });

            migrationBuilder.CreateTable(
                name: "ImportError",
                columns: table => new
                {
                    ImportErrorId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Edited = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Editor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportError", x => x.ImportErrorId);
                });

            migrationBuilder.CreateTable(
                name: "Log",
                columns: table => new
                {
                    LogIdentifier = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Exception = table.Column<string>(type: "TEXT", nullable: true),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MethodName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Editor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Edited = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Log", x => x.LogIdentifier);
                });

            migrationBuilder.CreateTable(
                name: "MP3Genre",
                columns: table => new
                {
                    GenreId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GenreName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Edited = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Editor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MP3Genre", x => x.GenreId);
                });

            migrationBuilder.CreateTable(
                name: "Playlist",
                columns: table => new
                {
                    PlaylistId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Minimum = table.Column<int>(type: "INTEGER", nullable: false),
                    StartYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EndYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Editor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Edited = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlist", x => x.PlaylistId);
                });

            migrationBuilder.CreateTable(
                name: "Song",
                columns: table => new
                {
                    SongId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Genre = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Album = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    Seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    BPM = table.Column<int>(type: "INTEGER", nullable: true),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    File = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    FileAddress = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: true),
                    FileDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Editor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Edited = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Song", x => x.SongId);
                });

            migrationBuilder.CreateTable(
                name: "SongFilter",
                columns: table => new
                {
                    SongFilterId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlaylistId = table.Column<int>(type: "INTEGER", nullable: false),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    HasArtist = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasTitle = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasGenre = table.Column<bool>(type: "INTEGER", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Editor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Edited = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongFilter", x => x.SongFilterId);
                    table.ForeignKey(
                        name: "FK_SongFilter_Playlist",
                        column: x => x.PlaylistId,
                        principalTable: "Playlist",
                        principalColumn: "PlaylistId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SongFilter_Song",
                        column: x => x.SongId,
                        principalTable: "Song",
                        principalColumn: "SongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SongFilter_PlaylistId",
                table: "SongFilter",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_SongFilter_SongId",
                table: "SongFilter",
                column: "SongId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Config");

            migrationBuilder.DropTable(
                name: "ImportError");

            migrationBuilder.DropTable(
                name: "Log");

            migrationBuilder.DropTable(
                name: "MP3Genre");

            migrationBuilder.DropTable(
                name: "SongFilter");

            migrationBuilder.DropTable(
                name: "Playlist");

            migrationBuilder.DropTable(
                name: "Song");
        }
    }
}
