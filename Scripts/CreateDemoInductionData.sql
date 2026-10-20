/*
    NAMFISA Employee Induction demo-data generator

    Creates:
      - 3 induction programmes with checklist phases and presentation schedules
      - 12 employee induction records with generated access tokens
      - checklist tasks copied from the active checklist templates
      - attendance records with a mixture of present, absent and pending states
      - programme feedback and per-presentation ratings
      - training evaluation responses
      - sign-offs appropriate to each induction status

    Safety:
      1. Review the script and confirm that HRInduction is the intended database.
      2. Change @ApplyDemoData from 0 to 1 before running.
      3. Demo email addresses use the reserved example.invalid domain and cannot
         receive invitations.
      4. The script is transactional and idempotent. It can be run repeatedly
         without duplicating its programmes, employees, tasks or feedback.
*/

USE [HRInduction];
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ApplyDemoData bit = 0;

IF @ApplyDemoData <> 1
BEGIN
    THROW 51000, 'Demo data was not created. Review the script and set @ApplyDemoData to 1 when ready.', 1;
END;

DECLARE @NowUtc datetime2(7) = SYSUTCDATETIME();
DECLARE @CurrentMonth date = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
DECLARE @SeedUser nvarchar(150) = N'DEMO-SEED';

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Programmes TABLE
    (
        Code nvarchar(20) NOT NULL PRIMARY KEY,
        Title nvarchar(200) NOT NULL,
        ProgrammeDate date NOT NULL,
        Venue nvarchar(200) NOT NULL,
        Status int NOT NULL
    );

    INSERT INTO @Programmes (Code, Title, ProgrammeDate, Venue, Status)
    VALUES
        (N'PRG-001', N'[DEMO] Corporate Induction Alpha', DATEADD(DAY, 9, DATEADD(MONTH, -2, @CurrentMonth)), N'Gold Boardroom', 2),
        (N'PRG-002', N'[DEMO] Corporate Induction Beta',  DATEADD(DAY, 13, DATEADD(MONTH, -1, @CurrentMonth)), N'Training Room 1', 2),
        (N'PRG-003', N'[DEMO] Corporate Induction Gamma', DATEADD(DAY, 7, DATEADD(MONTH,  1, @CurrentMonth)), N'Gold Boardroom', 1);

    INSERT INTO dbo.InductionProgrammes
    (
        Title, ProgrammeDate, Venue, Notes, Status,
        CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc
    )
    SELECT
        source.Title,
        source.ProgrammeDate,
        source.Venue,
        CONCAT(N'DEMO-SEED:', source.Code, N'. Safe to remove from Administration.'),
        source.Status,
        @SeedUser,
        @NowUtc,
        @SeedUser,
        @NowUtc
    FROM @Programmes source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.InductionProgrammes existing
        WHERE existing.Title = source.Title
    );

    DECLARE @ProgrammeIds TABLE
    (
        Code nvarchar(20) NOT NULL PRIMARY KEY,
        Id int NOT NULL
    );

    INSERT INTO @ProgrammeIds (Code, Id)
    SELECT source.Code, programme.Id
    FROM @Programmes source
    INNER JOIN dbo.InductionProgrammes programme ON programme.Title = source.Title;

    INSERT INTO dbo.InductionProgrammeChecklistPhases (InductionProgrammeId, Phase)
    SELECT programme.Id, phase.Phase
    FROM @ProgrammeIds programme
    CROSS JOIN (VALUES (0), (1), (2), (3)) phase(Phase)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.InductionProgrammeChecklistPhases existing
        WHERE existing.InductionProgrammeId = programme.Id
          AND existing.Phase = phase.Phase
    );

    DECLARE @Sessions TABLE
    (
        ProgrammeCode nvarchar(20) NOT NULL,
        SortOrder int NOT NULL,
        Division nvarchar(120) NOT NULL,
        PresenterName nvarchar(150) NOT NULL,
        PresenterUsername nvarchar(150) NOT NULL,
        StartTime time(0) NOT NULL,
        EndTime time(0) NOT NULL,
        PRIMARY KEY (ProgrammeCode, SortOrder)
    );

    INSERT INTO @Sessions
    (
        ProgrammeCode, SortOrder, Division, PresenterName,
        PresenterUsername, StartTime, EndTime
    )
    SELECT programme.Code, schedule.SortOrder, schedule.Division, schedule.PresenterName,
           schedule.PresenterUsername, schedule.StartTime, schedule.EndTime
    FROM @Programmes programme
    CROSS JOIN
    (
        VALUES
            (10, N'Human Resources', N'Demo HR Presenter', N'NAMFISA\demo.hrpresenter', CAST('08:30' AS time(0)), CAST('09:15' AS time(0))),
            (20, N'Governance, Risk and Compliance', N'Demo Risk Presenter', N'NAMFISA\demo.riskpresenter', CAST('09:15' AS time(0)), CAST('10:00' AS time(0))),
            (30, N'Information and Communication Technology', N'Demo ICT Presenter', N'NAMFISA\demo.ictpresenter', CAST('10:15' AS time(0)), CAST('11:00' AS time(0))),
            (40, N'Finance and Administration', N'Demo Finance Presenter', N'NAMFISA\demo.financepresenter', CAST('11:00' AS time(0)), CAST('11:45' AS time(0))),
            (50, N'Market Conduct', N'Demo Conduct Presenter', N'NAMFISA\demo.conductpresenter', CAST('13:00' AS time(0)), CAST('13:45' AS time(0)))
    ) schedule(SortOrder, Division, PresenterName, PresenterUsername, StartTime, EndTime);

    INSERT INTO dbo.InductionProgrammeSessions
    (
        InductionProgrammeId, Division, PresenterName, PresenterUsername,
        StartTime, EndTime, SortOrder
    )
    SELECT
        programme.Id,
        session.Division,
        session.PresenterName,
        session.PresenterUsername,
        session.StartTime,
        session.EndTime,
        session.SortOrder
    FROM @Sessions session
    INNER JOIN @ProgrammeIds programme ON programme.Code = session.ProgrammeCode
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.InductionProgrammeSessions existing
        WHERE existing.InductionProgrammeId = programme.Id
          AND existing.SortOrder = session.SortOrder
    );

    DECLARE @Employees TABLE
    (
        Code nvarchar(20) NOT NULL PRIMARY KEY,
        ProgrammeCode nvarchar(20) NOT NULL,
        EmployeeTitle nvarchar(30) NOT NULL,
        FirstName nvarchar(100) NOT NULL,
        LastName nvarchar(100) NOT NULL,
        Gender nvarchar(50) NOT NULL,
        Position nvarchar(150) NOT NULL,
        Department nvarchar(120) NOT NULL,
        Division nvarchar(120) NOT NULL,
        AppointmentOffsetDays int NOT NULL,
        InductionStatus int NOT NULL,
        AttendanceStatus int NOT NULL,
        ProgrammeUnderstandingRating int NULL,
        OverallProgrammeRating int NULL,
        HasTrainingEvaluation bit NOT NULL
    );

    INSERT INTO @Employees
    (
        Code, ProgrammeCode, EmployeeTitle, FirstName, LastName, Gender,
        Position, Department, Division, AppointmentOffsetDays,
        InductionStatus, AttendanceStatus, ProgrammeUnderstandingRating,
        OverallProgrammeRating, HasTrainingEvaluation
    )
    VALUES
        (N'EMP-001', N'PRG-001', N'Ms', N'Demo', N'Employee01', N'Female', N'Compliance Analyst', N'Employee Services', N'Governance, Risk and Compliance', -14, 3, 1, 5, 5, 1),
        (N'EMP-002', N'PRG-001', N'Mr', N'Demo', N'Employee02', N'Male', N'ICT Support Officer', N'Business Systems & Services', N'Information and Communication Technology', -10, 3, 1, 4, 4, 0),
        (N'EMP-003', N'PRG-001', N'Mrs', N'Demo', N'Employee03', N'Female', N'Financial Accountant', N'Employee Services', N'Finance and Administration', -7, 2, 1, 4, 4, 1),
        (N'EMP-004', N'PRG-001', N'Mr', N'Demo', N'Employee04', N'Male', N'Market Conduct Officer', N'Employee Services', N'Market Conduct', -4, 3, 1, 3, 3, 0),
        (N'EMP-005', N'PRG-002', N'Dr', N'Demo', N'Employee05', N'Female', N'Research Analyst', N'Employee Services', N'Research, Policy and Statistics', -12, 3, 1, 5, 5, 1),
        (N'EMP-006', N'PRG-002', N'Ms', N'Demo', N'Employee06', N'Female', N'Legal Officer', N'Employee Services', N'Legal and Licensing Services', -8, 1, 1, 4, 4, 0),
        (N'EMP-007', N'PRG-002', N'Mr', N'Demo', N'Employee07', N'Male', N'Insurance Analyst', N'Employee Services', N'Insurance and Medical Aid Funds', -5, 1, 2, NULL, NULL, 0),
        (N'EMP-008', N'PRG-002', N'Ms', N'Demo', N'Employee08', N'Female', N'Internal Auditor', N'Employee Services', N'Internal Audit', -2, 2, 1, 3, 3, 1),
        (N'EMP-009', N'PRG-003', N'Mr', N'Demo', N'Employee09', N'Male', N'Pension Funds Analyst', N'Employee Services', N'Pension Funds and Friendly Societies', -3, 0, 0, NULL, NULL, 0),
        (N'EMP-010', N'PRG-003', N'Ms', N'Demo', N'Employee10', N'Female', N'Communications Officer', N'Employee Services', N'Corporate Communications and Consumer Education', -2, 0, 0, NULL, NULL, 0),
        (N'EMP-011', N'PRG-003', N'Mr', N'Demo', N'Employee11', N'Male', N'Project Officer', N'Business Systems & Services', N'Strategy and Projects', -1, 0, 0, NULL, NULL, 0),
        (N'EMP-012', N'PRG-003', N'Adv', N'Demo', N'Employee12', N'Prefer not to say', N'Capital Markets Analyst', N'Employee Services', N'Capital Markets', 0, 0, 0, NULL, NULL, 0);

    INSERT INTO dbo.InductionCases
    (
        EmployeeTitle, FirstName, LastName, EmployeeName, Gender, Position,
        EmployeeUsername, EmployeeEmail, EmployeeNumber, Department, Division,
        AppointmentDate, LineManagerName, LineManagerUsername,
        HrBusinessPartnerName, HrBusinessPartnerUsername,
        Status, CreatedAtUtc, CreatedBy, InductionProgrammeId
    )
    SELECT
        employee.EmployeeTitle,
        employee.FirstName,
        employee.LastName,
        CONCAT(employee.EmployeeTitle, N' ', employee.FirstName, N' ', employee.LastName),
        employee.Gender,
        employee.Position,
        CONCAT(N'NAMFISA\demo.induction', RIGHT(employee.Code, 3)),
        CONCAT(N'demo.induction', RIGHT(employee.Code, 3), N'@example.invalid'),
        CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3)),
        employee.Department,
        employee.Division,
        DATEADD(DAY, employee.AppointmentOffsetDays, programmeDetails.ProgrammeDate),
        N'Demo Line Manager',
        N'NAMFISA\demo.manager',
        N'Demo HR Business Partner',
        N'NAMFISA\demo.hrbp',
        employee.InductionStatus,
        @NowUtc,
        @SeedUser,
        programme.Id
    FROM @Employees employee
    INNER JOIN @ProgrammeIds programme ON programme.Code = employee.ProgrammeCode
    INNER JOIN @Programmes programmeDetails ON programmeDetails.Code = employee.ProgrammeCode
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.InductionCases existing
        WHERE existing.EmployeeNumber = CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3))
           OR existing.EmployeeUsername = CONCAT(N'NAMFISA\demo.induction', RIGHT(employee.Code, 3))
    );

    INSERT INTO dbo.InductionTasks
    (
        InductionCaseId, ChecklistTemplateItemId, Section, Title, Phase,
        ResponsibleRole, AssignedToUsername, DueDate, IsMandatory,
        ExcludedByProgramme, SortOrder, Status, CompletedBy, CompletedAtUtc, Notes
    )
    SELECT
        induction.Id,
        template.Id,
        template.Section,
        template.Title,
        template.Phase,
        template.ResponsibleRole,
        CASE
            WHEN template.ResponsibleRole LIKE N'%employee%' THEN induction.EmployeeUsername
            WHEN template.ResponsibleRole LIKE N'%manager%' THEN induction.LineManagerUsername
            ELSE induction.HrBusinessPartnerUsername
        END,
        DATEADD(DAY, template.DueOffsetDays, induction.AppointmentDate),
        template.IsMandatory,
        CASE WHEN programmePhase.InductionProgrammeId IS NULL THEN 1 ELSE 0 END,
        template.SortOrder,
        CASE
            WHEN induction.Status IN (2, 3) THEN 1
            WHEN induction.Status = 1 AND template.SortOrder % 3 = 0 THEN 1
            ELSE 0
        END,
        CASE
            WHEN induction.Status IN (2, 3) THEN @SeedUser
            WHEN induction.Status = 1 AND template.SortOrder % 3 = 0 THEN @SeedUser
            ELSE NULL
        END,
        CASE
            WHEN induction.Status IN (2, 3) THEN DATEADD(DAY, template.DueOffsetDays, induction.AppointmentDate)
            WHEN induction.Status = 1 AND template.SortOrder % 3 = 0 THEN @NowUtc
            ELSE NULL
        END,
        CASE WHEN induction.Status IN (2, 3) THEN N'Completed by demo-data generator.' ELSE NULL END
    FROM dbo.InductionCases induction
    INNER JOIN @Employees employee ON induction.EmployeeNumber = CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3))
    INNER JOIN dbo.ChecklistTemplateItems template ON template.IsActive = 1
    LEFT JOIN dbo.InductionProgrammeChecklistPhases programmePhase
        ON programmePhase.InductionProgrammeId = induction.InductionProgrammeId
       AND programmePhase.Phase = template.Phase
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.InductionTasks existing
        WHERE existing.InductionCaseId = induction.Id
          AND existing.ChecklistTemplateItemId = template.Id
    );

    INSERT INTO dbo.ProgrammeAttendances
    (
        InductionProgrammeId, InductionCaseId, Status,
        ConfirmedBy, ConfirmedBySid, AuthenticationType, ConfirmationSource,
        ConfirmedAtUtc, InvitationSentAtUtc, InvitationSentBy, InvitationSentTo
    )
    SELECT
        induction.InductionProgrammeId,
        induction.Id,
        employee.AttendanceStatus,
        CASE WHEN employee.AttendanceStatus = 0 THEN NULL ELSE induction.EmployeeUsername END,
        NULL,
        CASE WHEN employee.AttendanceStatus = 0 THEN NULL ELSE N'Demo seed' END,
        CASE WHEN employee.AttendanceStatus = 0 THEN NULL ELSE N'Employee self-service' END,
        CASE WHEN employee.AttendanceStatus = 0 THEN NULL ELSE @NowUtc END,
        NULL,
        NULL,
        NULL
    FROM dbo.InductionCases induction
    INNER JOIN @Employees employee ON induction.EmployeeNumber = CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3))
    WHERE induction.InductionProgrammeId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.ProgrammeAttendances existing
          WHERE existing.InductionCaseId = induction.Id
      );

    INSERT INTO dbo.EmployeeFeedback
    (
        InductionCaseId, UnderstandsResponsibilities, AwareOfDevelopmentOpportunities,
        ProgrammeUnderstandingRating, TimeAllocationResponse, OverallProgrammeRating,
        MostValuableAspect, ImprovementRecommendations, MissingFromInduction,
        AdditionalComments, SubmittedBy, SubmittedAtUtc
    )
    SELECT
        induction.Id,
        1,
        1,
        employee.ProgrammeUnderstandingRating,
        CASE WHEN employee.OverallProgrammeRating = 3 THEN 2 ELSE 1 END,
        employee.OverallProgrammeRating,
        N'The divisional presentations and practical introduction to NAMFISA were most valuable.',
        N'Allow additional time for questions and include more role-specific examples.',
        N'No material topics were missing from the programme.',
        CONCAT(N'Demo feedback response for ', induction.EmployeeName, N'.'),
        induction.EmployeeUsername,
        DATEADD(HOUR, 2, CAST(programme.ProgrammeDate AS datetime2))
    FROM dbo.InductionCases induction
    INNER JOIN @Employees employee ON induction.EmployeeNumber = CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3))
    INNER JOIN @Programmes programme ON programme.Code = employee.ProgrammeCode
    WHERE employee.OverallProgrammeRating IS NOT NULL
      AND employee.AttendanceStatus = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.EmployeeFeedback existing
          WHERE existing.InductionCaseId = induction.Id
      );

    INSERT INTO dbo.PresentationRatings
    (
        EmployeeFeedbackId, InductionProgrammeSessionId, Rating
    )
    SELECT
        feedback.Id,
        session.Id,
        CASE
            WHEN session.SortOrder IN (20, 40) AND employee.OverallProgrammeRating > 1
                THEN employee.OverallProgrammeRating - 1
            ELSE employee.OverallProgrammeRating
        END
    FROM dbo.EmployeeFeedback feedback
    INNER JOIN dbo.InductionCases induction ON induction.Id = feedback.InductionCaseId
    INNER JOIN @Employees employee ON induction.EmployeeNumber = CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3))
    INNER JOIN dbo.InductionProgrammeSessions session ON session.InductionProgrammeId = induction.InductionProgrammeId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.PresentationRatings existing
        WHERE existing.EmployeeFeedbackId = feedback.Id
          AND existing.InductionProgrammeSessionId = session.Id
    );

    INSERT INTO dbo.TrainingEvaluations
    (
        InductionCaseId, CourseTitle, CourseProvider, CourseDate,
        ObjectivesClearlyDefined, ParticipationEncouraged, ContentOrganized,
        MaterialsHelpful, UsefulInWork, ObjectivesMet, TimeSufficient,
        FacilitiesAdequate, DisagreementImprovements, LikedMost, CouldImprove,
        OverallImpression, AdditionalComments, TrainerKnowledge,
        TrainerExplanation, TrainerAnswers, VenueComments, MealsComments,
        OfficialResultsAttached, CertificateAttached, AttendanceRegisterAttached,
        SubmittedBy, SubmittedAtUtc
    )
    SELECT
        induction.Id,
        N'[DEMO] Corporate Induction Training Evaluation',
        N'NAMFISA Human Resources',
        programme.ProgrammeDate,
        4, 4, 4, 3, 4, 4, 3, 4,
        N'More practical examples could improve the session.',
        N'The facilitator encouraged questions and participation.',
        N'Provide the reference material before the programme.',
        4,
        N'Demo training evaluation generated for reporting and workflow testing.',
        5, 4, 5,
        N'The venue was suitable and comfortable.',
        N'Refreshments were adequate.',
        0, 0, 1,
        induction.EmployeeUsername,
        DATEADD(HOUR, 3, CAST(programme.ProgrammeDate AS datetime2))
    FROM dbo.InductionCases induction
    INNER JOIN @Employees employee ON induction.EmployeeNumber = CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3))
    INNER JOIN @Programmes programme ON programme.Code = employee.ProgrammeCode
    WHERE employee.HasTrainingEvaluation = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.TrainingEvaluations existing
          WHERE existing.InductionCaseId = induction.Id
            AND existing.CourseTitle = N'[DEMO] Corporate Induction Training Evaluation'
      );

    INSERT INTO dbo.SignOffs
    (
        InductionCaseId, Role, SignedBy, ExpectedSignerUsername,
        SignedBySid, AuthenticationType, SignedAtUtc, Acknowledgement, Comments
    )
    SELECT
        induction.Id,
        role.Role,
        CASE role.Role
            WHEN 0 THEN induction.EmployeeUsername
            WHEN 1 THEN induction.LineManagerUsername
            WHEN 2 THEN induction.HrBusinessPartnerUsername
        END,
        CASE role.Role
            WHEN 0 THEN induction.EmployeeUsername
            WHEN 1 THEN induction.LineManagerUsername
            WHEN 2 THEN induction.HrBusinessPartnerUsername
        END,
        NULL,
        N'Demo seed',
        DATEADD(HOUR, role.Role + 4, CAST(programme.ProgrammeDate AS datetime2)),
        CASE role.Role
            WHEN 0 THEN N'I confirm that I participated in the employee induction programme.'
            WHEN 1 THEN N'I confirm that the employee induction requirements were reviewed.'
            WHEN 2 THEN N'I confirm that the employee induction record was reviewed and completed.'
        END,
        N'Generated by the demo-data script.'
    FROM dbo.InductionCases induction
    INNER JOIN @Employees employee ON induction.EmployeeNumber = CONCAT(N'DEMO-IND-', RIGHT(employee.Code, 3))
    INNER JOIN @Programmes programme ON programme.Code = employee.ProgrammeCode
    CROSS JOIN (VALUES (0), (1), (2)) role(Role)
    WHERE (induction.Status = 3 OR (induction.Status = 2 AND role.Role = 0))
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.SignOffs existing
          WHERE existing.InductionCaseId = induction.Id
            AND existing.Role = role.Role
      );

    COMMIT TRANSACTION;

    SELECT
        (SELECT COUNT(*) FROM dbo.InductionProgrammes WHERE Title LIKE N'[[]DEMO[]]%') AS DemoProgrammes,
        (SELECT COUNT(*) FROM dbo.InductionCases WHERE EmployeeNumber LIKE N'DEMO-IND-%') AS DemoEmployeeInductions,
        (SELECT COUNT(*)
         FROM dbo.EmployeeFeedback feedback
         INNER JOIN dbo.InductionCases induction ON induction.Id = feedback.InductionCaseId
         WHERE induction.EmployeeNumber LIKE N'DEMO-IND-%') AS DemoProgrammeFeedbackResponses,
        (SELECT COUNT(*)
         FROM dbo.PresentationRatings rating
         INNER JOIN dbo.EmployeeFeedback feedback ON feedback.Id = rating.EmployeeFeedbackId
         INNER JOIN dbo.InductionCases induction ON induction.Id = feedback.InductionCaseId
         WHERE induction.EmployeeNumber LIKE N'DEMO-IND-%') AS DemoPresentationRatings,
        (SELECT COUNT(*)
         FROM dbo.TrainingEvaluations evaluation
         INNER JOIN dbo.InductionCases induction ON induction.Id = evaluation.InductionCaseId
         WHERE induction.EmployeeNumber LIKE N'DEMO-IND-%') AS DemoTrainingEvaluations;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
