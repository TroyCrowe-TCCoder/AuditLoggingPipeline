-- Appends a batch of usage telemetry events in one set-based insert. The caller supplies every field.
-- Returns the number of rows stored.
CREATE PROCEDURE [dbo].[UsageEvent_Insert]
	@Events [dbo].[UsageEventList] READONLY
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	BEGIN TRY
		INSERT INTO [dbo].[UsageEvent] ([OccurredOn], [Application], [Version], [Environment], [Name], [Category], [Target], [DurationMs], [Outcome], [UserKey], [SampleWeight], [Properties])
		SELECT [OccurredOn], [Application], [Version], [Environment], [Name], [Category], [Target], [DurationMs], [Outcome], [UserKey], [SampleWeight], [Properties]
		FROM @Events;

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
