namespace Outbox.Postgres;

internal static class PostgresVars
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
            INSERT INTO "OutboxMessages" ("Id", "Data", "Type", "Headers", "InsertDate", "ScheduledAt")
            VALUES (@Id, @Data, @Type, @Headers, @InsertDate, @ScheduledAt)
            """;

        public const string ClaimNextOutboxMessage =
            """
            WITH next AS (
                SELECT "Id"
                FROM "OutboxMessages"
                WHERE "ScheduledAt" <= @Now
                ORDER BY "ScheduledAt", "InsertDate"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            DELETE FROM "OutboxMessages" om
            USING next
            WHERE om."Id" = next."Id"
            RETURNING
                om."Id",
                om."Data",
                om."Type",
                om."Headers",
                om."InsertDate",
                om."ScheduledAt";
            """;
    }

    public static class Schema
    {
        public const string EnsureOutboxMessages =
            """
            CREATE TABLE IF NOT EXISTS "OutboxMessages"
            (
                "Id"          UUID         NOT NULL,
                "Data"        TEXT         NOT NULL,
                "Type"        VARCHAR(512) NOT NULL,
                "Headers"     TEXT         NOT NULL,
                "InsertDate"  TIMESTAMPTZ  NOT NULL,
                "ScheduledAt" TIMESTAMPTZ  NOT NULL,
                CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_ScheduledAt_InsertDate"
                ON "OutboxMessages" ("ScheduledAt", "InsertDate");
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
