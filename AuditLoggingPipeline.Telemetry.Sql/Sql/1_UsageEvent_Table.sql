-- Usage telemetry stream: what happened, how long it took and how often, for analysing how the product
-- is used. Written in batches by the AuditLoggingPipeline.Telemetry.Sql sink through dbo.UsageEvent_Insert.
-- Informational only: it says nothing about who did something (UserKey is an
-- optional salted hash, NULL unless the application turns it on). Separate from the audit tables, which
-- are never touched by telemetry. SampleWeight is how many calls a row stands for when sampling is on.
CREATE TABLE [dbo].[UsageEvent]
(
	[UsageEventId]  BIGINT IDENTITY(1,1) NOT NULL,
	[OccurredOn]    DATETIME2            NOT NULL, -- when the observation was made (UTC)
	[RecordedOn]    DATETIME2            NOT NULL CONSTRAINT [DF_UsageEvent_RecordedOn] DEFAULT (SYSUTCDATETIME()), -- when the row was stored
	[Application]   NVARCHAR(100)        NULL,
	[Version]       NVARCHAR(50)         NULL,
	[Environment]   NVARCHAR(50)         NULL,
	[Name]          NVARCHAR(200)        NOT NULL, -- what happened, e.g. 'IReportService.ExportAsync'
	[Category]      NVARCHAR(100)        NULL,
	[Target]        NVARCHAR(200)        NULL,
	[DurationMs]    DECIMAL(18,3)        NULL,
	[Outcome]       NVARCHAR(20)         NOT NULL, -- 'Success' | 'Failure' | 'Canceled'
	[UserKey]       NVARCHAR(64)         NULL,
	[SampleWeight]  INT                  NOT NULL CONSTRAINT [DF_UsageEvent_SampleWeight] DEFAULT (1),
	[Properties]    NVARCHAR(MAX)        NULL, -- JSON object of low-cardinality dimensions
	CONSTRAINT [PK_UsageEvent] PRIMARY KEY CLUSTERED ([UsageEventId] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_UsageEvent_OccurredOn] ON [dbo].[UsageEvent] ([OccurredOn] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_UsageEvent_Name_OccurredOn] ON [dbo].[UsageEvent] ([Name], [OccurredOn] DESC);
GO
