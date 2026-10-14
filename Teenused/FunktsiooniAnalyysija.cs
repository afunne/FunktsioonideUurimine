using System.Globalization;
using System.Text.RegularExpressions;
using FunktsioonideUurimine.Mudelid;
using MathNet.Symbolics;

namespace FunktsioonideUurimine.Teenused;

public class ValemiViga(string sonum) : Exception(sonum);

public record KoostatudFunktsioon(
    string Valem,
    string Tuletis,
    string TeineTuletis,
    Func<double, double> F,
    Func<double, double> Tuletisfunktsioon,
    Func<double, double> TeineTuletisfunktsioon,
    string Liik,
    SymbolicExpression Avaldis);

public record Ekstreemum(double X, double Y, bool OnMaksimum);

public record Punkt(double X, double Y);

// Vertikaalne asümptoot x = X; horisontaalne (K = 0) või kaldasümptoot y = K*x + B.
public record Asumptoot(bool OnVertikaalne, double X, double K, double B);

public class AnalyysiTulemus
{
    public required KoostatudFunktsioon Funktsioon { get; init; }

    // Teema 8: funktsiooni uurimine
    public required string Maaramispiirkond { get; init; }
    public required string Muutumispiirkond { get; init; }
    public required string Nullkohad { get; init; }
    public required string Positiivsuspiirkond { get; init; }
    public required string Negatiivsuspiirkond { get; init; }
    public required string Paarsus { get; init; }
    public required string Perioodilisus { get; init; }
    public required string YTeljeLoikepunkt { get; init; }
    public required string Asumptoodid { get; init; }

    // Teema 11: funktsiooni uurimine tuletise abil
    public required string KriitilisedPunktid { get; init; }
    public required string Kasvamisvahemikud { get; init; }
    public required string Kahanemisvahemikud { get; init; }
    public required string Ekstreemumid { get; init; }
    public required string Kumerusvahemikud { get; init; }
    public required string Nogususvahemikud { get; init; }
    public required string Kaanupunktid { get; init; }

    // Graafiku jaoks
    public required IReadOnlyList<double> NullkohtadeX { get; init; }
    public required IReadOnlyList<Ekstreemum> EkstreemumidePunktid { get; init; }
    public required IReadOnlyList<Punkt> KaanupunktidePunktid { get; init; }
    public required IReadOnlyList<Asumptoot> AsumptootideAndmed { get; init; }
    public required double SoovitatudXMin { get; init; }
    public required double SoovitatudXMax { get; init; }

    // Vahetulemused lahenduskäikude jaoks (FunktsiooniAnalyysija.Lahendused).
    internal FunktsiooniAnalyysija.Vaheandmed? Andmed { get; init; }

    public void KannaOlemisse(FunktsiooniUurimine olem)
    {
        olem.Valem = Funktsioon.Valem;
        olem.Liik = Funktsioon.Liik;
        olem.Tuletis = Funktsioon.Tuletis;
        olem.TeineTuletis = Funktsioon.TeineTuletis;
        olem.Maaramispiirkond = Maaramispiirkond;
        olem.Muutumispiirkond = Muutumispiirkond;
        olem.Nullkohad = Nullkohad;
        olem.Positiivsuspiirkond = Positiivsuspiirkond;
        olem.Negatiivsuspiirkond = Negatiivsuspiirkond;
        olem.Paarsus = Paarsus;
        olem.Perioodilisus = Perioodilisus;
        olem.YTeljeLoikepunkt = YTeljeLoikepunkt;
        olem.Asumptoodid = Asumptoodid;
        olem.KriitilisedPunktid = KriitilisedPunktid;
        olem.Kasvamisvahemikud = Kasvamisvahemikud;
        olem.Kahanemisvahemikud = Kahanemisvahemikud;
        olem.Ekstreemumid = Ekstreemumid;
        olem.Kumerusvahemikud = Kumerusvahemikud;
        olem.Nogususvahemikud = Nogususvahemikud;
        olem.Kaanupunktid = Kaanupunktid;
    }
}

public record PuutujaTulemus(double X0, double Y0, double K, double TousunurkKraadides, string Puutuja, string Normaal,
    double? NormaaliK, double? NormaaliB);

public record LoiguTulemus(Punkt Suurim, Punkt Vahim);

/// <summary>
/// Parsib tekstilise valemi (MathNet.Symbolics), leiab sümbolselt esimese ja teise tuletise ning uurib funktsiooni
/// numbriliselt vahemikus [-50; 50] sammuga 0.01 (Teema 8 ja Teema 11 küsimused).
/// </summary>
public partial class FunktsiooniAnalyysija
{
    public const int VahemikuPiir = 50;
    private const int Samme = VahemikuPiir * 200;   // x = -50 ... 50, samm 0.01
    private const int MaxNaidatud = 10;
    private const int MaxVahemikke = 6;

    private static readonly HashSet<string> LubatudNimed =
        ["x", "sin", "cos", "tan", "exp", "ln", "sqrt", "pi", "e"];

    [GeneratedRegex("[a-z]+")]
    private static partial Regex NimeMuster();

    [GeneratedRegex(@"\s+")]
    private static partial Regex TuhikuMuster();

    // Unaarne miinus valemi alguses, sulu või tehtemärgi järel – MathNet loeks -x^2 kui (-x)^2 = x^2.
    [GeneratedRegex(@"(?<=^\s*|[(+\-*]\s*)-")]
    private static partial Regex UnaarneMiinus();

    // x_i arvutatakse jagamisega, et 0, 0.5, 1 jne oleksid täpselt võrgus.
    private static double X(int i) => (i - Samme / 2) / 100.0;

    public KoostatudFunktsioon Koosta(string sisend)
    {
        var valem = TuhikuMuster().Replace((sisend ?? string.Empty).Trim().ToLowerInvariant().Replace("**", "^"), " ");
        if (valem.Length == 0)
            throw new ValemiViga("Valem on tühi.");
        if (valem.Contains(','))
            throw new ValemiViga("Kasuta kümnendkohtade jaoks punkti, mitte koma (nt 0.5*x).");

        foreach (Match nimi in NimeMuster().Matches(valem))
        {
            if (!LubatudNimed.Contains(nimi.Value))
                throw new ValemiViga($"Tundmatu nimi \"{nimi.Value}\". Muutuja peab olema x; lubatud funktsioonid: sin, cos, tan, exp, ln, sqrt; konstandid: pi, e.");
        }

        SymbolicExpression avaldis;
        try
        {
            avaldis = SymbolicExpression.Parse(UnaarneMiinus().Replace(valem, "(-1)*"));
        }
        catch (Exception)
        {
            throw new ValemiViga("Valemit ei õnnestunud lugeda. Kontrolli sulge ja tehtemärke (nt 3*x, mitte 3x).");
        }

        SymbolicExpression tuletis, teineTuletis;
        try
        {
            var x = SymbolicExpression.Variable("x");
            tuletis = avaldis.Differentiate(x);
            teineTuletis = tuletis.Differentiate(x);
        }
        catch (Exception)
        {
            throw new ValemiViga("Selle funktsiooni tuletist ei õnnestunud leida.");
        }

        return new KoostatudFunktsioon(valem, IlusKuju(tuletis), IlusKuju(teineTuletis),
            Kompileeri(avaldis), Kompileeri(tuletis), Kompileeri(teineTuletis),
            FunktsiooniLiigitaja.Liigita(avaldis), avaldis);
    }

