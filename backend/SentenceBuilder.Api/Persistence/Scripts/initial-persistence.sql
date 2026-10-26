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
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE TABLE [Sentence] (
        [Id] int NOT NULL IDENTITY,
        [Text] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Sentence] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Sentence_Text_Length] CHECK (DATALENGTH([Text]) <= 20198),
        CONSTRAINT [CK_Sentence_Text_NotBlank] CHECK (LEN(LTRIM(RTRIM([Text]))) > 0),
        CONSTRAINT [CK_Sentence_Timestamps] CHECK ([UpdatedAt] >= [CreatedAt])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE TABLE [WordType] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_WordType] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WordType_Name_NotBlank] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE TABLE [Word] (
        [Id] int NOT NULL IDENTITY,
        [WordTypeId] int NOT NULL,
        [Value] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Word] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Word_Value_NotBlank] CHECK (LEN(LTRIM(RTRIM([Value]))) > 0),
        CONSTRAINT [FK_Word_WordType_WordTypeId] FOREIGN KEY ([WordTypeId]) REFERENCES [WordType] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE TABLE [SentenceWord] (
        [SentenceId] int NOT NULL,
        [Position] int NOT NULL,
        [WordId] int NOT NULL,
        [WordText] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_SentenceWord] PRIMARY KEY ([SentenceId], [Position]),
        CONSTRAINT [CK_SentenceWord_Position] CHECK ([Position] >= 0),
        CONSTRAINT [CK_SentenceWord_WordText_NotBlank] CHECK (LEN(LTRIM(RTRIM([WordText]))) > 0),
        CONSTRAINT [FK_SentenceWord_Sentence_SentenceId] FOREIGN KEY ([SentenceId]) REFERENCES [Sentence] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SentenceWord_Word_WordId] FOREIGN KEY ([WordId]) REFERENCES [Word] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Sentence_CreatedAt_Id] ON [Sentence] ([CreatedAt] DESC, [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_SentenceWord_WordId] ON [SentenceWord] ([WordId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Word_WordTypeId_Value] ON [Word] ([WordTypeId], [Value]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WordType_Name] ON [WordType] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020100448_InitialPersistence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020100448_InitialPersistence', N'10.0.13');
END;

COMMIT;
GO
