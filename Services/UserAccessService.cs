using System.Security.Claims;
using System.Security.Principal;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.Services;

public interface IUserAccessService
{
    string CurrentUsername(ClaimsPrincipal user);
    bool IsHr(ClaimsPrincipal user);
    bool CanView(ClaimsPrincipal user, InductionCase inductionCase);
    bool CanComplete(ClaimsPrincipal user, InductionTask task);
    bool CanSubmitFeedback(ClaimsPrincipal user, InductionCase inductionCase);
    bool CanManageProgrammeAttendance(ClaimsPrincipal user, InductionProgramme programme);
    bool CanSelfConfirmAttendance(ClaimsPrincipal user, InductionCase inductionCase);
    bool CanSign(ClaimsPrincipal user, InductionCase inductionCase, SignOffRole role);
}

public class UserAccessService(IConfiguration configuration, IWebHostEnvironment environment) : IUserAccessService
{
    private static readonly StringComparer UserComparer = StringComparer.OrdinalIgnoreCase;

    public string CurrentUsername(ClaimsPrincipal user) => user.Identity?.Name ?? string.Empty;

    public bool IsHr(ClaimsPrincipal user)
    {
        if (environment.IsDevelopment() && configuration.GetValue<bool>("Security:AllowAllAuthenticatedUsersInDevelopment"))
        {
            return user.Identity?.IsAuthenticated == true;
        }

        var current = CurrentUsername(user);
        var configuredUsers = configuration.GetSection("Security:HrUsers").Get<string[]>() ?? [];
        if (configuredUsers.Any(x => UserComparer.Equals(x, current)))
        {
            return true;
        }

        var hrGroup = configuration["Security:HrWindowsGroup"];
        return !string.IsNullOrWhiteSpace(hrGroup)
            && user.Identity is WindowsIdentity windowsIdentity
            && new WindowsPrincipal(windowsIdentity).IsInRole(hrGroup);
    }

    public bool CanView(ClaimsPrincipal user, InductionCase inductionCase)
    {
        if (IsHr(user)) return true;
        var current = CurrentUsername(user);
        return Matches(current, inductionCase.EmployeeUsername)
            || Matches(current, inductionCase.LineManagerUsername)
            || Matches(current, inductionCase.HrBusinessPartnerUsername)
            || inductionCase.Tasks.Any(x => !x.ExcludedByProgramme && Matches(current, x.AssignedToUsername));
    }

    public bool CanComplete(ClaimsPrincipal user, InductionTask task) =>
        IsHr(user) || Matches(CurrentUsername(user), task.AssignedToUsername);

    public bool CanSubmitFeedback(ClaimsPrincipal user, InductionCase inductionCase) =>
        IsHr(user) || Matches(CurrentUsername(user), inductionCase.EmployeeUsername);

    public bool CanManageProgrammeAttendance(ClaimsPrincipal user, InductionProgramme programme) =>
        IsHr(user) || programme.Sessions.Any(x => Matches(CurrentUsername(user), x.PresenterUsername));

    public bool CanSelfConfirmAttendance(ClaimsPrincipal user, InductionCase inductionCase) =>
        Matches(CurrentUsername(user), inductionCase.EmployeeUsername);

    public bool CanSign(ClaimsPrincipal user, InductionCase inductionCase, SignOffRole role)
    {
        if (environment.IsDevelopment() && IsHr(user)) return true;

        var expectedUsername = role switch
        {
            SignOffRole.Employee => inductionCase.EmployeeUsername,
            SignOffRole.LineManager => inductionCase.LineManagerUsername,
            SignOffRole.HrBusinessPartner => inductionCase.HrBusinessPartnerUsername,
            _ => string.Empty
        };

        return Matches(CurrentUsername(user), expectedUsername);
    }

    private static bool Matches(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first)
        && !string.IsNullOrWhiteSpace(second)
        && UserComparer.Equals(first.Trim(), second.Trim());
}
