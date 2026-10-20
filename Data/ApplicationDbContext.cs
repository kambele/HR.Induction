using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<InductionCase> InductionCases => Set<InductionCase>();
    public DbSet<ChecklistTemplateItem> ChecklistTemplateItems => Set<ChecklistTemplateItem>();
    public DbSet<InductionTask> InductionTasks => Set<InductionTask>();
    public DbSet<SignOff> SignOffs => Set<SignOff>();
    public DbSet<EmployeeFeedback> EmployeeFeedback => Set<EmployeeFeedback>();
    public DbSet<TrainingEvaluation> TrainingEvaluations => Set<TrainingEvaluation>();
    public DbSet<MasterDataItem> MasterDataItems => Set<MasterDataItem>();
    public DbSet<WorkflowConfiguration> WorkflowConfigurations => Set<WorkflowConfiguration>();
    public DbSet<InductionProgramme> InductionProgrammes => Set<InductionProgramme>();
    public DbSet<InductionProgrammeChecklistPhase> InductionProgrammeChecklistPhases => Set<InductionProgrammeChecklistPhase>();
    public DbSet<InductionProgrammeSession> InductionProgrammeSessions => Set<InductionProgrammeSession>();
    public DbSet<PresentationRating> PresentationRatings => Set<PresentationRating>();
    public DbSet<ProgrammeAttendance> ProgrammeAttendances => Set<ProgrammeAttendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InductionCase>()
            .HasIndex(x => x.AccessToken)
            .IsUnique();

        modelBuilder.Entity<InductionCase>()
            .HasMany(x => x.Tasks)
            .WithOne(x => x.InductionCase)
            .HasForeignKey(x => x.InductionCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionCase>()
            .HasMany(x => x.SignOffs)
            .WithOne(x => x.InductionCase)
            .HasForeignKey(x => x.InductionCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionCase>()
            .HasMany(x => x.TrainingEvaluations)
            .WithOne(x => x.InductionCase)
            .HasForeignKey(x => x.InductionCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionCase>()
            .HasOne(x => x.EmployeeFeedback)
            .WithOne(x => x.InductionCase)
            .HasForeignKey<EmployeeFeedback>(x => x.InductionCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionProgramme>()
            .HasMany(x => x.Participants)
            .WithOne(x => x.InductionProgramme)
            .HasForeignKey(x => x.InductionProgrammeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InductionProgramme>()
            .HasMany(x => x.Sessions)
            .WithOne(x => x.InductionProgramme)
            .HasForeignKey(x => x.InductionProgrammeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionProgramme>()
            .HasMany(x => x.ChecklistPhases)
            .WithOne(x => x.InductionProgramme)
            .HasForeignKey(x => x.InductionProgrammeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionProgrammeChecklistPhase>()
            .HasKey(x => new { x.InductionProgrammeId, x.Phase });

        modelBuilder.Entity<InductionProgramme>()
            .HasMany(x => x.Attendances)
            .WithOne(x => x.InductionProgramme)
            .HasForeignKey(x => x.InductionProgrammeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionCase>()
            .HasOne(x => x.ProgrammeAttendance)
            .WithOne(x => x.InductionCase)
            .HasForeignKey<ProgrammeAttendance>(x => x.InductionCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmployeeFeedback>()
            .HasMany(x => x.PresentationRatings)
            .WithOne(x => x.EmployeeFeedback)
            .HasForeignKey(x => x.EmployeeFeedbackId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InductionProgrammeSession>()
            .HasMany(x => x.Ratings)
            .WithOne(x => x.InductionProgrammeSession)
            .HasForeignKey(x => x.InductionProgrammeSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SignOff>()
            .HasIndex(x => new { x.InductionCaseId, x.Role })
            .IsUnique();

        modelBuilder.Entity<EmployeeFeedback>()
            .HasIndex(x => x.InductionCaseId)
            .IsUnique();

        modelBuilder.Entity<PresentationRating>()
            .HasIndex(x => new { x.EmployeeFeedbackId, x.InductionProgrammeSessionId })
            .IsUnique();

        modelBuilder.Entity<InductionProgrammeSession>()
            .HasIndex(x => new { x.InductionProgrammeId, x.SortOrder });

        modelBuilder.Entity<ProgrammeAttendance>()
            .HasIndex(x => new { x.InductionProgrammeId, x.Status });

        modelBuilder.Entity<ChecklistTemplateItem>()
            .HasIndex(x => new { x.Phase, x.SortOrder });

        modelBuilder.Entity<MasterDataItem>()
            .HasIndex(x => new { x.Category, x.Name })
            .IsUnique();

        modelBuilder.Entity<MasterDataItem>()
            .HasIndex(x => new { x.Category, x.SortOrder });

        base.OnModelCreating(modelBuilder);
    }
}
