using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Controllers;

public class TrainingEvaluationsController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(int inductionCaseId)
    {
        var inductionCase = await dbContext.InductionCases
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == inductionCaseId);

        if (inductionCase is null) return NotFound();
        if (!accessService.CanSubmitFeedback(User, inductionCase)) return Forbid();

        ViewBag.EmployeeName = inductionCase.EmployeeName;
        ViewBag.InductionAccessToken = inductionCase.AccessToken.ToString("N");
        return View(new TrainingEvaluationInputModel
        {
            InductionCaseId = inductionCase.Id,
            CourseDate = DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TrainingEvaluationInputModel model)
    {
        var inductionCase = await dbContext.InductionCases
            .SingleOrDefaultAsync(x => x.Id == model.InductionCaseId);

        if (inductionCase is null) return NotFound();
        if (!accessService.CanSubmitFeedback(User, inductionCase)) return Forbid();

        if (!ModelState.IsValid)
        {
            ViewBag.EmployeeName = inductionCase.EmployeeName;
            ViewBag.InductionAccessToken = inductionCase.AccessToken.ToString("N");
            return View(model);
        }

        var evaluation = new TrainingEvaluation
        {
            InductionCaseId = inductionCase.Id,
            CourseTitle = model.CourseTitle.Trim(),
            CourseProvider = model.CourseProvider.Trim(),
            CourseDate = model.CourseDate.Date,
            ObjectivesClearlyDefined = model.ObjectivesClearlyDefined,
            ParticipationEncouraged = model.ParticipationEncouraged,
            ContentOrganized = model.ContentOrganized,
            MaterialsHelpful = model.MaterialsHelpful,
            UsefulInWork = model.UsefulInWork,
            ObjectivesMet = model.ObjectivesMet,
            TimeSufficient = model.TimeSufficient,
            FacilitiesAdequate = model.FacilitiesAdequate,
            DisagreementImprovements = model.DisagreementImprovements?.Trim(),
            LikedMost = model.LikedMost?.Trim(),
            CouldImprove = model.CouldImprove?.Trim(),
            OverallImpression = model.OverallImpression,
            AdditionalComments = model.AdditionalComments?.Trim(),
            TrainerKnowledge = model.TrainerKnowledge,
            TrainerExplanation = model.TrainerExplanation,
            TrainerAnswers = model.TrainerAnswers,
            VenueComments = model.VenueComments?.Trim(),
            MealsComments = model.MealsComments?.Trim(),
            OfficialResultsAttached = model.OfficialResultsAttached,
            CertificateAttached = model.CertificateAttached,
            AttendanceRegisterAttached = model.AttendanceRegisterAttached,
            SubmittedBy = accessService.CurrentUsername(User)
        };

        dbContext.TrainingEvaluations.Add(evaluation);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Training evaluation submitted.";
        return RedirectToAction("Details", "Inductions", new { id = inductionCase.AccessToken.ToString("N") });
    }

    public async Task<IActionResult> Details(int id)
    {
        var evaluation = await dbContext.TrainingEvaluations
            .AsNoTracking()
            .Include(x => x.InductionCase)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (evaluation is null) return NotFound();
        if (!accessService.CanView(User, evaluation.InductionCase)) return Forbid();
        return View(evaluation);
    }
}