    // Tuletise kuju nagu koolis: polünoom kahanevate astmete järgi (3*x^2 - 6*x - 9), murdude korral lihtsustatud kuju.
    private static string IlusKuju(SymbolicExpression avaldis)
    {
        var x = SymbolicExpression.Variable("x");
        var tekst = avaldis.ToString();
        try
        {
            if (avaldis.IsPolynomial(x))
                return PolynoomiTekst(avaldis.Coefficients(x)) ?? tekst;
            var lihtne = avaldis.RationalSimplify(x).ToString();
            return lihtne.Length < tekst.Length ? lihtne : tekst;
        }
        catch (Exception)
        {
            return tekst;
        }
    }

    private static string? PolynoomiTekst(SymbolicExpression[] kordajad)
    {
        var liikmed = new List<(bool Negatiivne, string Tekst)>();
        for (int aste = kordajad.Length - 1; aste >= 0; aste--)
        {
            var kordaja = kordajad[aste].ToString();
            if (kordaja == "0")
                continue;
            if (kordaja.Contains(' ') || kordaja.Contains('+') || kordaja.LastIndexOf('-') > 0)
                return null;   // mittearvuline kordaja – jätame MathNeti kuju
            bool negatiivne = kordaja.StartsWith('-');
            var abs = negatiivne ? kordaja[1..] : kordaja;
            var astmeosa = aste switch { 0 => "", 1 => "x", _ => $"x^{aste}" };
            liikmed.Add((negatiivne, astmeosa.Length == 0 ? abs : abs == "1" ? astmeosa : $"{abs}*{astmeosa}"));
        }
        if (liikmed.Count == 0)
            return "0";
        return string.Concat(liikmed.Select((l, i) => i == 0
            ? (l.Negatiivne ? "-" : "") + l.Tekst
            : (l.Negatiivne ? " - " : " + ") + l.Tekst));
    }

    public AnalyysiTulemus Analyysi(string sisend)
    {
        var funktsioon = Koosta(sisend);
        var f = funktsioon.F;
        var df = funktsioon.Tuletisfunktsioon;
        var d2f = funktsioon.TeineTuletisfunktsioon;

        var fv = new double[Samme + 1];
        var dfv = new double[Samme + 1];
        var d2fv = new double[Samme + 1];
        var maaratud = new bool[Samme + 1];
        var dfKasutatav = new bool[Samme + 1];
        var d2fKasutatav = new bool[Samme + 1];
        for (int i = 0; i <= Samme; i++)
        {
            fv[i] = f(X(i));
            dfv[i] = df(X(i));
            d2fv[i] = d2f(X(i));
            maaratud[i] = double.IsFinite(fv[i]);
            dfKasutatav[i] = maaratud[i] && double.IsFinite(dfv[i]);
            d2fKasutatav[i] = maaratud[i] && double.IsFinite(d2fv[i]);
        }

        if (!maaratud.Contains(true))
            throw new ValemiViga($"Funktsioon ei ole vahemikus [-{VahemikuPiir}; {VahemikuPiir}] üheski punktis määratud.");

        // Määramispiirkond X
        var poolused = LeiaPoolused(f, fv, maaratud);
        var piirkond = LeiaMaaramispiirkond(f, maaratud, poolused);

        // Nullkohad: f(x) = 0
        var (nullkohad, fOnNull) = LeiaNullkohad(f, fv, maaratud);

        // Kriitilised punktid: f'(x) = 0 (ainult seal, kus f on määratud)
        var (kriitilised, dfOnNull) = LeiaNullkohad(df, dfv, dfKasutatav);
        kriitilised = kriitilised.Where(x => double.IsFinite(f(x))).ToList();

        // Teise tuletise nullkohad: käänupunktide kandidaadid
        var (d2Nullid, d2OnNull) = LeiaNullkohad(d2f, d2fv, d2fKasutatav);
        d2Nullid = d2Nullid.Where(x => double.IsFinite(f(x))).ToList();

        const double h = 1e-4;

        // Ekstreemumid: tuletise märk muutub kriitilises punktis
        var ekstreemumid = new List<Ekstreemum>();
        if (!dfOnNull)
        {
            foreach (var x in kriitilised)
            {
                double vasak = df(x - h), parem = df(x + h);
                if (!double.IsFinite(vasak) || !double.IsFinite(parem))
                    continue;
                if (vasak > 0 && parem < 0)
                    ekstreemumid.Add(new Ekstreemum(x, Ilusta(f(x)), OnMaksimum: true));
                else if (vasak < 0 && parem > 0)
                    ekstreemumid.Add(new Ekstreemum(x, Ilusta(f(x)), OnMaksimum: false));
            }
        }

        // Käänupunktid: teise tuletise märk muutub
        var kaanupunktid = new List<Punkt>();
        if (!d2OnNull)
        {
            foreach (var x in d2Nullid)
            {
                double vasak = d2f(x - h), parem = d2f(x + h);
                if (double.IsFinite(vasak) && double.IsFinite(parem) && vasak != 0 && parem != 0 && Math.Sign(vasak) != Math.Sign(parem))
                    kaanupunktid.Add(new Punkt(x, Ilusta(f(x))));
            }
        }

        // Märgipiirkonnad
        var positiivne = fOnNull ? [] : MargiVahemikud(f, piirkond, nullkohad, +1, kinnisedOtsad: true, yhenda: false);
        var negatiivne = fOnNull ? [] : MargiVahemikud(f, piirkond, nullkohad, -1, kinnisedOtsad: true, yhenda: false);
        var kasvab = dfOnNull ? [] : MargiVahemikud(df, piirkond, kriitilised, +1, kinnisedOtsad: false, yhenda: true);
        var kahaneb = dfOnNull ? [] : MargiVahemikud(df, piirkond, kriitilised, -1, kinnisedOtsad: false, yhenda: true);
        var nogus = d2OnNull ? [] : MargiVahemikud(d2f, piirkond, d2Nullid, +1, kinnisedOtsad: false, yhenda: true);
        var kumer = d2OnNull ? [] : MargiVahemikud(d2f, piirkond, d2Nullid, -1, kinnisedOtsad: false, yhenda: true);

        var periood = LeiaPeriood(f, fv, dfOnNull);
        var asumptoodid = LeiaAsumptoodid(f, piirkond);
        var (xMin, xMax) = SoovitatudVahemik(piirkond, nullkohad, ekstreemumid, kaanupunktid, asumptoodid, periood, f);

        var muutumispiirkond = dfOnNull ? [] : LeiaMuutumispiirkond(f, fv, maaratud, piirkond, ekstreemumid);

        string Konstantne(string tekst) => dfOnNull ? "∅ (konstantne funktsioon)" : tekst;
        string Sirge(string tekst) => d2OnNull ? "∅ (f''(x) = 0, graafik on sirge)" : tekst;

        return new AnalyysiTulemus
        {
            Funktsioon = funktsioon,
            Maaramispiirkond = HulgaTekst(piirkond),
            Muutumispiirkond = dfOnNull ? "{" + Vorminda(fv[Array.IndexOf(maaratud, true)]) + "}" : HulgaTekst(muutumispiirkond),
            Nullkohad = fOnNull ? "kõik x määramispiirkonnas" : KohtadeTekst(nullkohad),
            Positiivsuspiirkond = HulgaTekst(positiivne),
            Negatiivsuspiirkond = HulgaTekst(negatiivne),
            Paarsus = PaarsuseTekst(f, fv, maaratud),
            Perioodilisus = PerioodiTekst(periood, dfOnNull),
            YTeljeLoikepunkt = double.IsFinite(f(0)) ? $"(0; {Vorminda(f(0))})" : "puudub (0 ∉ X)",
            Asumptoodid = AsumptootideTekst(asumptoodid),
            KriitilisedPunktid = dfOnNull ? "f'(x) = 0 kõikjal (konstantne funktsioon)" : KohtadeTekst(kriitilised),
            Kasvamisvahemikud = Konstantne(HulgaTekst(kasvab, punktideKuju: false)),
            Kahanemisvahemikud = Konstantne(HulgaTekst(kahaneb, punktideKuju: false)),
            Ekstreemumid = EkstreemumideTekst(ekstreemumid),
            Kumerusvahemikud = Sirge(HulgaTekst(kumer, punktideKuju: false)),
            Nogususvahemikud = Sirge(HulgaTekst(nogus, punktideKuju: false)),
            Kaanupunktid = PunktideTekst(kaanupunktid),
            NullkohtadeX = fOnNull ? [] : nullkohad,
            EkstreemumidePunktid = ekstreemumid,
            KaanupunktidePunktid = kaanupunktid,
            AsumptootideAndmed = asumptoodid,
            SoovitatudXMin = xMin,
            SoovitatudXMax = xMax,
            Andmed = new Vaheandmed(piirkond, muutumispiirkond, fOnNull ? [] : nullkohad, fOnNull, kriitilised, dfOnNull, d2Nullid, d2OnNull, periood),
        };
    }

