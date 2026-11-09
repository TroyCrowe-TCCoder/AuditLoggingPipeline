-- The standard audit table type. Every audit insert procedure takes this type, so every audit table in every app
-- has the same shape. Columns are in the order the SQL sinks fill them. Create it once per database.
CREATE TYPE [dbo].[AuditList] AS TABLE
(
	[OccurredOn]     DATETIME2      NOT NULL,
	[Who]            NVARCHAR(200)  NOT NULL, -- the acting principal's id
	[WhoName]        NVARCHAR(200)  NULL,     -- the acting principal's display name
	[What]           NVARCHAR(100)  NOT NULL, -- the operation or action
	[Outcome]        NVARCHAR(10)   NOT NULL, -- 'Success' | 'Failure'
	[CorrelationId]  NVARCHAR(64)   NOT NULL,
	[EntityType]     NVARCHAR(100)  NOT NULL,
	[EntityId]       NVARCHAR(200)  NULL,     -- the target's key as text; a composite key is its parts joined with '|'
	[EntityKeys]     NVARCHAR(MAX)  NULL,     -- JSON of the key parts by name
	[State]          NVARCHAR(MAX)  NULL,     -- JSON of the record after the change; null when there is no record state
	[Reason]         NVARCHAR(500)  NULL,
	[IpAddress]      NVARCHAR(45)   NOT NULL
);
GO
