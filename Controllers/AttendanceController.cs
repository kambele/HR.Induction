using Microsoft.AspNetCore.Mvc;

namespace Namfisa.Induction.Controllers;

// Retains the previous route only to prevent old reusable attendance links from
// exposing an employee workflow. New invitations use EmployeePortalController.
public class AttendanceController : Controller
{
    [HttpGet]
    public IActionResult Confirm(int id) => RedirectToAction(
        "Unavailable",
        "EmployeePortal");

    [HttpPost]
    [ActionName("Confirm")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmPost(int inductionCaseId) => RedirectToAction(
        "Unavailable",
        "EmployeePortal");
}
