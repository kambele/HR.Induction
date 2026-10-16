using System.ComponentModel.DataAnnotations;

namespace Namfisa.Induction.Models;

public enum MasterDataCategory
{
    Department = 0,
    Division = 1,
    ResponsibleRole = 2,
    ChecklistSection = 3,
    EmployeeTitle = 4,
    Gender = 5,
    Position = 6
}

public class MasterDataItem
{
    public int Id { get; set; }
    public MasterDataCategory Category { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(150)]
    public string UpdatedBy { get; set; } = string.Empty;
}

public class WorkflowConfiguration
{
    public int Id { get; set; }
    public bool RequireMandatoryTasksBeforeSignOff { get; set; } = true;
    public bool RequireEmployeeFeedbackBeforeSignOff { get; set; } = true;
    public bool EnforceSequentialSignOff { get; set; } = true;
    public bool LockChecklistWhenSignOffStarts { get; set; } = true;

    [Required, StringLength(300)]
    public string EmailSubjectTemplate { get; set; } = "Your NAMFISA induction plan - {EmployeeName}";

    [Required, StringLength(4000)]
    public string EmailBodyTemplate { get; set; } =
        "Hello {FirstName},\r\n\r\nYour NAMFISA induction plan is ready. Please open the plan to review your induction activities, provide feedback and complete your sign-off.\r\n\r\nPosition: {Position}\r\nDepartment: {Department}\r\nAppointment date: {AppointmentDate}\r\n\r\nYou will be signed in with your NAMFISA Windows account.\r\n\r\nRegards,\r\nNAMFISA Human Resources";

    [Required, StringLength(1000)]
    public string EmployeeAcknowledgement { get; set; } =
        "I confirm that I received the induction information, explanations and documentation recorded in this checklist.";

    [Required, StringLength(1000)]
    public string LineManagerAcknowledgement { get; set; } =
        "I confirm that the employee received induction relevant to the role and division.";

    [Required, StringLength(1000)]
    public string HrBusinessPartnerAcknowledgement { get; set; } =
        "I confirm that the required induction process and awareness activities were completed.";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(150)]
    public string UpdatedBy { get; set; } = string.Empty;
}
