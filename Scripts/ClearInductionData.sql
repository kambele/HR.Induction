/*
    NAMFISA Employee Induction - transactional data cleanup

    Clears:
      - Employee induction records and checklist activity
      - Programme and training feedback, including presentation ratings
      - Attendance and sign-off records
      - Induction programmes, sessions and selected phases

    Preserves:
      - Checklist templates
      - Master data
      - Workflow configuration
      - EF Core migration history

    Safety:
      1. The script only runs against a database named HRInduction.
      2. @ConfirmClear must be changed to 1 before any DELETE executes.
      3. All DELETE statements run in one transaction and roll back on error.
*/

USE [HRInduction];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ConfirmClear bit = 0; -- Change to 1 only when the preview counts are correct.
DECLARE @ResetIdentityValues bit = 0; -- Optional: change to 1 to restart identity values at 1.

IF DB_NAME() <> N'HRInduction'
BEGIN
    THROW 51000, 'Safety check failed: this script may run only against HRInduction.', 1;
END;

IF OBJECT_ID(N'dbo.InductionCases', N'U') IS NULL
   OR OBJECT_ID(N'dbo.InductionProgrammes', N'U') IS NULL
   OR OBJECT_ID(N'dbo.EmployeeFeedback', N'U') IS NULL
BEGIN
    THROW 51001, 'Required HRInduction tables were not found. No data was changed.', 1;
END;

PRINT N'Current records that will be removed:';

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.InductionCases) AS EmployeeInductions,
    (SELECT COUNT_BIG(*) FROM dbo.InductionTasks) AS ChecklistTasks,
    (SELECT COUNT_BIG(*) FROM dbo.SignOffs) AS SignOffs,
    (SELECT COUNT_BIG(*) FROM dbo.EmployeeFeedback) AS ProgrammeFeedbackResponses,
    (SELECT COUNT_BIG(*) FROM dbo.PresentationRatings) AS PresentationRatings,
    (SELECT COUNT_BIG(*) FROM dbo.TrainingEvaluations) AS TrainingEvaluations,
    (SELECT COUNT_BIG(*) FROM dbo.ProgrammeAttendances) AS AttendanceRecords,
    (SELECT COUNT_BIG(*) FROM dbo.InductionProgrammes) AS InductionProgrammes,
    (SELECT COUNT_BIG(*) FROM dbo.InductionProgrammeSessions) AS ProgrammeSessions,
    (SELECT COUNT_BIG(*) FROM dbo.InductionProgrammeChecklistPhases) AS ProgrammeChecklistPhases;

IF @ConfirmClear <> 1
BEGIN
    PRINT N'PREVIEW ONLY: no records were deleted. Set @ConfirmClear = 1 and run the script again to proceed.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Feedback must be removed before programme sessions because presentation
    -- ratings have a restrictive foreign key to those sessions.
    DELETE FROM dbo.PresentationRatings;
    DELETE FROM dbo.TrainingEvaluations;
    DELETE FROM dbo.EmployeeFeedback;

    DELETE FROM dbo.ProgrammeAttendances;
    DELETE FROM dbo.SignOffs;
    DELETE FROM dbo.InductionTasks;
    DELETE FROM dbo.InductionCases;

    DELETE FROM dbo.InductionProgrammeChecklistPhases;
    DELETE FROM dbo.InductionProgrammeSessions;
    DELETE FROM dbo.InductionProgrammes;

    IF @ResetIdentityValues = 1
    BEGIN
        DBCC CHECKIDENT (N'dbo.PresentationRatings', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.TrainingEvaluations', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.EmployeeFeedback', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.ProgrammeAttendances', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.SignOffs', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.InductionTasks', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.InductionCases', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.InductionProgrammeSessions', RESEED, 0) WITH NO_INFOMSGS;
        DBCC CHECKIDENT (N'dbo.InductionProgrammes', RESEED, 0) WITH NO_INFOMSGS;
    END;

    COMMIT TRANSACTION;

    PRINT N'Cleanup completed successfully.';
    SELECT
        (SELECT COUNT_BIG(*) FROM dbo.InductionCases) AS RemainingEmployeeInductions,
        (SELECT COUNT_BIG(*) FROM dbo.EmployeeFeedback) AS RemainingProgrammeFeedbackResponses,
        (SELECT COUNT_BIG(*) FROM dbo.TrainingEvaluations) AS RemainingTrainingEvaluations,
        (SELECT COUNT_BIG(*) FROM dbo.InductionProgrammes) AS RemainingInductionProgrammes;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