    // ---------- Teema 10: puutuja ja normaal ----------

    public PuutujaTulemus Puutuja(string sisend, double x0)
    {
        var funktsioon = Koosta(sisend);
        double y0 = funktsioon.F(x0);
        if (!double.IsFinite(y0))
            throw new ValemiViga($"Funktsioon ei ole kohal x0 = {Vorminda(x0)} määratud.");
        double k = funktsioon.Tuletisfunktsioon(x0);
        if (!double.IsFinite(k))
            throw new ValemiViga($"Funktsioonil puudub kohal x0 = {Vorminda(x0)} tuletis, puutujat ei saa leida.");

        y0 = Ilusta(y0);
        k = Ilusta(k);
        double nurk = Math.Atan(k) * 180 / Math.PI;
        if (nurk < 0)
            nurk += 180;

        string normaal;
        double? normaaliK = null, normaaliB = null;
        if (k == 0)
        {
            normaal = $"x = {Vorminda(x0)}";
        }
        else
        {
            normaaliK = Ilusta(-1 / k);
            normaaliB = Ilusta(y0 - normaaliK.Value * x0);
            normaal = SirgeTekst(normaaliK.Value, normaaliB.Value);
        }

        return new PuutujaTulemus(x0, y0, k, Math.Round(nurk, 2), SirgeTekst(k, Ilusta(y0 - k * x0)), normaal, normaaliK, normaaliB);
    }

    // ---------- Teema 11 ül 10: suurim ja vähim väärtus lõigul [a; b] ----------

    public LoiguTulemus SuurimJaVahim(string sisend, double a, double b)
    {
        if (!(a < b))
            throw new ValemiViga("Lõigu algus a peab olema väiksem kui lõpp b.");
        var f = Koosta(sisend).F;

        const int n = 20000;
        int iMax = 0, iMin = 0;
        var v = new double[n + 1];
        for (int i = 0; i <= n; i++)
        {
            double x = a + (b - a) * i / n;
            v[i] = f(x);
            if (!double.IsFinite(v[i]))
                throw new ValemiViga($"Funktsioon ei ole kogu lõigul määratud (nt x = {Vorminda(x)}).");
            if (v[i] > v[iMax]) iMax = i;
            if (v[i] < v[iMin]) iMin = i;
        }

        Punkt Tapsusta(int i, int marki)
        {
            double x = a + (b - a) * i / n;
            if (i > 0 && i < n)
                x = KuldloikeMiinimum(t => -marki * f(t), a + (b - a) * (i - 1) / n, a + (b - a) * (i + 1) / n);
            x = Ilusta(x, 1e-6);
            return new Punkt(x, Ilusta(f(x)));
        }

        return new LoiguTulemus(Tapsusta(iMax, +1), Tapsusta(iMin, -1));
    }

    // ---------- Kompileerimine ----------

    private static Func<double, double> Kompileeri(SymbolicExpression avaldis)
    {
        try
        {
            var kiire = avaldis.Compile("x");
            return x =>
            {
                try { return kiire(x); }
                catch (Exception) { return double.NaN; }
            };
        }
        catch (Exception)
        {
            // Varuvariant: aeglasem, kuid toetab kõiki avaldisi.
            return x =>
            {
                try
                {
                    var tulemus = avaldis.Evaluate(new Dictionary<string, FloatingPoint> { ["x"] = FloatingPoint.NewReal(x) });
                    return tulemus.IsReal ? tulemus.RealValue : double.NaN;
                }
                catch (Exception) { return double.NaN; }
            };
        }
    }

