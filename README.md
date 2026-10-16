# NAMFISA Employee Induction

An ASP.NET Core MVC application for planning, conducting and signing off employee inductions. The application uses Windows authentication, SQL Server and Entity Framework Core.

## Included in this MVP

- HR dashboard with active, overdue, awaiting sign-off and completed counts
- Employee induction plan creation
- Structured employee profile with configurable title, gender and position master data
- Checklist generation for pre-start, first-day, first-two-week and divisional induction activities
- Task assignment to Windows usernames, due-date adjustment, completion and not-applicable reasons
- Employee induction feedback
- Training evaluation questionnaire and trainer ratings
- Sequential employee, line manager and HR business partner sign-off
- Windows username and UTC timestamp audit information
- Role-specific AD ID enforcement with the assigned account, signing account, Windows SID and authentication method retained for sign-off audit
- Role-aware access to employee records
- HR administration workspace for master data, checklist templates and workflow controls
- Configurable employee titles, genders, positions, departments, divisions, responsible roles and checklist sections
- Configurable sign-off rules, checklist locking, email wording and acknowledgement wording
- Email-link sharing through the user's default mail app, with optional direct SMTP delivery
- Branded PDF induction report download containing checklist, feedback, training and sign-off details
- SQL Server migration and development sample data

## Prerequisites

- Windows
- .NET 10 SDK for development
- .NET 10 Hosting Bundle on the IIS server
- SQL Server Express instance `MSHIGWEDHA\SQLEXPRESS`
- Active Directory accounts for employees and responsible staff

## Run locally

From this folder:

```powershell
dotnet tool restore
dotnet restore
dotnet run
```

The application uses the `NMFSInduction` database on `MSHIGWEDHA\SQLEXPRESS`. At startup, it applies the Entity Framework Core migrations and seeds the checklist templates. The Development profile also seeds one sample employee induction. All authenticated users are treated as HR administrators in Development so the workflow can be evaluated without creating an Active Directory group first.

## Production configuration

1. Confirm that `ConnectionStrings:InductionDatabase` points to the approved SQL Server instance and database.
2. Set `Security:HrWindowsGroup` to the approved Active Directory HR group, or add individual accounts to `Security:HrUsers`.
3. Keep `Security:AllowAllAuthenticatedUsersInDevelopment` disabled outside Development.
4. Keep `Demo:SeedSampleCase` disabled outside Development.
5. Grant the IIS application-pool identity access to the application database when using integrated SQL Server security.
6. Configure the approved SMTP server and sender details. Induction invitations are sent directly by the application to the employee email recorded on each induction plan.

HR business partners can open **Administration** in the application navigation to maintain operational values and workflow wording. Master-data and checklist records are archived rather than deleted, and existing employee induction tasks retain their original wording for audit purposes.

Example configuration:

```json
{
  "ConnectionStrings": {
    "InductionDatabase": "Server=MSHIGWEDHA\\SQLEXPRESS;Database=NMFSInduction;Integrated Security=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True"
  },
  "Security": {
    "HrWindowsGroup": "NAMFISA\\HR Induction Administrators",
    "HrUsers": [
      "NAMFISA\\approved.user"
    ],
    "AllowAllAuthenticatedUsersInDevelopment": false
  },
  "Demo": {
    "SeedSampleCase": false
  },
  "Email": {
    "SmtpHost": "",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "",
    "Password": "",
    "FromAddress": "",
    "FromName": "NAMFISA Human Resources",
    "PublicBaseUrl": "https://intranet.example/hr-inductions",
    "TimeoutMilliseconds": 30000
  }
}
```

For IIS, the same values can be supplied as environment variables: `Email__SmtpHost`, `Email__SmtpPort`, `Email__UseSsl`, `Email__Username`, `Email__Password`, `Email__FromAddress`, `Email__FromName`, and `Email__PublicBaseUrl`. Set `PublicBaseUrl` to the employee-accessible application root, including `/hr-inductions`. Leave the username blank only when the SMTP server accepts the IIS application-pool identity through Windows credentials.

## IIS deployment

1. Publish with `dotnet publish -c Release -o .\\publish`.
2. Create an IIS application pool with **No Managed Code**.
3. Create the IIS site and point it to the publish folder.
4. Disable Anonymous Authentication and enable Windows Authentication.
5. Configure the application pool to use an approved domain service account when that identity will connect to SQL Server.
6. Set `ASPNETCORE_ENVIRONMENT` to `Production`.
7. Confirm that users access the application through an intranet address recognised as a Local Intranet site so their Windows credentials can flow automatically.

The application applies pending Entity Framework Core migrations and seeds the checklist templates on startup. Production database schema changes should be reviewed and backed up before deploying a new version.

## Checklist sources

The initial checklist template reflects the supplied Workplace Induction Checklist and Divisional Induction Checklist. The training evaluation screen reflects the supplied Training Evaluation Form. Named people from the Word documents were deliberately not embedded in the application; HR assigns current Windows accounts to tasks instead.

## Recommended next phase

- Scheduled email or Teams reminders and escalation
- Secure document and certificate uploads
- HR checklist-template maintenance screens
- Reporting and trend analysis
- Integration with the HR employee master-data source
"# HR.Induction" 
