using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopCut.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The store's book of regulars (ADR-0035), plus the link from every participant to it.
    /// <para>
    /// The link is <b>not nullable</b>, so this migration has to invent the book for the
    /// tournaments that already exist. It does it in SQL, in the same order the application would:
    /// one entry per account, and one per name among the players typed in at the counter.
    /// </para>
    /// </summary>
    public partial class StorePlayers : Migration
    {
        /// <summary>
        /// The same normalisation as <c>StorePlayer.Normalize</c>: lower case, no accents, no
        /// repeated spaces. The accents come off by casting to a non-Unicode collation, which is
        /// the only way to do it in T-SQL and does handle the ñ; the spaces collapse with the usual
        /// three-way replace. Verified against the same strings the C# is tested with.
        /// </summary>
        private const string Normalized = """
            LOWER(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(
                CAST(p.Name AS varchar(200)) COLLATE SQL_Latin1_General_CP1253_CI_AI,
                ' ', '<>'), '><', ''), '<>', ' '))))
            """;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorePlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MergedIntoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorePlayers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorePlayers_TenantId_NormalizedName",
                table: "StorePlayers",
                columns: new[] { "TenantId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_StorePlayers_TenantId_UserId",
                table: "StorePlayers",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            // Nullable for as long as it takes to fill it in.
            migrationBuilder.AddColumn<Guid>(
                name: "StorePlayerId",
                table: "Participants",
                type: "uniqueidentifier",
                nullable: true);

            // One entry per account, and one per name among those with no account. Exactly the rule
            // the application uses from here on: the account is the link that cannot be wrong, and
            // a typed name only ever matches other typed names of the same store.
            migrationBuilder.Sql($"""
                INSERT INTO [StorePlayers] ([Id], [TenantId], [Name], [NormalizedName], [UserId], [CreatedAt])
                SELECT NEWID(), book.[TenantId], MIN(book.[Name]), book.[Normalized], book.[UserId], SYSDATETIMEOFFSET()
                FROM (SELECT p.[TenantId], p.[Name], p.[UserId], {Normalized} AS [Normalized] FROM [Participants] p) book
                GROUP BY book.[TenantId], book.[Normalized], book.[UserId];
                """);

            migrationBuilder.Sql($"""
                UPDATE p
                SET p.[StorePlayerId] = sp.[Id]
                FROM [Participants] p
                JOIN [StorePlayers] sp
                  ON sp.[TenantId] = p.[TenantId]
                 AND sp.[NormalizedName] = {Normalized}
                 AND ((sp.[UserId] IS NULL AND p.[UserId] IS NULL) OR sp.[UserId] = p.[UserId]);
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "StorePlayerId",
                table: "Participants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_StorePlayerId",
                table: "Participants",
                column: "StorePlayerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_StorePlayerId",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "StorePlayerId",
                table: "Participants");

            migrationBuilder.DropTable(
                name: "StorePlayers");
        }
    }
}
