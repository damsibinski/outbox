namespace Outbox.SqlServer;

internal static class SqlServerVars
{
    public const int TypeColumnMaxLength = 512;

    public static class Columns
    {
        public const string Id = "Id";
        public const string Data = "Data";
        public const string Type = "Type";
        public const string Headers = "Headers";
        public const string InsertDate = "InsertDate";
        public const string ScheduledAt = "ScheduledAt";
    }

    public static class Queries
    {
        public const string InsertOutboxMessage =
            """
            INSERT INTO OutboxMessages (Id, Data, Type, Headers, InsertDate, ScheduledAt)
            VALUES (@Id, @Data, @Type, @Headers, @InsertDate, @ScheduledAt)
            """;

        public const string ClaimNextOutboxMessage =
            """
            DELETE om
            OUTPUT
                DELETED.Id,
                DELETED.Data,
                DELETED.Type,
                DELETED.Headers,
                DELETED.InsertDate,
                DELETED.ScheduledAt
            FROM OutboxMessages om
            INNER JOIN (
                SELECT TOP (1) Id
                FROM OutboxMessages WITH (ROWLOCK, READPAST, UPDLOCK)
                WHERE ScheduledAt <= @Now
                ORDER BY ScheduledAt, InsertDate
            ) next ON om.Id = next.Id;
            """;
    }

    public static class Schema
    {
        public const string EnsureOutboxMessages =
            """
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
            """;
    }

    public static class Parameters
    {
        public const string Id = "@Id";
        public const string Data = "@Data";
        public const string Type = "@Type";
        public const string Headers = "@Headers";
        public const string InsertDate = "@InsertDate";
        public const string ScheduledAt = "@ScheduledAt";
        public const string Now = "@Now";
    }
}
