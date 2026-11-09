-- Template for a standard audit table. Copy it once per audit table and rename it: one table per consuming
-- solution for the security audit (for example SecurityAudit), and one table per tracked object for state
-- tracking (for example UsersAudit). The columns do not change between tables. Replace 'Audit' in the names below.
CREATE TABLE [dbo].[Audit]
(
	[AuditId]        BIGINT IDENTITY(1,1) NOT NULL,
	[OccurredOn]     DATETIME2            NOT NULL CONSTRAINT [DF_Audit_OccurredOn] DEFAULT (SYSUTCDATETIME()),
	[Who]            NVARCHAR(200)        NOT NULL, -- the acting principal's id
	[WhoName]        NVARCHAR(200)        NULL,     -- the acting principal's display name
	[What]           NVARCHAR(100)        NOT NULL, -- the operation or action
	[Outcome]        NVARCHAR(10)         NOT NULL, -- 'Success' | 'Failure'
	[CorrelationId]  NVARCHAR(64)         NOT NULL,
	[EntityType]     NVARCHAR(100)        NOT NULL,
	[EntityId]       NVARCHAR(200)        NULL,     -- the target's key as text; a composite key is its parts joined with '|'
	[EntityKeys]     NVARCHAR(MAX)        NULL,     -- JSON of the key parts by name
	[State]          NVARCHAR(MAX)        NULL,     -- JSON of the record after the change; null when there is no record state
	[Reason]         NVARCHAR(500)        NULL,
	[IpAddress]      NVARCHAR(45)         NOT NULL CONSTRAINT [DF_Audit_IpAddress] DEFAULT (N'unknown'),
	CONSTRAINT [PK_Audit] PRIMARY KEY CLUSTERED ([AuditId] ASC),
	CONSTRAINT [CK_Audit_Outcome] CHECK ([Outcome] IN (N'Success', N'Failure'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Audit_OccurredOn] ON [dbo].[Audit] ([OccurredOn] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_Audit_EntityId_OccurredOn] ON [dbo].[Audit] ([EntityId], [OccurredOn] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_Audit_Who_OccurredOn] ON [dbo].[Audit] ([Who], [OccurredOn] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_Audit_CorrelationId] ON [dbo].[Audit] ([CorrelationId]);
GO
