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

CREATE INDEX `IX_OutboxMessages_ScheduledAt_InsertDate`
    ON `OutboxMessages` (`ScheduledAt`, `InsertDate`);
