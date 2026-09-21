IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Leagues] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TcgId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [StartsOn] date NOT NULL,
        [EndsOn] date NOT NULL,
        [IsOpen] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Leagues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Stores] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address_Street] nvarchar(200) NOT NULL,
        [Address_City] nvarchar(100) NOT NULL,
        [Address_Province] nvarchar(100) NOT NULL,
        [Address_PostalCode] nvarchar(10) NOT NULL,
        [Address_Latitude] float NULL,
        [Address_Longitude] float NULL,
        [Status] int NOT NULL,
        [IsTestStore] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Stores] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Tcgs] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(16) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Tcgs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Tournaments] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TcgId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [Venue_Street] nvarchar(200) NOT NULL,
        [Venue_City] nvarchar(100) NOT NULL,
        [Venue_Province] nvarchar(100) NOT NULL,
        [Venue_PostalCode] nvarchar(10) NOT NULL,
        [Venue_Latitude] float NULL,
        [Venue_Longitude] float NULL,
        [Capacity] int NOT NULL,
        [SwissRounds] int NOT NULL,
        [TopCutSize] int NULL,
        [Status] int NOT NULL,
        [Settings_SourceRulesetId] int NOT NULL,
        [Settings_RulesVersion] nvarchar(200) NOT NULL,
        [Settings_WinPoints] int NOT NULL,
        [Settings_DrawPoints] int NOT NULL,
        [Settings_LossPoints] int NOT NULL,
        [Settings_ByePoints] int NOT NULL,
        [Settings_GameWinPoints] int NOT NULL,
        [Settings_GameDrawPoints] int NOT NULL,
        [Settings_GameLossPoints] int NOT NULL,
        [Settings_ByesCountInOwnWinPercentage] bit NOT NULL,
        [Settings_MinimumPercentage] decimal(4,3) NOT NULL,
        [Settings_SwissPhase_Format] int NOT NULL,
        [Settings_SwissPhase_RoundLength] time NULL,
        [Settings_SwissPhase_ExtraTime] time NOT NULL,
        [Settings_TopCutPhase_Format] int NOT NULL,
        [Settings_TopCutPhase_RoundLength] time NULL,
        [Settings_TopCutPhase_ExtraTime] time NOT NULL,
        [Settings_TimeExpiry] int NOT NULL,
        [Tiebreakers] nvarchar(64) NOT NULL,
        [LeagueId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Tournaments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Rulesets] (
        [Id] int NOT NULL IDENTITY,
        [TcgId] int NOT NULL,
        [WinPoints] int NOT NULL,
        [DrawPoints] int NOT NULL,
        [LossPoints] int NOT NULL,
        [ByePoints] int NOT NULL,
        [GameWinPoints] int NOT NULL,
        [GameDrawPoints] int NOT NULL,
        [GameLossPoints] int NOT NULL,
        [ByesCountInOwnWinPercentage] bit NOT NULL,
        [MinimumPercentage] decimal(4,3) NOT NULL,
        [TimeExpiry] int NOT NULL,
        [SwissPhase_Format] int NOT NULL,
        [SwissPhase_RoundLength] time NULL,
        [SwissPhase_ExtraTime] time NOT NULL,
        [TopCutPhase_Format] int NOT NULL,
        [TopCutPhase_RoundLength] time NULL,
        [TopCutPhase_ExtraTime] time NOT NULL,
        [RulesVersion] nvarchar(200) NOT NULL,
        [OfficialSource] nvarchar(500) NOT NULL,
        [Tiebreakers] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_Rulesets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Rulesets_Tcgs_TcgId] FOREIGN KEY ([TcgId]) REFERENCES [Tcgs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Participants] (
        [Id] uniqueidentifier NOT NULL,
        [TournamentId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [UserId] nvarchar(450) NULL,
        [Status] int NOT NULL,
        [WaitlistPosition] int NULL,
        [RegisteredAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Participants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Participants_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Rounds] (
        [Id] uniqueidentifier NOT NULL,
        [TournamentId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [Phase] int NOT NULL,
        [Format] int NOT NULL,
        [Status] int NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [RegularTimeEndsAt] datetimeoffset NULL,
        [ExtraTimeEndsAt] datetimeoffset NULL,
        [ClosedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Rounds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Rounds_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Pairings] (
        [Id] uniqueidentifier NOT NULL,
        [RoundId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Table] int NOT NULL,
        [PlayerAId] uniqueidentifier NOT NULL,
        [PlayerBId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [ReportedBy] uniqueidentifier NULL,
        [AmendedByOrganizer] bit NOT NULL,
        [DecidedByTimeExpiry] bit NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Pairings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pairings_Rounds_RoundId] FOREIGN KEY ([RoundId]) REFERENCES [Rounds] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE TABLE [Games] (
        [Id] uniqueidentifier NOT NULL,
        [PairingId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [Winner] int NOT NULL,
        CONSTRAINT [PK_Games] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Games_Pairings_PairingId] FOREIGN KEY ([PairingId]) REFERENCES [Pairings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Games_PairingId_Number] ON [Games] ([PairingId], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Leagues_TenantId_TcgId_IsOpen] ON [Leagues] ([TenantId], [TcgId], [IsOpen]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Pairings_PlayerAId] ON [Pairings] ([PlayerAId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Pairings_PlayerBId] ON [Pairings] ([PlayerBId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Pairings_RoundId_Table] ON [Pairings] ([RoundId], [Table]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Participants_TournamentId_Status] ON [Participants] ([TournamentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Participants_TournamentId_UserId] ON [Participants] ([TournamentId], [UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Rounds_TournamentId_Number] ON [Rounds] ([TournamentId], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Rulesets_TcgId] ON [Rulesets] ([TcgId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Stores_Address_City] ON [Stores] ([Address_City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Stores_Status_IsTestStore] ON [Stores] ([Status], [IsTestStore]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tcgs_Code] ON [Tcgs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Tournaments_LeagueId] ON [Tournaments] ([LeagueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Tournaments_Status_StartsAt] ON [Tournaments] ([Status], [StartsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Tournaments_TcgId] ON [Tournaments] ([TcgId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Tournaments_TenantId_Status] ON [Tournaments] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Tournaments_Venue_City] ON [Tournaments] ([Venue_City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919231240_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260919231240_InitialSchema', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [ConsentAcceptedAt] datetimeoffset NOT NULL,
        [ConsentVersion] nvarchar(32) NOT NULL,
        [StoreId] uniqueidentifier NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_StoreId] ON [AspNetUsers] ([StoreId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920084314_IdentitySchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260920084314_IdentitySchema', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920094951_AttendanceBands'
)
BEGIN
    ALTER TABLE [Rulesets] ADD [AttendanceBands] nvarchar(256) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920094951_AttendanceBands'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260920094951_AttendanceBands', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    CREATE TABLE [StorePlayers] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [NormalizedName] nvarchar(200) NOT NULL,
        [UserId] nvarchar(450) NULL,
        [MergedIntoId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_StorePlayers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    CREATE INDEX [IX_StorePlayers_TenantId_NormalizedName] ON [StorePlayers] ([TenantId], [NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StorePlayers_TenantId_UserId] ON [StorePlayers] ([TenantId], [UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    ALTER TABLE [Participants] ADD [StorePlayerId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    INSERT INTO [StorePlayers] ([Id], [TenantId], [Name], [NormalizedName], [UserId], [CreatedAt])
    SELECT NEWID(), book.[TenantId], MIN(book.[Name]), book.[Normalized], book.[UserId], SYSDATETIMEOFFSET()
    FROM (SELECT p.[TenantId], p.[Name], p.[UserId], LOWER(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(
        CAST(p.Name AS varchar(200)) COLLATE SQL_Latin1_General_CP1253_CI_AI,
        ' ', '<>'), '><', ''), '<>', ' ')))) AS [Normalized] FROM [Participants] p) book
    GROUP BY book.[TenantId], book.[Normalized], book.[UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    UPDATE p
    SET p.[StorePlayerId] = sp.[Id]
    FROM [Participants] p
    JOIN [StorePlayers] sp
      ON sp.[TenantId] = p.[TenantId]
     AND sp.[NormalizedName] = LOWER(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(
        CAST(p.Name AS varchar(200)) COLLATE SQL_Latin1_General_CP1253_CI_AI,
        ' ', '<>'), '><', ''), '<>', ' '))))
     AND ((sp.[UserId] IS NULL AND p.[UserId] IS NULL) OR sp.[UserId] = p.[UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Participants]') AND [c].[name] = N'StorePlayerId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Participants] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Participants] ALTER COLUMN [StorePlayerId] uniqueidentifier NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    CREATE INDEX [IX_Participants_StorePlayerId] ON [Participants] ([StorePlayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921215033_StorePlayers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921215033_StorePlayers', N'10.0.12');
END;

COMMIT;
GO

