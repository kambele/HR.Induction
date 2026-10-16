using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("InductionDatabase")
    ?? throw new InvalidOperationException("Connection string 'InductionDatabase' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<IUserAccessService, UserAccessService>();
builder.Services.AddScoped<IInductionWorkflowService, InductionWorkflowService>();
builder.Services.AddScoped<IInductionImportService, InductionImportService>();
builder.Services.AddScoped<IInductionReportService, InductionReportService>();
builder.Services.AddScoped<IInductionEmailService, InductionEmailService>();
builder.Services.AddScoped<IWorkflowConfigurationService, WorkflowConfigurationService>();
builder.Services.Configure<InductionEmailOptions>(builder.Configuration.GetSection("Email"));

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
   .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    // By default, all incoming requests will be authorized according to the default policy.
    options.FallbackPolicy = options.DefaultPolicy;
});
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await SeedData.InitializeAsync(
        dbContext,
        scope.ServiceProvider.GetRequiredService<IInductionWorkflowService>(),
        app.Configuration,
        app.Environment);
}

app.Run();

public partial class Program;
