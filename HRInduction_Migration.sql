IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE TABLE [ChecklistTemplateItems] (
        [Id] int NOT NULL IDENTITY,
        [Section] nvarchar(120) NOT NULL,
        [Title] nvarchar(400) NOT NULL,
        [Phase] int NOT NULL,
        [ResponsibleRole] nvarchar(80) NOT NULL,
        [DueOffsetDays] int NOT NULL,
        [IsMandatory] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_ChecklistTemplateItems] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE TABLE [InductionCases] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeName] nvarchar(150) NOT NULL,
        [EmployeeUsername] nvarchar(150) NOT NULL,
        [EmployeeNumber] nvarchar(40) NULL,
        [Department] nvarchar(120) NOT NULL,
        [Division] nvarchar(120) NOT NULL,
        [AppointmentDate] datetime2 NOT NULL,
        [LineManagerName] nvarchar(150) NOT NULL,
        [LineManagerUsername] nvarchar(150) NOT NULL,
        [HrBusinessPartnerName] nvarchar(150) NOT NULL,
        [HrBusinessPartnerUsername] nvarchar(150) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_InductionCases] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE TABLE [EmployeeFeedback] (
        [Id] int NOT NULL IDENTITY,
        [InductionCaseId] int NOT NULL,
        [UnderstandsResponsibilities] bit NOT NULL,
        [AwareOfDevelopmentOpportunities] bit NOT NULL,
        [MissingFromInduction] nvarchar(2000) NULL,
        [AdditionalComments] nvarchar(2000) NULL,
        [SubmittedBy] nvarchar(150) NOT NULL,
        [SubmittedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EmployeeFeedback] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeFeedback_InductionCases_InductionCaseId] FOREIGN KEY ([InductionCaseId]) REFERENCES [InductionCases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE TABLE [InductionTasks] (
        [Id] int NOT NULL IDENTITY,
        [InductionCaseId] int NOT NULL,
        [ChecklistTemplateItemId] int NULL,
        [Section] nvarchar(120) NOT NULL,
        [Title] nvarchar(400) NOT NULL,
        [Phase] int NOT NULL,
        [ResponsibleRole] nvarchar(80) NOT NULL,
        [AssignedToUsername] nvarchar(150) NULL,
        [DueDate] datetime2 NOT NULL,
        [IsMandatory] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [Status] int NOT NULL,
        [CompletedBy] nvarchar(150) NULL,
        [CompletedAtUtc] datetime2 NULL,
        [Notes] nvarchar(1000) NULL,
        CONSTRAINT [PK_InductionTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InductionTasks_InductionCases_InductionCaseId] FOREIGN KEY ([InductionCaseId]) REFERENCES [InductionCases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE TABLE [SignOffs] (
        [Id] int NOT NULL IDENTITY,
        [InductionCaseId] int NOT NULL,
        [Role] int NOT NULL,
        [SignedBy] nvarchar(150) NOT NULL,
        [SignedAtUtc] datetime2 NOT NULL,
        [Acknowledgement] nvarchar(1000) NOT NULL,
        [Comments] nvarchar(1000) NULL,
        CONSTRAINT [PK_SignOffs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SignOffs_InductionCases_InductionCaseId] FOREIGN KEY ([InductionCaseId]) REFERENCES [InductionCases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE TABLE [TrainingEvaluations] (
        [Id] int NOT NULL IDENTITY,
        [InductionCaseId] int NOT NULL,
        [CourseTitle] nvarchar(200) NOT NULL,
        [CourseProvider] nvarchar(200) NOT NULL,
        [CourseDate] datetime2 NOT NULL,
        [ObjectivesClearlyDefined] int NOT NULL,
        [ParticipationEncouraged] int NOT NULL,
        [ContentOrganized] int NOT NULL,
        [MaterialsHelpful] int NOT NULL,
        [UsefulInWork] int NOT NULL,
        [ObjectivesMet] int NOT NULL,
        [TimeSufficient] int NOT NULL,
        [FacilitiesAdequate] int NOT NULL,
        [DisagreementImprovements] nvarchar(2000) NULL,
        [LikedMost] nvarchar(2000) NULL,
        [CouldImprove] nvarchar(2000) NULL,
        [OverallImpression] int NOT NULL,
        [AdditionalComments] nvarchar(2000) NULL,
        [TrainerKnowledge] int NOT NULL,
        [TrainerExplanation] int NOT NULL,
        [TrainerAnswers] int NOT NULL,
        [VenueComments] nvarchar(1000) NULL,
        [MealsComments] nvarchar(1000) NULL,
        [OfficialResultsAttached] bit NOT NULL,
        [CertificateAttached] bit NOT NULL,
        [AttendanceRegisterAttached] bit NOT NULL,
        [SubmittedBy] nvarchar(150) NOT NULL,
        [SubmittedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TrainingEvaluations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrainingEvaluations_InductionCases_InductionCaseId] FOREIGN KEY ([InductionCaseId]) REFERENCES [InductionCases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChecklistTemplateItems_Phase_SortOrder] ON [ChecklistTemplateItems] ([Phase], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmployeeFeedback_InductionCaseId] ON [EmployeeFeedback] ([InductionCaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InductionTasks_InductionCaseId] ON [InductionTasks] ([InductionCaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SignOffs_InductionCaseId_Role] ON [SignOffs] ([InductionCaseId], [Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TrainingEvaluations_InductionCaseId] ON [TrainingEvaluations] ([InductionCaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085050_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014085050_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    ALTER TABLE [ChecklistTemplateItems] ADD [UpdatedAtUtc] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    ALTER TABLE [ChecklistTemplateItems] ADD [UpdatedBy] nvarchar(150) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    UPDATE ChecklistTemplateItems SET UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = 'System migration'
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    CREATE TABLE [MasterDataItems] (
        [Id] int NOT NULL IDENTITY,
        [Category] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(300) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_MasterDataItems] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    CREATE TABLE [WorkflowConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [RequireMandatoryTasksBeforeSignOff] bit NOT NULL,
        [RequireEmployeeFeedbackBeforeSignOff] bit NOT NULL,
        [EnforceSequentialSignOff] bit NOT NULL,
        [LockChecklistWhenSignOffStarts] bit NOT NULL,
        [EmailSubjectTemplate] nvarchar(300) NOT NULL,
        [EmailBodyTemplate] nvarchar(4000) NOT NULL,
        [EmployeeAcknowledgement] nvarchar(1000) NOT NULL,
        [LineManagerAcknowledgement] nvarchar(1000) NOT NULL,
        [HrBusinessPartnerAcknowledgement] nvarchar(1000) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_WorkflowConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MasterDataItems_Category_Name] ON [MasterDataItems] ([Category], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    CREATE INDEX [IX_MasterDataItems_Category_SortOrder] ON [MasterDataItems] ([Category], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014121754_AddAdministrationWorkspace'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014121754_AddAdministrationWorkspace', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [SignOffs] ADD [AuthenticationType] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [SignOffs] ADD [ExpectedSignerUsername] nvarchar(150) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [SignOffs] ADD [SignedBySid] nvarchar(184) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [EmployeeTitle] nvarchar(30) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [FirstName] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [Gender] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [LastName] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [Position] nvarchar(150) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    UPDATE InductionCases SET EmployeeTitle = 'Not recorded', FirstName = LEFT(EmployeeName, 100), LastName = 'Not recorded', Gender = 'Not recorded', Position = 'Not recorded'
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    UPDATE s SET ExpectedSignerUsername = CASE s.Role WHEN 0 THEN i.EmployeeUsername WHEN 1 THEN i.LineManagerUsername WHEN 2 THEN i.HrBusinessPartnerUsername ELSE s.SignedBy END FROM SignOffs s INNER JOIN InductionCases i ON i.Id = s.InductionCaseId
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014124842_AddEmployeeProfileAndSignOffIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014124842_AddEmployeeProfileAndSignOffIdentity', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015072524_AddEmployeeEmailAndInvitationAudit'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [EmployeeEmail] nvarchar(254) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015072524_AddEmployeeEmailAndInvitationAudit'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [InvitationSentAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015072524_AddEmployeeEmailAndInvitationAudit'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [InvitationSentBy] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015072524_AddEmployeeEmailAndInvitationAudit'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [InvitationSentTo] nvarchar(254) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015072524_AddEmployeeEmailAndInvitationAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015072524_AddEmployeeEmailAndInvitationAudit', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    ALTER TABLE [InductionCases] ADD [InductionProgrammeId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    ALTER TABLE [EmployeeFeedback] ADD [ImprovementRecommendations] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    ALTER TABLE [EmployeeFeedback] ADD [MostValuableAspect] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    ALTER TABLE [EmployeeFeedback] ADD [OverallProgrammeRating] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    ALTER TABLE [EmployeeFeedback] ADD [ProgrammeUnderstandingRating] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    ALTER TABLE [EmployeeFeedback] ADD [TimeAllocationResponse] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    CREATE TABLE [InductionProgrammes] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [ProgrammeDate] datetime2 NOT NULL,
        [Venue] nvarchar(200) NULL,
        [Notes] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(150) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_InductionProgrammes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    CREATE TABLE [InductionProgrammeSessions] (
        [Id] int NOT NULL IDENTITY,
        [InductionProgrammeId] int NOT NULL,
        [Division] nvarchar(120) NOT NULL,
        [PresenterName] nvarchar(150) NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_InductionProgrammeSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InductionProgrammeSessions_InductionProgrammes_InductionProgrammeId] FOREIGN KEY ([InductionProgrammeId]) REFERENCES [InductionProgrammes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    CREATE TABLE [PresentationRatings] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeFeedbackId] int NOT NULL,
        [InductionProgrammeSessionId] int NOT NULL,
        [Rating] int NOT NULL,
        CONSTRAINT [PK_PresentationRatings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PresentationRatings_EmployeeFeedback_EmployeeFeedbackId] FOREIGN KEY ([EmployeeFeedbackId]) REFERENCES [EmployeeFeedback] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PresentationRatings_InductionProgrammeSessions_InductionProgrammeSessionId] FOREIGN KEY ([InductionProgrammeSessionId]) REFERENCES [InductionProgrammeSessions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    CREATE INDEX [IX_InductionCases_InductionProgrammeId] ON [InductionCases] ([InductionProgrammeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    CREATE INDEX [IX_InductionProgrammeSessions_InductionProgrammeId_SortOrder] ON [InductionProgrammeSessions] ([InductionProgrammeId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PresentationRatings_EmployeeFeedbackId_InductionProgrammeSessionId] ON [PresentationRatings] ([EmployeeFeedbackId], [InductionProgrammeSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    CREATE INDEX [IX_PresentationRatings_InductionProgrammeSessionId] ON [PresentationRatings] ([InductionProgrammeSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    ALTER TABLE [InductionCases] ADD CONSTRAINT [FK_InductionCases_InductionProgrammes_InductionProgrammeId] FOREIGN KEY ([InductionProgrammeId]) REFERENCES [InductionProgrammes] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016073650_AddCorporateInductionProgrammesAndFeedback'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016073650_AddCorporateInductionProgrammesAndFeedback', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016121023_AddPresenterAttendanceWorkflow'
)
BEGIN
    ALTER TABLE [InductionProgrammeSessions] ADD [PresenterUsername] nvarchar(150) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016121023_AddPresenterAttendanceWorkflow'
)
BEGIN
    CREATE TABLE [ProgrammeAttendances] (
        [Id] int NOT NULL IDENTITY,
        [InductionProgrammeId] int NOT NULL,
        [InductionCaseId] int NOT NULL,
        [Status] int NOT NULL,
        [ConfirmedBy] nvarchar(150) NULL,
        [ConfirmedBySid] nvarchar(184) NULL,
        [AuthenticationType] nvarchar(100) NULL,
        [ConfirmationSource] nvarchar(50) NULL,
        [ConfirmedAtUtc] datetime2 NULL,
        [InvitationSentAtUtc] datetime2 NULL,
        [InvitationSentBy] nvarchar(150) NULL,
        [InvitationSentTo] nvarchar(254) NULL,
        CONSTRAINT [PK_ProgrammeAttendances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProgrammeAttendances_InductionCases_InductionCaseId] FOREIGN KEY ([InductionCaseId]) REFERENCES [InductionCases] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProgrammeAttendances_InductionProgrammes_InductionProgrammeId] FOREIGN KEY ([InductionProgrammeId]) REFERENCES [InductionProgrammes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016121023_AddPresenterAttendanceWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProgrammeAttendances_InductionCaseId] ON [ProgrammeAttendances] ([InductionCaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016121023_AddPresenterAttendanceWorkflow'
)
BEGIN
    CREATE INDEX [IX_ProgrammeAttendances_InductionProgrammeId_Status] ON [ProgrammeAttendances] ([InductionProgrammeId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016121023_AddPresenterAttendanceWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016121023_AddPresenterAttendanceWorkflow', N'10.0.13');
END;

COMMIT;
GO

