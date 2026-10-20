using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    ConsentAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, collation: "und-x-icu"),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Xml = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TcgId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorePlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, collation: "und-x-icu"),
                    MergedIntoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorePlayers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    Address_Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    Address_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "und-x-icu"),
                    Address_Province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "und-x-icu"),
                    Address_PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, collation: "und-x-icu"),
                    Address_Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Address_Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsTestStore = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tcgs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "und-x-icu"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "und-x-icu"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tcgs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TcgId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Venue_Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    Venue_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "und-x-icu"),
                    Venue_Province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "und-x-icu"),
                    Venue_PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, collation: "und-x-icu"),
                    Venue_Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Venue_Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    SwissRounds = table.Column<int>(type: "integer", nullable: false),
                    TopCutSize = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Settings_SourceRulesetId = table.Column<int>(type: "integer", nullable: false),
                    Settings_RulesVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    Settings_WinPoints = table.Column<int>(type: "integer", nullable: false),
                    Settings_DrawPoints = table.Column<int>(type: "integer", nullable: false),
                    Settings_LossPoints = table.Column<int>(type: "integer", nullable: false),
                    Settings_ByePoints = table.Column<int>(type: "integer", nullable: false),
                    Settings_GameWinPoints = table.Column<int>(type: "integer", nullable: false),
                    Settings_GameDrawPoints = table.Column<int>(type: "integer", nullable: false),
                    Settings_GameLossPoints = table.Column<int>(type: "integer", nullable: false),
                    Settings_ByesCountInOwnWinPercentage = table.Column<bool>(type: "boolean", nullable: false),
                    Settings_MinimumPercentage = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    Settings_SwissPhase_Format = table.Column<int>(type: "integer", nullable: false),
                    Settings_SwissPhase_RoundLength = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Settings_SwissPhase_ExtraTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Settings_TopCutPhase_Format = table.Column<int>(type: "integer", nullable: false),
                    Settings_TopCutPhase_RoundLength = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Settings_TopCutPhase_ExtraTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Settings_TimeExpiry = table.Column<int>(type: "integer", nullable: false),
                    Tiebreakers = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    ProviderKey = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    UserId = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    RoleId = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    LoginProvider = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Value = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rulesets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TcgId = table.Column<int>(type: "integer", nullable: false),
                    WinPoints = table.Column<int>(type: "integer", nullable: false),
                    DrawPoints = table.Column<int>(type: "integer", nullable: false),
                    LossPoints = table.Column<int>(type: "integer", nullable: false),
                    ByePoints = table.Column<int>(type: "integer", nullable: false),
                    GameWinPoints = table.Column<int>(type: "integer", nullable: false),
                    GameDrawPoints = table.Column<int>(type: "integer", nullable: false),
                    GameLossPoints = table.Column<int>(type: "integer", nullable: false),
                    ByesCountInOwnWinPercentage = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumPercentage = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    TimeExpiry = table.Column<int>(type: "integer", nullable: false),
                    SwissPhase_Format = table.Column<int>(type: "integer", nullable: false),
                    SwissPhase_RoundLength = table.Column<TimeSpan>(type: "interval", nullable: true),
                    SwissPhase_ExtraTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TopCutPhase_Format = table.Column<int>(type: "integer", nullable: false),
                    TopCutPhase_RoundLength = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TopCutPhase_ExtraTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    OfficialSource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "und-x-icu"),
                    AttendanceBands = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Tiebreakers = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorePlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "und-x-icu"),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, collation: "und-x-icu"),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WaitlistPosition = table.Column<int>(type: "integer", nullable: true),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegularTimeEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExtraTimeEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Table = table.Column<int>(type: "integer", nullable: false),
                    PlayerAId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerBId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AmendedByOrganizer = table.Column<bool>(type: "boolean", nullable: false),
                    DecidedByTimeExpiry = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PairingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Winner = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_StoreId",
                table: "AspNetUsers",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

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
                name: "IX_Participants_StorePlayerId",
                table: "Participants",
                column: "StorePlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TournamentId_Status",
                table: "Participants",
                columns: new[] { "TournamentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TournamentId_UserId",
                table: "Participants",
                columns: new[] { "TournamentId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

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
                name: "IX_StorePlayers_TenantId_NormalizedName",
                table: "StorePlayers",
                columns: new[] { "TenantId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_StorePlayers_TenantId_UserId",
                table: "StorePlayers",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

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
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Rulesets");

            migrationBuilder.DropTable(
                name: "StorePlayers");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

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
