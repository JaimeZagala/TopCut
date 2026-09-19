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

