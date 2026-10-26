-- Initial database records only: never a runtime fallback vocabulary list.
-- Run only after approval, against the dedicated development or test database.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT IN (
    N'TIH_SentenceBuilder_Dev',
    N'TIH_SentenceBuilder_Test',
    N'sqldb-sentence-builder-dev'
)
    THROW 50001, 'Vocabulary seed requires the approved dedicated development or test database.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Vocabulary TABLE (TypeName nvarchar(50) NOT NULL, WordValue nvarchar(100) NOT NULL);
    INSERT INTO @Vocabulary (TypeName, WordValue) VALUES
        (N'Noun', N'cat'), (N'Noun', N'dog'), (N'Noun', N'garden'),
        (N'Verb', N'runs'), (N'Verb', N'plays'), (N'Verb', N'sleeps'),
        (N'Adjective', N'quiet'), (N'Adjective', N'happy'), (N'Adjective', N'green'),
        (N'Adverb', N'quickly'), (N'Adverb', N'softly'), (N'Adverb', N'often'),
        (N'Pronoun', N'he'), (N'Pronoun', N'she'), (N'Pronoun', N'they'),
        (N'Preposition', N'in'), (N'Preposition', N'on'), (N'Preposition', N'under'),
        (N'Conjunction', N'and'), (N'Conjunction', N'but'), (N'Conjunction', N'or'),
        (N'Determiner', N'the'), (N'Determiner', N'a'), (N'Determiner', N'this'),
        (N'Exclamation', N'wow'), (N'Exclamation', N'oh'), (N'Exclamation', N'hey');

    INSERT INTO dbo.WordType (Name)
    SELECT DISTINCT v.TypeName FROM @Vocabulary v
    WHERE NOT EXISTS (SELECT 1 FROM dbo.WordType t WITH (UPDLOCK, HOLDLOCK) WHERE t.Name = v.TypeName);

    INSERT INTO dbo.Word (WordTypeId, Value)
    SELECT t.Id, v.WordValue FROM @Vocabulary v
    JOIN dbo.WordType t ON t.Name = v.TypeName
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Word w WITH (UPDLOCK, HOLDLOCK) WHERE w.WordTypeId = t.Id AND w.Value = v.WordValue);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
