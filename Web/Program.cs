using Data.Model.Data;
using Microsoft.AspNetCore.Identity;
using Web;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

// Add services to the container.
services
    .AddCoreServices(builder.Configuration)
    .AddIdentity(builder.Configuration)
    .AddEnableBanking()
    .AddCategorization(builder.Configuration)
    .AddControllersWithViews();

services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

using (var scope = services.BuildServiceProvider().CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync(Constants.AppRoles.USER))
        await roleManager.CreateAsync(new IdentityRole(Constants.AppRoles.USER));
    if (!await roleManager.RoleExistsAsync(Constants.AppRoles.ADMIN))
        await roleManager.CreateAsync(new IdentityRole(Constants.AppRoles.ADMIN));
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Exposes the implicit top-level Program class as a public type so the test project's
// WebApplicationFactory<Program> (used for integration tests, e.g. auth/routing checks
// against a real HTTP pipeline) can reference it.
public partial class Program { }
