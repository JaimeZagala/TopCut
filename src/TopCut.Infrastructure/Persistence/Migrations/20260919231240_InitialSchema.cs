using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopCut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TcgId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address_Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address_Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address_PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Address_Latitude = table.Column<double>(type: "float", nullable: true),
                    Address_Longitude = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsTestStore = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tcgs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tcgs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TcgId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Venue_Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Venue_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Venue_Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Venue_PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Venue_Latitude = table.Column<double>(type: "float", nullable: true),
                    Venue_Longitude = table.Column<double>(type: "float", nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    SwissRounds = table.Column<int>(type: "int", nullable: false),
                    TopCutSize = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Settings_SourceRulesetId = table.Column<int>(type: "int", nullable: false),
                    Settings_RulesVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Settings_WinPoints = table.Column<int>(type: "int", nullable: false),
                    Settings_DrawPoints = table.Column<int>(type: "int", nullable: false),
                    Settings_LossPoints = table.Column<int>(type: "int", nullable: false),
                    Settings_ByePoints = table.Column<int>(type: "int", nullable: false),
                    Settings_GameWinPoints = table.Column<int>(type: "int", nullable: false),
                    Settings_GameDrawPoints = table.Column<int>(type: "int", nullable: false),
                    Settings_GameLossPoints = table.Column<int>(type: "int", nullable: false),
                    Settings_ByesCountInOwnWinPercentage = table.Column<bool>(type: "bit", nullable: false),
                    Settings_MinimumPercentage = table.Column<decimal>(type: "decimal(4,3)", precision: 4, scale: 3, nullable: false),
                    Settings_SwissPhase_Format = table.Column<int>(type: "int", nullable: false),
                    Settings_SwissPhase_RoundLength = table.Column<TimeSpan>(type: "time", nullable: true),
                    Settings_SwissPhase_ExtraTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Settings_TopCutPhase_Format = table.Column<int>(type: "int", nullable: false),
                    Settings_TopCutPhase_RoundLength = table.Column<TimeSpan>(type: "time", nullable: true),
                    Settings_TopCutPhase_ExtraTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Settings_TimeExpiry = table.Column<int>(type: "int", nullable: false),
                    Tiebreakers = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rulesets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TcgId = table.Column<int>(type: "int", nullable: false),
                    WinPoints = table.Column<int>(type: "int", nullable: false),
                    DrawPoints = table.Column<int>(type: "int", nullable: false),
                    LossPoints = table.Column<int>(type: "int", nullable: false),
                    ByePoints = table.Column<int>(type: "int", nullable: false),
                    GameWinPoints = table.Column<int>(type: "int", nullable: false),
                    GameDrawPoints = table.Column<int>(type: "int", nullable: false),
                    GameLossPoints = table.Column<int>(type: "int", nullable: false),
                    ByesCountInOwnWinPercentage = table.Column<bool>(type: "bit", nullable: false),
                    MinimumPercentage = table.Column<decimal>(type: "decimal(4,3)", precision: 4, scale: 3, nullable: false),
                    TimeExpiry = table.Column<int>(type: "int", nullable: false),
                    SwissPhase_Format = table.Column<int>(type: "int", nullable: false),
                    SwissPhase_RoundLength = table.Column<TimeSpan>(type: "time", nullable: true),
                    SwissPhase_ExtraTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    TopCutPhase_Format = table.Column<int>(type: "int", nullable: false),
                    TopCutPhase_RoundLength = table.Column<TimeSpan>(type: "time", nullable: true),
                    TopCutPhase_ExtraTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    RulesVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OfficialSource = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Tiebreakers = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rulesets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rulesets_Tcgs_TcgId",
                        column: x => x.TcgId,
                        principalTable: "Tcgs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WaitlistPosition = table.Column<int>(type: "int", nullable: true),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RegularTimeEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExtraTimeEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rounds_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Table = table.Column<int>(type: "int", nullable: false),
                    PlayerAId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerBId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmendedByOrganizer = table.Column<bool>(type: "bit", nullable: false),
                    DecidedByTimeExpiry = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pairings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pairings_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PairingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Winner = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_Pairings_PairingId",
                        column: x => x.PairingId,
                        principalTable: "Pairings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_PairingId_Number",
                table: "Games",
                columns: new[] { "PairingId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_TenantId_TcgId_IsOpen",
                table: "Leagues",
                columns: new[] { "TenantId", "TcgId", "IsOpen" });

            migrationBuilder.CreateIndex(
                name: "IX_Pairings_PlayerAId",
                table: "Pairings",
                column: "PlayerAId");

            migrationBuilder.CreateIndex(
                name: "IX_Pairings_PlayerBId",
                table: "Pairings",
                column: "PlayerBId");

            migrationBuilder.CreateIndex(
                name: "IX_Pairings_RoundId_Table",
                table: "Pairings",
                columns: new[] { "RoundId", "Table" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TournamentId_Status",
                table: "Participants",
                columns: new[] { "TournamentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TournamentId_UserId",
                table: "Participants",
                columns: new[] { "TournamentId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_TournamentId_Number",
                table: "Rounds",
                columns: new[] { "TournamentId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rulesets_TcgId",
                table: "Rulesets",
                column: "TcgId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Address_City",
                table: "Stores",
                column: "Address_City");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Status_IsTestStore",
                table: "Stores",
                columns: new[] { "Status", "IsTestStore" });

            migrationBuilder.CreateIndex(
                name: "IX_Tcgs_Code",
                table: "Tcgs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_LeagueId",
                table: "Tournaments",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_Status_StartsAt",
                table: "Tournaments",
                columns: new[] { "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_TcgId",
                table: "Tournaments",
                column: "TcgId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_TenantId_Status",
                table: "Tournaments",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_Venue_City",
                table: "Tournaments",
                column: "Venue_City");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Rulesets");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "Pairings");

            migrationBuilder.DropTable(
                name: "Tcgs");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
