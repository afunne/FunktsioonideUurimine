namespace FunktsioonideUurimine.Mudelid;

// Üks uurimisküsimus: võti (JSON/HTML), silt, rühm ja vastus olemist. OnValem = näidatakse koodina.
public record Kysimus(string Voti, string Silt, string Ruhm, Func<FunktsiooniUurimine, string> Vastus, bool OnValem = false);

// Kõik küsimused, millele rakendus vastab (Moodle: Teema 8 „Funktsiooni uurimine“ ja Teema 11 „Funktsiooni uurimine tuletise abil“).
public static class Kysimused
{
    public const string Uurimine = "Funktsiooni uurimine";
    public const string TuletiseAbil = "Uurimine tuletise abil";

    public static readonly IReadOnlyList<Kysimus> Koik =
    [
        new("liik", "Funktsiooni liik", Uurimine, f => FunktsiooniLiigid.Nimi(f.Liik)),
        new("maaramispiirkond", "Määramispiirkond X", Uurimine, f => f.Maaramispiirkond),
        new("muutumispiirkond", "Muutumispiirkond Y", Uurimine, f => f.Muutumispiirkond),
        new("nullkohad", "Nullkohad X₀", Uurimine, f => f.Nullkohad),
        new("positiivsuspiirkond", "Positiivsuspiirkond X⁺", Uurimine, f => f.Positiivsuspiirkond),
        new("negatiivsuspiirkond", "Negatiivsuspiirkond X⁻", Uurimine, f => f.Negatiivsuspiirkond),
        new("paarsus", "Paarsus", Uurimine, f => f.Paarsus),
        new("perioodilisus", "Perioodilisus", Uurimine, f => f.Perioodilisus),
        new("yTeljeLoikepunkt", "Lõikepunkt y-teljega", Uurimine, f => f.YTeljeLoikepunkt),
        new("asumptoodid", "Asümptoodid", Uurimine, f => f.Asumptoodid),

        new("tuletis", "Tuletis f'(x)", TuletiseAbil, f => f.Tuletis, OnValem: true),
        new("kriitilisedPunktid", "Kriitilised punktid (f'(x) = 0)", TuletiseAbil, f => f.KriitilisedPunktid),
        new("kasvamisvahemikud", "Kasvamisvahemikud X↑ (f'(x) > 0)", TuletiseAbil, f => f.Kasvamisvahemikud),
        new("kahanemisvahemikud", "Kahanemisvahemikud X↓ (f'(x) < 0)", TuletiseAbil, f => f.Kahanemisvahemikud),
        new("ekstreemumid", "Ekstreemumkohad Xₑ", TuletiseAbil, f => f.Ekstreemumid),
        new("teineTuletis", "Teine tuletis f''(x)", TuletiseAbil, f => f.TeineTuletis, OnValem: true),
        new("kumerusvahemikud", "Kumer ∩ (f''(x) < 0)", TuletiseAbil, f => f.Kumerusvahemikud),
        new("nogususvahemikud", "Nõgus ∪ (f''(x) > 0)", TuletiseAbil, f => f.Nogususvahemikud),
        new("kaanupunktid", "Käänupunktid", TuletiseAbil, f => f.Kaanupunktid),
    ];

    public static Dictionary<string, string> Vastused(FunktsiooniUurimine olem) =>
        Koik.ToDictionary(k => k.Voti, k => k.Vastus(olem));
}

// Vastuste osavaade: kindla olemi vastused (detailvaade) või tühjad väljad, mille täidab graafik.js (eelvaade).
public record VastusteSeaded(FunktsiooniUurimine? Olem);
