IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboxMessages
    (
        Id          UNIQUEIDENTIFIER  NOT NULL,
        Data        NVARCHAR(MAX)     NOT NULL,
        Type        NVARCHAR(512)     NOT NULL,
        Headers     NVARCHAR(MAX)     NOT NULL,
        InsertDate  DATETIME2(7)      NOT NULL,
        ScheduledAt DATETIMEOFFSET(7) NOT NULL,
        CONSTRAINT PK_OutboxMessages PRIMARY KEY CLUSTERED (Id)
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OutboxMessages_ScheduledAt_InsertDate'
      AND object_id = OBJECT_ID(N'dbo.OutboxMessages', N'U'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_OutboxMessages_ScheduledAt_InsertDate
        ON dbo.OutboxMessages (ScheduledAt ASC, InsertDate ASC);
END;