    // ---------- Nullkohtade otsimine ----------

    // Leiab g nullkohad: täpsed nullid võrgus, märgimuutused (poolitusmeetod) ja puutepunktid (|g| lokaalne miinimum).
    private static (List<double> Kohad, bool IdentneNull) LeiaNullkohad(Func<double, double> g, double[] gv, bool[] kasutatav)
    {
        int kasutatavaid = kasutatav.Count(k => k);
        int nulle = Enumerable.Range(0, Samme + 1).Count(i => kasutatav[i] && gv[i] == 0);
        if (kasutatavaid > 0 && nulle > kasutatavaid / 2)
            return ([], true);

        // Alavool (nt e^(-x^2) väga suurte |x| korral) loetakse nulliks, et seda ei peetaks nullkohaks.
        double V(int j) => Math.Abs(gv[j]) < 1e-290 ? 0 : gv[j];

        var kohad = new List<double>();
        for (int i = 0; i <= Samme; i++)
        {
            if (!kasutatav[i])
                continue;

            if (V(i) == 0)
            {
                // Mitu järjestikust täpset nulli on alavool (nt e^(-x^2) suurte |x| korral), mitte nullkohad.
                bool jada = (i > 0 && kasutatav[i - 1] && V(i - 1) == 0) || (i < Samme && kasutatav[i + 1] && V(i + 1) == 0);
                if (!jada)
                    kohad.Add(X(i));
                continue;
            }

            if (i < Samme && kasutatav[i + 1] && V(i + 1) != 0 && Math.Sign(V(i)) != Math.Sign(V(i + 1)))
            {
                double r = Poolitus(g, X(i), X(i + 1));
                double gr = Math.Abs(g(r));
                // Pideva funktsiooni korral on |g(r)| väiksem kui otspunktides; poolusel on see suur.
                if (gr < 1e-9 || gr < Math.Min(Math.Abs(V(i)), Math.Abs(V(i + 1))))
                    kohad.Add(r);
            }

            if (i > 0 && i < Samme && kasutatav[i - 1] && kasutatav[i + 1]
                && Math.Abs(V(i)) < Math.Abs(V(i - 1)) && Math.Abs(V(i)) <= Math.Abs(V(i + 1))
                && Math.Sign(V(i - 1)) == Math.Sign(V(i)) && Math.Sign(V(i + 1)) == Math.Sign(V(i)))
            {
                double m = KuldloikeMiinimum(t => Math.Abs(g(t)), X(i - 1), X(i + 1));
                if (Math.Abs(g(m)) < 1e-9)
                    kohad.Add(m);
            }
        }

        return (Yhenda(kohad), false);
    }

