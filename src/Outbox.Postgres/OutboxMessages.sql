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
