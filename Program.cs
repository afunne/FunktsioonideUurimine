using FunktsioonideUurimine.Andmed;
using FunktsioonideUurimine.Teenused;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<FunktsioonideKontekst>(valikud =>
    valikud.UseSqlite(builder.Configuration.GetConnectionString("Andmebaas")));
builder.Services.AddSingleton<FunktsiooniAnalyysija>();

var app = builder.Build();

// Rakendab migratsioonid ja lisab näidisandmed, kui andmebaas on tühi.
using (var ulatus = app.Services.CreateScope())
{
    var kontekst = ulatus.ServiceProvider.GetRequiredService<FunktsioonideKontekst>();
    var analyysija = ulatus.ServiceProvider.GetRequiredService<FunktsiooniAnalyysija>();

    var ootel = await kontekst.Database.GetPendingMigrationsAsync();
    bool lisatiKysimused = await kontekst.Database.CanConnectAsync()
        && (await kontekst.Database.GetAppliedMigrationsAsync()).Any()
        && ootel.Any(m => m.EndsWith("_UurimiseKysimused"));

    await kontekst.Database.MigrateAsync();
    await Naidisandmed.LisaAsync(kontekst, analyysija);
    await Naidisandmed.UuendaVanadAsync(kontekst, analyysija);
    if (lisatiKysimused)
        await Naidisandmed.LisaUlesandedAsync(kontekst, analyysija);
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Funktsioonid}/{action=Index}/{id?}");

app.Run();