    // Poolused, mis ei satu täpselt võrgupunkti (nt 1/(x - 1/3) või 1/(x - 1/3)^2).
    private static List<double> LeiaPoolused(Func<double, double> f, double[] fv, bool[] maaratud)
    {
        var poolused = new List<double>();
        for (int i = 0; i < Samme; i++)
        {
            if (!maaratud[i] || !maaratud[i + 1])
                continue;

            double a = fv[i], b = fv[i + 1];
            if (a != 0 && b != 0 && Math.Sign(a) != Math.Sign(b))
            {
                double r = Poolitus(f, X(i), X(i + 1));
                double fr = Math.Abs(f(r));
                if (!double.IsFinite(fr) || fr > 1e4 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b))))
                    poolused.Add(r);
            }

            if (i > 0 && maaratud[i - 1]
                && Math.Abs(a) > Math.Abs(fv[i - 1]) && Math.Abs(a) >= Math.Abs(b)
                && Math.Sign(fv[i - 1]) == Math.Sign(a) && Math.Sign(b) == Math.Sign(a))
            {
                double p = KuldloikeMiinimum(t => -Math.Abs(f(t)), X(i - 1), X(i + 1));
                double fp = Math.Abs(f(p));
                if (!double.IsFinite(fp) || fp > 1e4 * Math.Max(1, Math.Abs(a)))
                    poolused.Add(p);
            }
        }
        return Yhenda(poolused);
    }

    private static double Poolitus(Func<double, double> g, double a, double b)
    {
        double ga = g(a);
        for (int k = 0; k < 200; k++)
        {
            double k2 = (a + b) / 2;
            if (k2 <= a || k2 >= b)
                break;
            double gk = g(k2);
            if (gk == 0 || double.IsNaN(gk))
                return k2;
            if (Math.Sign(gk) == Math.Sign(ga)) { a = k2; ga = gk; }
            else b = k2;
        }
        return (a + b) / 2;
    }

    private static double KuldloikeMiinimum(Func<double, double> h, double a, double b)
    {
        double r = (Math.Sqrt(5) - 1) / 2;
        double c = b - r * (b - a), d = a + r * (b - a);
        double hc = h(c), hd = h(d);
        for (int k = 0; k < 120; k++)
        {
            if (hc < hd) { b = d; d = c; hd = hc; c = b - r * (b - a); hc = h(c); }
            else { a = c; c = d; hc = hd; d = a + r * (b - a); hd = h(d); }
        }
        return (a + b) / 2;
    }

    // Asendab arvu lähima lihtsa murruga (nimetaja ≤ 100), kui see on väga lähedal – nt 0.99999999 → 1.
    private static double Ilusta(double v, double tapsus = 1e-9)
    {
        if (!double.IsFinite(v))
            return v;
        for (int q = 1; q <= 100; q++)
        {
            double p = Math.Round(v * q);
            if (Math.Abs(v - p / q) < tapsus)
                return p / q;
        }
        return v;
    }

    private static List<double> Yhenda(IEnumerable<double> kohad)
    {
        var tulemus = new List<double>();
        foreach (var x in kohad.Select(x => Ilusta(x)).Order())
        {
            if (tulemus.Count == 0 || x - tulemus[^1] > 1e-6)
                tulemus.Add(x);
        }
        return tulemus;
    }

    // ---------- Hulgad ja vahemikud ----------

    internal record Vaheandmed(List<Intervall> Piirkond, List<Intervall> Muutumispiirkond, List<double> Nullkohad, bool FOnNull, List<double> Kriitilised,
        bool DfOnNull, List<double> D2Nullid, bool D2OnNull, (double T, string Tekst)? Periood);

    internal record struct Intervall(double Vasak, bool VasakKinnine, double Parem, bool ParemKinnine);

    private static List<Intervall> LeiaMaaramispiirkond(Func<double, double> f, bool[] maaratud, List<double> poolused)
    {
        var intervallid = new List<Intervall>();
        int i = 0;
        while (i <= Samme)
        {
            if (!maaratud[i]) { i++; continue; }
            int algus = i;
            while (i < Samme && maaratud[i + 1]) i++;
            int lopp = i;
            i++;

            var (vasak, vasakKinnine) = algus == 0
                ? (double.NegativeInfinity, false)
                : LeiaPiir(f, X(algus - 1), X(algus));
            var (parem, paremKinnine) = lopp == Samme
                ? (double.PositiveInfinity, false)
                : LeiaPiir(f, X(lopp + 1), X(lopp));
            intervallid.Add(new Intervall(vasak, vasakKinnine, parem, paremKinnine));
        }

        // Poolused lõhuvad intervalli kaheks.
        foreach (var poolus in poolused)
        {
            int k = intervallid.FindIndex(iv => iv.Vasak < poolus && poolus < iv.Parem);
            if (k < 0) continue;
            var iv = intervallid[k];
            intervallid[k] = iv with { Parem = poolus, ParemKinnine = false };
            intervallid.Insert(k + 1, iv with { Vasak = poolus, VasakKinnine = false });
        }
        return intervallid;
    }

    // Leiab määramispiirkonna otspunkti poolitusmeetodiga ning otsustab, kas see kuulub piirkonda.
    private static (double Piir, bool Kinnine) LeiaPiir(Func<double, double> f, double maaramata, double maaratud)
    {
        for (int k = 0; k < 200; k++)
        {
            double k2 = (maaramata + maaratud) / 2;
            if (k2 == maaramata || k2 == maaratud)
                break;
            if (double.IsFinite(f(k2))) maaratud = k2;
            else maaramata = k2;
        }

        double ilus = Ilusta(maaratud);
        if (ilus != maaratud)
            return (ilus, double.IsFinite(f(ilus)));

        // Irratsionaalne piir: kui f läheneb piirile lõplikule väärtusele, on otspunkt kinnine (nt sqrt), muidu lahtine (nt ln).
        double sisse = maaratud + Math.Sign(maaratud - maaramata) * 1e-6;
        bool kinnine = Math.Abs(f(maaratud) - f(sisse)) < 0.5;
        return (maaratud, kinnine);
    }

    // Vahemikud, kus g märk on `mark` (intervallmeetod): määramispiirkonna osad jagatakse lõikepunktidega
    // ja märk määratakse iga osa sisepunktis. `yhenda` liidab kõrvuti olevad sama märgiga osad (nt x^3 kasvab kogu ℝ-is).
    private static List<Intervall> MargiVahemikud(Func<double, double> g, List<Intervall> piirkond, List<double> loikepunktid,
        int mark, bool kinnisedOtsad, bool yhenda)
    {
        var tulemus = new List<Intervall>();
        foreach (var iv in piirkond)
        {
            var piirid = new List<double> { iv.Vasak };
            piirid.AddRange(loikepunktid.Where(c => c > iv.Vasak && c < iv.Parem));
            piirid.Add(iv.Parem);

            Intervall? jooksev = null;
            for (int k = 0; k + 1 < piirid.Count; k++)
            {
                double a = piirid[k], b = piirid[k + 1];
                double sees = (double.IsNegativeInfinity(a), double.IsPositiveInfinity(b)) switch
                {
                    (true, true) => 0,
                    (true, false) => b - 1,
                    (false, true) => a + 1,
                    _ => (a + b) / 2,
                };
                bool sobib = Math.Sign(g(sees)) == mark;

                if (!sobib)
                {
                    if (jooksev is not null) tulemus.Add(jooksev.Value);
                    jooksev = null;
                    continue;
                }

                bool vasakKinnine = kinnisedOtsad && k == 0 && iv.VasakKinnine && Math.Sign(g(a)) == mark;
                bool paremKinnine = kinnisedOtsad && k + 2 == piirid.Count && iv.ParemKinnine && Math.Sign(g(b)) == mark;

                if (jooksev is not null && yhenda)
                {
                    jooksev = jooksev.Value with { Parem = b, ParemKinnine = paremKinnine };
                }
                else
                {
                    if (jooksev is not null) tulemus.Add(jooksev.Value);
                    jooksev = new Intervall(a, vasakKinnine, b, paremKinnine);
                }
            }
            if (jooksev is not null) tulemus.Add(jooksev.Value);
        }
        return tulemus;
    }

    // Muutumispiirkond Y: iga määramispiirkonna osa kohta leitakse väikseim ja suurim väärtus (võrgupunktid, ekstreemumid,
    // kinnised otspunktid) ning piirväärtused lahtistes otstes ja lõpmatuses. Osade tulemused ühendatakse.
    private static List<Intervall> LeiaMuutumispiirkond(Func<double, double> f, double[] fv, bool[] maaratud,
        List<Intervall> piirkond, List<Ekstreemum> ekstreemumid)
    {
        var osad = new List<Intervall>();
        foreach (var iv in piirkond)
        {
            var vorgus = new List<double>();    // võrgupunktide väärtused
            var tapsed = new List<double>();    // ekstreemumid ja kinnised otspunktid
            var piirid = new List<double>();    // piirväärtused, mida ei pruugita saavutada

            for (int i = 0; i <= Samme; i++)
            {
                if (maaratud[i] && X(i) > iv.Vasak && X(i) < iv.Parem)
                    vorgus.Add(fv[i]);
            }
            tapsed.AddRange(ekstreemumid.Where(e => e.X > iv.Vasak && e.X < iv.Parem).Select(e => e.Y));

            void LisaOts(double ots, bool kinnine, int suund)
            {
                if (double.IsInfinity(ots))
                {
                    var p = PiirLopmatuses(f, Math.Sign(ots));
                    if (p is double v) piirid.Add(v);
                }
                else if (kinnine)
                {
                    tapsed.Add(f(ots));
                }
                else if (YhepoolnePiir(f, ots, suund) is double v)
                {
                    piirid.Add(v);
                }
            }
            LisaOts(iv.Vasak, iv.VasakKinnine, +1);
            LisaOts(iv.Parem, iv.ParemKinnine, -1);

            if (vorgus.Count == 0 && tapsed.Count == 0 && piirid.Count == 0)
                continue;

            var (alumine, alumineKinnine) = Aar(vorgus, tapsed, piirid, -1);
            var (ulemine, ulemineKinnine) = Aar(vorgus, tapsed, piirid, +1);
            osad.Add(new Intervall(alumine, alumineKinnine, ulemine, ulemineKinnine));
        }

        // Ühendame kattuvad või kokku puutuvad osad.
        var tulemus = new List<Intervall>();
        foreach (var iv in osad.OrderBy(o => o.Vasak))
        {
            if (tulemus.Count > 0)
            {
                var viimane = tulemus[^1];
                double tol = 1e-7 * (1 + Math.Abs(iv.Vasak));
                bool kattub = iv.Vasak < viimane.Parem - tol
                    || (Math.Abs(iv.Vasak - viimane.Parem) <= tol && (viimane.ParemKinnine || iv.VasakKinnine))
                    || (double.IsInfinity(iv.Vasak) && iv.Vasak == viimane.Vasak);
                if (kattub)
                {
                    if (iv.Parem > viimane.Parem || (iv.Parem == viimane.Parem && iv.ParemKinnine))
                        tulemus[^1] = viimane with { Parem = iv.Parem, ParemKinnine = iv.ParemKinnine };
                    continue;
                }
            }
            tulemus.Add(iv);
        }
        return tulemus;
    }

    // Väikseim (suund = -1) või suurim (+1) väärtus; otspunkt on kinnine, kui see saavutatakse
    // ega ole ainult piirväärtus (nt e^x → 0, kui x → -∞, kuid 0 ei saavutata).
    private static (double Vaartus, bool Kinnine) Aar(List<double> vorgus, List<double> tapsed, List<double> piirid, int suund)
    {
        double Vali(IEnumerable<double> v) => suund > 0 ? v.DefaultIfEmpty(double.NegativeInfinity).Max() : v.DefaultIfEmpty(double.PositiveInfinity).Min();
        double saavutatud = Vali(vorgus.Concat(tapsed)), piir = Vali(piirid);
        if (piirid.Count == 0)
            return (saavutatud, true);
        double tol = double.IsFinite(piir) ? 1e-7 * (1 + Math.Abs(piir)) : 0;
        bool piirOnAar = suund > 0 ? piir >= saavutatud - tol : piir <= saavutatud + tol;
        if (!piirOnAar)
            return (saavutatud, true);
        // Võrgupunkt võib piirväärtusele vaid läheneda; kinnine on otspunkt ainult siis,
        // kui ekstreemum või kinnine otspunkt selle väärtuse tõesti saavutab.
        return (piir, double.IsFinite(piir) && Math.Abs(Vali(tapsed) - piir) <= tol);
    }

    // ---------- Piirväärtused ----------

    // lim g(x), kui x → ±∞ (suund = ±1). Tagastab null, kui piirväärtust pole (nt sin x).
    private static double? PiirLopmatuses(Func<double, double> g, int suund, int maxAste = 300, double tol = 1e-6)
    {
        for (int k = maxAste; k >= 2; k--)
        {
            double v2 = g(suund * Math.Pow(10, k));
            if (double.IsNaN(v2))
                continue;
            if (double.IsInfinity(v2))
                return v2;
            return Hinda(g(suund * Math.Pow(10, k / 2.0)), v2, tol);
        }
        return null;
    }

    // lim g(x), kui x → a paremalt (suund = +1) või vasakult (suund = -1).
    private static double? YhepoolnePiir(Func<double, double> g, double a, int suund)
    {
        double skaala = Math.Max(1, Math.Abs(a));
        int maxAste = a == 0 ? 300 : 10;
        for (int k = maxAste; k >= 2; k--)
        {
            double v2 = g(a + suund * skaala * Math.Pow(10, -k));
            if (double.IsNaN(v2))
                continue;
            if (double.IsInfinity(v2))
                return v2;
            return Hinda(g(a + suund * skaala * Math.Pow(10, -k / 2.0)), v2, 1e-4);
        }
        return null;
    }

    // v1 on kaugemal, v2 lähemal piirile: koondub, hajub lõpmatusse või piirväärtust pole.
    private static double? Hinda(double v1, double v2, double tol)
    {
        if (!double.IsFinite(v1))
            return null;
        if (Math.Abs(v2 - v1) <= tol * (1 + Math.Abs(v2)))
            return Ilusta(v2, 1e-7);
        if (Math.Abs(v2) > 10 && Math.Sign(v1) == Math.Sign(v2) && Math.Abs(v2) >= 1.5 * Math.Abs(v1))
            return Math.Sign(v2) * double.PositiveInfinity;
        return null;
    }

    // ---------- Asümptoodid ----------

    private static List<Asumptoot> LeiaAsumptoodid(Func<double, double> f, List<Intervall> piirkond)
    {
        var tulemus = new List<Asumptoot>();

        // Vertikaalsed: lahtine lõplik otspunkt, kus f → ±∞.
        var vertikaalsed = new List<double>();
        foreach (var iv in piirkond)
        {
            if (double.IsFinite(iv.Vasak) && !iv.VasakKinnine && YhepoolnePiir(f, iv.Vasak, +1) is double v1 && double.IsInfinity(v1))
                vertikaalsed.Add(iv.Vasak);
            if (double.IsFinite(iv.Parem) && !iv.ParemKinnine && YhepoolnePiir(f, iv.Parem, -1) is double v2 && double.IsInfinity(v2))
                vertikaalsed.Add(iv.Parem);
        }
        tulemus.AddRange(Yhenda(vertikaalsed).Select(x => new Asumptoot(true, x, 0, 0)));

        // Horisontaalsed (y = L) ja kaldasümptoodid (y = kx + b), kui x → ±∞.
        foreach (var suund in new[] { -1, +1 })
        {
            bool ulatubLopmatusse = suund < 0
                ? piirkond.Count > 0 && double.IsNegativeInfinity(piirkond[0].Vasak)
                : piirkond.Count > 0 && double.IsPositiveInfinity(piirkond[^1].Parem);
            if (!ulatubLopmatusse)
                continue;

            Asumptoot? leitud = null;
            var piir = PiirLopmatuses(f, suund);
            if (piir is double l && double.IsFinite(l))
            {
                leitud = new Asumptoot(false, 0, 0, l);
            }
            else if (piir is double lop && double.IsInfinity(lop)
                && PiirLopmatuses(x => f(x) / x, suund, maxAste: 8) is double k && double.IsFinite(k) && k != 0)
            {
                k = Ilusta(k, 1e-6);
                if (PiirLopmatuses(x => f(x) - k * x, suund, maxAste: 8, tol: 1e-3) is double b && double.IsFinite(b))
                    leitud = new Asumptoot(false, 0, k, Ilusta(Math.Round(b, 6), 1e-5));
            }

            if (leitud is not null && !tulemus.Contains(leitud))
                tulemus.Add(leitud);
        }
        return tulemus;
    }

    // ---------- Perioodilisus ----------

    // Kontrollib kandidaate T = kπ/m ja täisarvud 1…20; tagastab vähima sobiva perioodi.
    private static (double T, string Tekst)? LeiaPeriood(Func<double, double> f, double[] fv, bool konstantne)
    {
        if (konstantne)
            return null;

        var kandidaadid = new List<(double T, string Tekst)>();
        foreach (var m in new[] { 1, 2, 3, 4, 6 })
        {
            for (int k = 1; k <= 4 * m; k++)
            {
                if (Syt(k, m) != 1) continue;
                string lugeja = k == 1 ? "π" : $"{k}π";
                kandidaadid.Add((k * Math.PI / m, m == 1 ? lugeja : $"{lugeja}/{m}"));
            }
        }
        for (int n = 1; n <= 20; n++)
            kandidaadid.Add((n, n.ToString(CultureInfo.InvariantCulture)));

        foreach (var (t, tekst) in kandidaadid.OrderBy(c => c.T))
        {
            int vorreldud = 0;
            bool sobib = true;
            for (int i = 0; i <= Samme && sobib; i += 7)
            {
                double x = X(i);
                if (x + t > VahemikuPiir)
                    break;
                double a = fv[i], b = f(x + t);
                bool aSuur = !double.IsFinite(a) || Math.Abs(a) > 1e6, bSuur = !double.IsFinite(b) || Math.Abs(b) > 1e6;
                if (aSuur || bSuur)
                {
                    // Poolusel võib üks pool ümardamise tõttu olla lõplik ja väga suur.
                    sobib = aSuur == bSuur;
                    continue;
                }
                sobib = Math.Abs(a - b) <= 1e-7 * (1 + Math.Abs(a));
                vorreldud++;
            }
            if (sobib && vorreldud >= 100)
                return (t, tekst);
        }
        return null;
    }

    private static int Syt(int a, int b) => b == 0 ? a : Syt(b, a % b);

    // ---------- Graafiku vahemik ----------

    // Vaikimisi [-5; 5]; laiendatakse, et nullkohad, ekstreemumid, käänupunktid ja asümptoodid jääksid nähtavale.
    private static (double Min, double Max) SoovitatudVahemik(List<Intervall> piirkond, List<double> nullkohad,
        List<Ekstreemum> ekstreemumid, List<Punkt> kaanupunktid, List<Asumptoot> asumptoodid, (double T, string)? periood,
        Func<double, double> f)
    {
        if (periood is { } p)
        {
            double pool = Math.Min(VahemikuPiir, Math.Max(5, Math.Ceiling(2 * p.T)));
            return (-pool, pool);
        }

        var kohad = new List<double>();
        kohad.AddRange(nullkohad);
        kohad.AddRange(ekstreemumid.Select(e => e.X));
        kohad.AddRange(kaanupunktid.Select(p => p.X));
        kohad.AddRange(asumptoodid.Where(a => a.OnVertikaalne).Select(a => a.X));
        kohad.AddRange(piirkond.SelectMany(iv => new[] { iv.Vasak, iv.Parem }).Where(double.IsFinite));
        if (kohad.Count == 0 || kohad.Count > 20)
            return (-5, 5);

        // Aken olulise osa ümber (nt x^4 - 4*x^3 → [-1; 5]); laiem aken muudaks y-telje nii suureks, et kõver on lame.
        double min = kohad.Min(), max = kohad.Max();
        double varu = Math.Max(1, (max - min) * 0.25);
        double vasak = Math.Floor(min - varu), parem = Math.Ceiling(max + varu);
        const double MinLaius = 6;
        if (parem - vasak < MinLaius)
        {
            double lisa = (MinLaius - (parem - vasak)) / 2;
            vasak = Math.Floor(vasak - lisa);
            parem = Math.Ceiling(parem + lisa);
        }

        // Ühelt poolt tõkestatud määramispiirkonna korral (nt sqrt(x)) nihutatakse aken määramispiirkonna poole.
        double xAlgus = piirkond.Count > 0 ? piirkond[0].Vasak : double.NegativeInfinity;
        double xLopp = piirkond.Count > 0 ? piirkond[^1].Parem : double.PositiveInfinity;
        if (double.IsFinite(xAlgus) && vasak < xAlgus - 1)
        {
            parem += xAlgus - 1 - vasak;
            vasak = xAlgus - 1;
        }
        else if (double.IsFinite(xLopp) && parem > xLopp + 1)
        {
            vasak -= parem - xLopp - 1;
            parem = xLopp + 1;
        }
        return (Math.Max(-VahemikuPiir, vasak), Math.Min(VahemikuPiir, parem));
    }

    // ---------- Vormindamine ----------

    private static List<double> Piira(List<double> kohad) =>
        kohad.Count <= MaxNaidatud ? kohad : kohad.OrderBy(Math.Abs).Take(MaxNaidatud).Order().ToList();

    // punktideKuju: kui välja jäävad ainult üksikud punktid, kirjutatakse ℝ \ {a, b}. Kasvamis- ja kumerusvahemike
    // puhul seda ei kasutata – nt 1/(x - 1) kahaneb vahemikes (-∞; 1) ja (1; ∞), aga mitte nende ühendil.
    private static string HulgaTekst(List<Intervall> intervallid, bool punktideKuju = true)
    {
        if (intervallid.Count == 0)
            return "∅";

        if (intervallid.Count == 1 && intervallid[0].Vasak == double.NegativeInfinity && intervallid[0].Parem == double.PositiveInfinity)
            return "(-∞; ∞)";

        bool ainultPunktid = punktideKuju && intervallid[0].Vasak == double.NegativeInfinity && intervallid[^1].Parem == double.PositiveInfinity;
        for (int k = 0; ainultPunktid && k < intervallid.Count - 1; k++)
        {
            ainultPunktid = intervallid[k].Parem == intervallid[k + 1].Vasak
                && !intervallid[k].ParemKinnine && !intervallid[k + 1].VasakKinnine;
        }
        if (ainultPunktid)
        {
            var punktid = intervallid.Take(intervallid.Count - 1).Select(iv => iv.Parem).ToList();
            var naidatud = Piira(punktid);
            return "ℝ \\ {" + string.Join("; ", naidatud.Select(Vorminda)) + (naidatud.Count < punktid.Count ? "; …" : "") + "}";
        }

        // Paljude vahemike korral (nt perioodilised funktsioonid) näidatakse nullpunktile lähimaid.
        var valitud = intervallid;
        if (intervallid.Count > MaxVahemikke)
        {
            double Kaugus(Intervall iv) => iv.Vasak <= 0 && iv.Parem >= 0 ? 0 : Math.Min(Math.Abs(iv.Vasak), Math.Abs(iv.Parem));
            valitud = intervallid.OrderBy(Kaugus).Take(MaxVahemikke).OrderBy(iv => iv.Vasak).ToList();
        }

        var osad = valitud.Select(iv => iv.Vasak == iv.Parem
            ? "{" + Vorminda(iv.Vasak) + "}"
            : (iv.VasakKinnine ? "[" : "(") + Piiritekst(iv.Vasak) + "; " + Piiritekst(iv.Parem) + (iv.ParemKinnine ? "]" : ")"));
        var tekst = string.Join(" ∪ ", osad);
        if (valitud.Count < intervallid.Count)
        {
            if (valitud[0] != intervallid[0]) tekst = "… ∪ " + tekst;
            if (valitud[^1] != intervallid[^1]) tekst += " ∪ …";
        }
        return tekst;
    }

    private static string KohtadeTekst(List<double> kohad)
    {
        if (kohad.Count == 0)
            return "puuduvad";
        var naidatud = Piira(kohad);
        var tekst = string.Join(", ", naidatud.Select((x, i) => $"x{i + 1} = {Vorminda(x)}"));
        return naidatud.Count < kohad.Count ? $"{tekst}, … (kokku {kohad.Count} vahemikus [-{VahemikuPiir}; {VahemikuPiir}])" : tekst;
    }

    private static string EkstreemumideTekst(List<Ekstreemum> ekstreemumid)
    {
        if (ekstreemumid.Count == 0)
            return "puuduvad";
        var naidatud = ekstreemumid.Count <= MaxNaidatud
            ? ekstreemumid
            : ekstreemumid.OrderBy(e => Math.Abs(e.X)).Take(MaxNaidatud).OrderBy(e => e.X).ToList();
        var tekst = string.Join("; ", naidatud.Select(e => e.OnMaksimum
            ? $"x_max = {Vorminda(e.X)}, y_max = {Vorminda(e.Y)}"
            : $"x_min = {Vorminda(e.X)}, y_min = {Vorminda(e.Y)}"));
        return naidatud.Count < ekstreemumid.Count ? tekst + "; …" : tekst;
    }

    private static string PunktideTekst(List<Punkt> punktid)
    {
        if (punktid.Count == 0)
            return "puuduvad";
        var naidatud = punktid.Count <= MaxNaidatud ? punktid : punktid.OrderBy(p => Math.Abs(p.X)).Take(MaxNaidatud).OrderBy(p => p.X).ToList();
        var tekst = string.Join(", ", naidatud.Select(p => $"({Vorminda(p.X)}; {Vorminda(p.Y)})"));
        return naidatud.Count < punktid.Count ? tekst + ", …" : tekst;
    }

    private static string PaarsuseTekst(Func<double, double> f, double[] fv, bool[] maaratud)
    {
        bool paaris = true, paaritu = true;
        for (int i = 0; i <= Samme / 2; i++)
        {
            int j = Samme - i;   // X(j) = -X(i)
            if (maaratud[i] != maaratud[j])
                return "ei ole paaris ega paaritu (määramispiirkond ei ole sümmeetriline nullpunkti suhtes)";
            if (!maaratud[i])
                continue;
            double tol = 1e-7 * (1 + Math.Abs(fv[i]) + Math.Abs(fv[j]));
            if (Math.Abs(fv[j] - fv[i]) > tol) paaris = false;
            if (Math.Abs(fv[j] + fv[i]) > tol) paaritu = false;
        }

        if (paaris && paaritu)
            return "nii paaris- kui ka paaritu funktsioon (f(x) = 0)";
        if (paaris)
            return "paarisfunktsioon: f(-x) = f(x), graafik on sümmeetriline y-telje suhtes";
        if (paaritu)
            return "paaritu funktsioon: f(-x) = -f(x), graafik on sümmeetriline koordinaatide alguspunkti suhtes";

        // Näide, mis kinnitab vastust.
        foreach (var x in new[] { 1.0, 2.0, 0.5, 3.0 })
        {
            double a = f(x), b = f(-x);
            if (double.IsFinite(a) && double.IsFinite(b) && Math.Abs(a - b) > 1e-9 && Math.Abs(a + b) > 1e-9)
                return $"ei ole paaris ega paaritu: nt f({Vorminda(x)}) = {Vorminda(a)}, f({Vorminda(-x)}) = {Vorminda(b)}";
        }
        return "ei ole paaris ega paaritu: f(-x) ≠ f(x) ja f(-x) ≠ -f(x)";
    }

    private static string PerioodiTekst((double T, string Tekst)? periood, bool konstantne)
    {
        if (konstantne)
            return "konstantne funktsioon: iga T > 0 on periood (vähimat perioodi pole)";
        return periood is { } p ? $"perioodiline, periood T = {p.Tekst}" : "ei ole perioodiline";
    }

    private static string AsumptootideTekst(List<Asumptoot> asumptoodid)
    {
        if (asumptoodid.Count == 0)
            return "puuduvad";
        var osad = new List<string>();
        var vertikaalsed = asumptoodid.Where(a => a.OnVertikaalne).Select(a => a.X).ToList();
        if (vertikaalsed.Count > 0)
        {
            var naidatud = Piira(vertikaalsed);
            osad.Add("vertikaalne: " + string.Join(", ", naidatud.Select(x => $"x = {Vorminda(x)}")) + (naidatud.Count < vertikaalsed.Count ? ", …" : ""));
        }
        foreach (var a in asumptoodid.Where(a => !a.OnVertikaalne))
            osad.Add((a.K == 0 ? "horisontaalne: " : "kaldasümptoot: ") + SirgeTekst(a.K, a.B));
        return string.Join("; ", osad);
    }

    // y = kx + b ilusal kujul: y = x - 2, y = -3, y = 0.5x.
    public static string SirgeTekst(double k, double b)
    {
        string kx = k switch
        {
            0 => "",
            1 => "x",
            -1 => "-x",
            _ => Vorminda(k) + "x",
        };
        if (kx.Length == 0)
            return $"y = {Vorminda(b)}";
        if (Math.Abs(b) < 1e-12)
            return $"y = {kx}";
        return $"y = {kx} {(b > 0 ? "+" : "-")} {Vorminda(Math.Abs(b))}";
    }

    private static string Piiritekst(double v) => v switch
    {
        double.NegativeInfinity => "-∞",
        double.PositiveInfinity => "∞",
        _ => Vorminda(v),
    };

    public static string Vorminda(double v)
    {
        if (double.IsPositiveInfinity(v)) return "∞";
        if (double.IsNegativeInfinity(v)) return "-∞";
        if (Math.Abs(v - Math.Round(v)) < 1e-7)
            v = Math.Round(v);
        v = Math.Round(v, 4);
        if (v == 0)
            v = 0; // eemaldab "-0"
        return v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
