-- Table type for passing a batch of usage telemetry events into dbo.UsageEvent_Insert as a
-- table-valued parameter. Columns mirror dbo.UsageEvent minus the generated ones.
CREATE TYPE [dbo].[UsageEventList] AS TABLE
(
	[OccurredOn]    DATETIME2      NOT NULL,
	[Application]   NVARCHAR(100)  NULL,
	[Version]       NVARCHAR(50)   NULL,
	[Environment]   NVARCHAR(50)   NULL,
	[Name]          NVARCHAR(200)  NOT NULL,
	[Category]      NVARCHAR(100)  NULL,
	[Target]        NVARCHAR(200)  NULL,
	[DurationMs]    DECIMAL(18,3)  NULL,
	[Outcome]       NVARCHAR(20)   NOT NULL,
	[UserKey]       NVARCHAR(64)   NULL,
	[SampleWeight]  INT            NOT NULL,
	[Properties]    NVARCHAR(MAX)  NULL
);
