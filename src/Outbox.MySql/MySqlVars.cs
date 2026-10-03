namespace Outbox.MySql;

internal static class MySqlVars
{
    public const int IdColumnMaxLength = 36;
    public const int TypeColumnMaxLength = 512;
    public static readonly DateTime MinimumDateTime =
        new(1000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
            INSERT INTO `OutboxMessages` (`Id`, `Data`, `Type`, `Headers`, `InsertDate`, `ScheduledAt`)
            VALUES (@Id, @Data, @Type, @Headers, @InsertDate, @ScheduledAt)
            """;

        public const string SelectNextOutboxMessage =
            """
            SELECT `Id`, `Data`, `Type`, `Headers`, `InsertDate`, `ScheduledAt`
            FROM `OutboxMessages`
            WHERE `ScheduledAt` <= @Now
            ORDER BY `ScheduledAt`, `InsertDate`
            LIMIT 1
            FOR UPDATE SKIP LOCKED;
            """;

        public const string DeleteOutboxMessage =
            """
            DELETE FROM `OutboxMessages`
            WHERE `Id` = @Id;
            """;
    }

    public static class Schema
    {
        public const string EnsureOutboxMessagesTable =
            """
            CREATE TABLE IF NOT EXISTS `OutboxMessages`
            (
                `Id`          CHAR(36)     NOT NULL,
                `Data`        LONGTEXT     NOT NULL,
                `Type`        VARCHAR(512) NOT NULL,
                `Headers`     LONGTEXT     NOT NULL,
                `InsertDate`  DATETIME(6)  NOT NULL,
                `ScheduledAt` DATETIME(6)  NOT NULL,
                CONSTRAINT `PK_OutboxMessages` PRIMARY KEY (`Id`)
            ) ENGINE = InnoDB
              DEFAULT CHARSET = utf8mb4
              COLLATE = utf8mb4_0900_ai_ci;
            """;

        public const string CreateOutboxMessagesIndex =
            """
            CREATE INDEX `IX_OutboxMessages_ScheduledAt_InsertDate`
                ON `OutboxMessages` (`ScheduledAt`, `InsertDate`);
            """;

        public const string OutboxMessagesIndexExists =
            """
            SELECT COUNT(1)
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = 'OutboxMessages'
              AND index_name = 'IX_OutboxMessages_ScheduledAt_InsertDate';
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
