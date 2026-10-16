using Namfisa.Induction.Models;

namespace Namfisa.Induction.Helpers;

public static class DisplayText
{
    public static string For(InductionPhase value) => value switch
    {
        InductionPhase.PreStart => "Before starting",
        InductionPhase.FirstDay => "First day",
        InductionPhase.FirstTwoWeeks => "First two weeks",
        InductionPhase.Divisional => "Divisional induction",
        _ => value.ToString()
    };

    public static string For(InductionStatus value) => value switch
    {
        InductionStatus.InProgress => "In progress",
        InductionStatus.AwaitingSignOff => "Awaiting sign-off",
        InductionStatus.OnHold => "On hold",
        _ => value.ToString()
    };

    public static string For(SignOffRole value) => value switch
    {
        SignOffRole.LineManager => "Line manager",
        SignOffRole.HrBusinessPartner => "HR business partner",
        _ => value.ToString()
    };

    public static string For(InductionTaskStatus value) => value switch
    {
        InductionTaskStatus.NotStarted => "Not started",
        InductionTaskStatus.NotApplicable => "Not applicable",
        _ => value.ToString()
    };

    public static string For(MasterDataCategory value) => value switch
    {
        MasterDataCategory.ResponsibleRole => "Responsible roles",
        MasterDataCategory.ChecklistSection => "Checklist sections",
        MasterDataCategory.EmployeeTitle => "Employee titles",
        MasterDataCategory.Gender => "Genders",
        MasterDataCategory.Position => "Positions",
        MasterDataCategory.Department => "Departments",
        MasterDataCategory.Division => "Divisions",
        _ => value.ToString()
    };
}
