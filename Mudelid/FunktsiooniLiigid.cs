namespace FunktsioonideUurimine.Mudelid;

// Funktsiooni liik: võti (andmebaasis ja URL-is, nt ?liik=ruut) ja nimetus.
public record FunktsiooniLiik(string Voti, string Nimi);

// Funktsioonide liigid nimekirja rühmitamiseks ja filtreerimiseks; järjekord on nimekirja rühmade järjekord.
public static class FunktsiooniLiigid
{
    public const string Konstantne = "konstantne";
    public const string Lineaar = "lineaar";
    public const string Ruut = "ruut";
    public const string Kuup = "kuup";
    public const string Polynoom = "polynoom";
    public const string Ratsionaal = "ratsionaal";
    public const string Juur = "juur";
    public const string Eksponent = "eksponent";
    public const string Logaritm = "logaritm";
    public const string Trigonomeetriline = "trigonomeetriline";
    public const string Liit = "liit";

    public static readonly IReadOnlyList<FunktsiooniLiik> Koik =
    [
        new(Konstantne, "Konstantne funktsioon"),
        new(Lineaar, "Lineaarfunktsioon"),
        new(Ruut, "Ruutfunktsioon"),
        new(Kuup, "Kuupfunktsioon"),
        new(Polynoom, "Polünoomfunktsioon (aste ≥ 4)"),
        new(Ratsionaal, "Ratsionaalfunktsioon"),
        new(Juur, "Juurfunktsioon"),
        new(Eksponent, "Eksponentfunktsioon"),
        new(Logaritm, "Logaritmfunktsioon"),
        new(Trigonomeetriline, "Trigonomeetriline funktsioon"),
        new(Liit, "Liitfunktsioon (mitu liiki)"),
    ];

    public static string Nimi(string? voti) =>
        Koik.FirstOrDefault(l => l.Voti == voti)?.Nimi ?? "Määramata";
}

// Nimekirja filtrinupp: liik ja selle liigi funktsioonide arv.
public record LiigiFilter(FunktsiooniLiik Liik, int Arv);

// Nimekirja vaade: näidatavad funktsioonid, valitud liik (null = kõik) ja filtrinupud.
public record NimekirjaVaade(
    IReadOnlyList<FunktsiooniUurimine> Funktsioonid,
    string? ValitudLiik,
    IReadOnlyList<LiigiFilter> Liigid,
    int KokkuArv);
