using FunktsioonideUurimine.Mudelid;
using FunktsioonideUurimine.Teenused;
using Microsoft.EntityFrameworkCore;

namespace FunktsioonideUurimine.Andmed;

public static class Naidisandmed
{
    private static readonly string[] Valemid =
    [
        "x^3 - 3*x",
        "x^2 - 4*x + 3",
        "x^3 - 12*x",
        "x^4 - 2*x^2",
        "1/(x - 1)",
        "sqrt(x)",
    ];

    // Moodle'i ülesannete funktsioonid (Teema 10 „Puutuja ja normaal“, Teema 11 „Funktsiooni uurimine tuletise abil“).
    private static readonly string[] Ulesanded =
    [
        // Teema 11
        "x^3 - 3*x^2 - 9*x + 5",
        "2*x^3 - 6*x",
        "x^4 - 4*x^3",
        "(x^2 - 1)/(x^2 + 1)",
        "x^4 - 8*x^2 + 3",
        "x/(x^2 + 4)",
        "x*exp(-x)",
        "2*x^3 - 9*x^2 + 12*x - 4",
        "0.25*x^4 - 2*x^2",
        // Teema 10
        "x^3 - 2*x",
        "x^3/3 - 4*x",
        "2*x^2 + 1",
        "x^2",
        "(x + 2)/(x - 1)",
        "exp(-x)",
        "x^2 - 3*x + 2",
        "x^3 - 3*x^2 + 5",
    ];

    // Tühja andmebaasi korral lisatakse näidisfunktsioonid ja ülesannete funktsioonid.
    public static async Task LisaAsync(FunktsioonideKontekst kontekst, FunktsiooniAnalyysija analyysija)
    {
        if (await kontekst.FunktsiooniUurimised.AnyAsync())
            return;

        await LisaPuuduvadAsync(kontekst, analyysija, [.. Valemid, .. Ulesanded]);
    }

    // Lisab ülesannete funktsioonid, mida andmebaasis veel pole (kasutatakse uute küsimuste migratsiooni järel).
    public static Task LisaUlesandedAsync(FunktsioonideKontekst kontekst, FunktsiooniAnalyysija analyysija) =>
        LisaPuuduvadAsync(kontekst, analyysija, Ulesanded);

    // Analüüsib uuesti kirjed, millel uute küsimuste vastused või liik puuduvad (salvestatud enne nende lisamist).
    public static async Task UuendaVanadAsync(FunktsioonideKontekst kontekst, FunktsiooniAnalyysija analyysija)
    {
        var vanad = await kontekst.FunktsiooniUurimised.Where(f => f.Muutumispiirkond == "" || f.Liik == "").ToListAsync();
        foreach (var olem in vanad)
        {
            try
            {
                analyysija.Analyysi(olem.Valem).KannaOlemisse(olem);
            }
            catch (ValemiViga)
            {
                // Valem, mida enam ei toetata, jääb muutmata.
            }
        }
        await kontekst.SaveChangesAsync();
    }

    private static async Task LisaPuuduvadAsync(FunktsioonideKontekst kontekst, FunktsiooniAnalyysija analyysija, IEnumerable<string> valemid)
    {
        var olemas = (await kontekst.FunktsiooniUurimised.Select(f => f.Valem).ToListAsync()).ToHashSet();
        foreach (var valem in valemid)
        {
            var tulemus = analyysija.Analyysi(valem);
            if (!olemas.Add(tulemus.Funktsioon.Valem))
                continue;
            var olem = new FunktsiooniUurimine();
            tulemus.KannaOlemisse(olem);
            await kontekst.FunktsiooniUurimised.AddAsync(olem);
        }
        await kontekst.SaveChangesAsync();
    }
}
