-- Template for the insert procedure of a standard audit table. Copy it once per audit table and rename it to
-- match (for example SecurityAudit_Insert or UsersAudit_Insert). The caller supplies every field. Returns the
-- number of rows stored. The procedure holds no validation or business rules.
CREATE PROCEDURE [dbo].[Audit_Insert]
	@Entries [dbo].[AuditList] READONLY
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	BEGIN TRY
		INSERT INTO [dbo].[Audit] ([OccurredOn], [Who], [WhoName], [What], [Outcome], [CorrelationId], [EntityType], [EntityId], [EntityKeys], [State], [Reason], [IpAddress])
		SELECT [OccurredOn], [Who], [WhoName], [What], [Outcome], [CorrelationId], [EntityType], [EntityId], [EntityKeys], [State], [Reason], [IpAddress]
		FROM @Entries;

		SELECT @@ROWCOUNT AS [RowsInserted];
	END TRY
	BEGIN CATCH
		DECLARE @ERROR_NUMBER INT = ERROR_NUMBER();
		DECLARE @ERROR_SEVERITY INT = ERROR_SEVERITY();
		DECLARE @ERROR_STATE INT = ERROR_STATE();
		DECLARE @ERROR_PROCEDURE VARCHAR(50) = ERROR_PROCEDURE();
		DECLARE @ERROR_LINE INT = ERROR_LINE();
		DECLARE @ERROR_MESSAGE VARCHAR(100) = LEFT(ERROR_MESSAGE(), 100);

		EXEC dbo.SqlErrorTracking_Insert
			@ERROR_NUMBER,
			@ERROR_SEVERITY,
			@ERROR_STATE,
			@ERROR_PROCEDURE,
			@ERROR_LINE,
			@ERROR_MESSAGE;

		THROW;
	END CATCH;
END
GO
