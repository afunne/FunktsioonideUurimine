using System.Globalization;
using System.Text;
using MathNet.Symbolics;
using Avaldis = MathNet.Symbolics.Expression;

namespace FunktsioonideUurimine.Teenused;

// Lahenduskäik nagu Photomathis: iga vastuse juurde üks või mitu lahendusviisi, igas viisis sammud.
// Tekst võib sisaldada valemeid kujul $...$ (LaTeX, joonistab KaTeX); Valem on eraldi real olev valem;
// Tabel on märgitabel (esimene rida on päis, lahtrid on LaTeX); Joonis on väike graafik (viis „Graafikult“).
public record LahenduseSamm(string Tekst, string? Valem = null, IReadOnlyList<IReadOnlyList<string>>? Tabel = null,
    Joonis? Joonis = null);

// Väike graafik lahenduse sammus (joonistab lahendused.js Chart.js-iga): f(x) kõver (Y on null, kus f pole määratud),
// esile tõstetud punktid ja vertikaalsed asümptoodid. Varjuta = 1 värvib ala, kus f(x) > 0, Varjuta = -1 seal, kus f(x) < 0.
public record Joonis(double XMin, double XMax, double YMin, double YMax, IReadOnlyList<double> X, IReadOnlyList<double?> Y,
    IReadOnlyList<JooniseMark> Margid, IReadOnlyList<double> Asumptoodid, int Varjuta = 0);

public record JooniseMark(double X, double Y, string? Silt);

public record LahenduseViis(string Nimi, IReadOnlyList<LahenduseSamm> Sammud);

public record Lahendus(IReadOnlyList<LahenduseViis> Viisid);

// Võrrandi lahend: arvväärtus ja täpne kuju LaTeXis (nt -\sqrt{3}).
public record Juur(double V, string L)
{
    public bool OnTapne => !L.Contains('.');
    public string Tekst => OnTapne && Murd.FromDouble(V) is null ? $"{L} \\approx {Latex.Arv(V)}" : L;
}

/// <summary>Ratsionaalarv p/q täpseks arvutamiseks (ruutvõrrandi lahendid, Horneri skeem).</summary>
public readonly record struct Murd
{
    public long P { get; }
    public long Q { get; }

    public Murd(long p, long q = 1)
    {
        if (q == 0) throw new DivideByZeroException();
        if (q < 0) { p = checked(-p); q = checked(-q); }
        long s = Syt(Math.Abs(p), q);
        P = s == 0 ? 0 : p / s;
        Q = s == 0 ? 1 : q / s;
    }

    public double V => (double)P / Q;
    public bool OnNull => P == 0;
    public bool OnTaisarv => Q == 1;
    public int Mark => Math.Sign(P);

    public static Murd operator +(Murd a, Murd b) => new(checked(a.P * b.Q + b.P * a.Q), checked(a.Q * b.Q));
    public static Murd operator -(Murd a, Murd b) => new(checked(a.P * b.Q - b.P * a.Q), checked(a.Q * b.Q));
    public static Murd operator *(Murd a, Murd b) => new(checked(a.P * b.P), checked(a.Q * b.Q));
    public static Murd operator /(Murd a, Murd b) => new(checked(a.P * b.Q), checked(a.Q * b.P));
    public static Murd operator -(Murd a) => new(-a.P, a.Q);
    public static implicit operator Murd(long v) => new(v);

    // Lähim lihtne murd (nimetaja ≤ maxQ), kui v on sellele väga lähedal; muidu null.
    public static Murd? FromDouble(double v, long maxQ = 1000)
    {
        if (!double.IsFinite(v) || Math.Abs(v) > 1e12)
            return null;
        for (long q = 1; q <= maxQ; q++)
        {
            double p = Math.Round(v * q);
            if (Math.Abs(v - p / q) < 1e-9 * (1 + Math.Abs(v)))
                return new Murd((long)p, q);
        }
        return null;
    }

    public string Latex() => Q == 1
        ? P.ToString(CultureInfo.InvariantCulture)
        : (P < 0 ? "-" : "") + $"\\frac{{{Math.Abs(P)}}}{{{Q}}}";

    // Sulgudes, kui negatiivne (asendamisel: 4 \cdot (-2)).
    public string LatexSulgudes() => P < 0 ? $"\\left({Latex()}\\right)" : Latex();

    public static long Syt(long a, long b) => b == 0 ? a : Syt(b, a % b);
    public static long Vyk(long a, long b) => checked(a / Syt(a, b) * b);

    // √(p/q) = s·√m, kus m on ruuduvaba täisarv (√8 = 2√2, √(1/4) = 1/2).
    public (Murd S, long M)? Ruutjuur()
    {
        if (P < 0) return null;
        long n = checked(P * Q);
        long s = 1, m = 1;
        for (long k = 2; k * k <= n; k++)
        {
            while (n % (k * k) == 0) { n /= k * k; s *= k; }
        }
        m = n;
        return (new Murd(s, Q), m);
    }

    public override string ToString() => Latex();
}

/// <summary>
/// MathNet avaldis koolipärases LaTeXis: polünoom kahanevate astmete järgi (x^4 - 4x^3), e^{-x} (mitte \exp),
/// murrud murrujoonega, juured juuremärgiga. Asendusega (nt x → -1) näitab arvutuskäiku: 4 \cdot (-1)^3.
/// </summary>
public static class Latex
{
    private static readonly Avaldis X = SymbolicExpression.Variable("x").Expression;

    private readonly record struct Osa(string Tekst, int Tase);   // 1 summa, 2 korrutis / miinusmärgiga, 3 aste / murd / funktsioon, 4 aatom

    public static string Vorminda(SymbolicExpression a, string? xAsemel = null) => Tee(a.Expression, xAsemel).Tekst;

    public static string Vorminda(Avaldis a, string? xAsemel = null) => Tee(a, xAsemel).Tekst;

    // Arv LaTeXis: täisarv, lihtne murd või ümardatud kümnendmurd.
    public static string Arv(double v)
    {
        if (double.IsPositiveInfinity(v)) return "\\infty";
        if (double.IsNegativeInfinity(v)) return "-\\infty";
        return FunktsiooniAnalyysija.Vorminda(v);
    }

    public static string Sulg(string s) => $"\\left({s}\\right)";

    private static Osa Tee(Avaldis a, string? sub)
    {
        switch (a)
        {
            case Avaldis.Number or Avaldis.Approximation:
            {
                double v = Vaartus(a);
                string t = a is Avaldis.Number && Murd.FromDouble(v, 1_000_000) is { } m ? m.Latex() : Arv(v);
                return new Osa(t, v < 0 ? 2 : t.StartsWith("\\frac") ? 3 : 4);
            }
            case Avaldis.Identifier:
                if (a.Equals(X) && sub is not null)
                    return new Osa(sub.All(ch => char.IsDigit(ch) || ch == '.') ? sub : Sulg(sub), 4);
                return new Osa(new SymbolicExpression(a).ToString(), 4);
            case Avaldis.Constant c:
                return new Osa(c.Item.IsPi ? "\\pi" : c.Item.IsE ? "e" : new SymbolicExpression(a).ToString(), 4);
            case Avaldis.Sum s:
                return Summa(s.Item.ToList(), sub);
            case Avaldis.Product p:
                return Korrutis(p.Item.ToList(), sub);
            case Avaldis.Power p:
                return Aste(p.Item1, p.Item2, sub);
            case Avaldis.Function f:
                return Funktsioon(f, sub);
            default:
                if (a.IsPositiveInfinity) return new Osa("\\infty", 4);
                if (a.IsNegativeInfinity) return new Osa("-\\infty", 2);
                return new Osa(new SymbolicExpression(a).ToString(), 4);
        }
    }

    private static Osa Summa(List<Avaldis> liikmed, string? sub)
    {
        var sb = new StringBuilder();
        int i = 0;
        foreach (var liige in liikmed.OrderByDescending(Jarjestus))
        {
            bool negatiivne = OnNegatiivne(liige);
            var osa = Tee(negatiivne ? Vastand(liige) : liige, sub);
            string tekst = osa.Tase <= 1 ? Sulg(osa.Tekst) : osa.Tekst;
            if (i++ == 0)
                sb.Append(negatiivne ? "-" + tekst : tekst);
            else
                sb.Append(negatiivne ? " - " : " + ").Append(tekst);
        }
        return new Osa(sb.ToString(), 1);
    }

    // Liikmete järjekord: kõrgem x aste ettepoole, vabaliige lõppu (nt 0.25x^4 - 2x^2, mitte -2x^2 + 0.25x^4).
    public static double Jarjestus(Avaldis liige)
    {
        if (!SisaldabX(liige)) return -1;
        try
        {
            var s = new SymbolicExpression(liige);
            var x = SymbolicExpression.Variable("x");
            if (s.IsPolynomial(x)) return s.Coefficients(x).Length - 1;
        }
        catch (Exception) { }
        return 1000;
    }

    private static Osa Korrutis(List<Avaldis> tegurid, string? sub)
    {
        bool negatiivne = false;
        string? lugejaArv = null, nimetajaArv = null;
        var lugeja = new List<Avaldis>();
        var nimetaja = new List<Avaldis>();

        foreach (var t in tegurid)
        {
            if (t is Avaldis.Number or Avaldis.Approximation)
            {
                double v = Vaartus(t);
                if (v < 0) { negatiivne = !negatiivne; v = -v; }
                if (t is Avaldis.Number && Murd.FromDouble(v, 1_000_000) is { } m)
                {
                    if (m.P != 1) lugejaArv = m.P.ToString(CultureInfo.InvariantCulture);
                    if (m.Q != 1) nimetajaArv = m.Q.ToString(CultureInfo.InvariantCulture);
                }
                else if (Math.Abs(v - 1) > 1e-12)
                {
                    lugejaArv = Arv(v);
                }
            }
            else if (t is Avaldis.Power p && OnNegatiivne(p.Item2) && !(p.Item1 is Avaldis.Constant c && c.Item.IsE))
            {
                var aste = Vastand(p.Item2);
                nimetaja.Add(aste.Equals(Avaldis.One) ? p.Item1 : new SymbolicExpression(p.Item1).Pow(new SymbolicExpression(aste)).Expression);
            }
            else
            {
                lugeja.Add(t);
            }
        }

        // Tegurite järjekord nagu koolis: x ja polünoomid enne, e astmed ja funktsioonid lõpus (2xe^{-x}, mitte 2e^{-x}x).
        static int Liik(Avaldis t) => t switch
        {
            Avaldis.Function => 2,
            Avaldis.Power p when p.Item1 is Avaldis.Constant || Latex.SisaldabX(p.Item2) => 2,
            Avaldis.Sum => 1,
            _ => 0,
        };

        string Liida(string? arv, List<Avaldis> osad, bool uksi)
        {
            var tukid = new List<string>();
            if (arv is not null) tukid.Add(arv);
            foreach (var o in osad.OrderBy(Liik))
            {
                var osa = Tee(o, sub);
                bool sulgu = osa.Tase <= 1 ? !(uksi && arv is null && osad.Count == 1) : osa.Tase == 2;
                tukid.Add(sulgu ? Sulg(osa.Tekst) : osa.Tekst);
            }
            if (tukid.Count == 0) return "1";
            var sb = new StringBuilder(tukid[0]);
            for (int k = 1; k < tukid.Count; k++)
            {
                bool punkt = sub is not null || char.IsDigit(tukid[k][0]) || (char.IsDigit(tukid[k - 1][^1]) && tukid[k].StartsWith("\\frac"));
                bool tyhik = !punkt && char.IsLetter(tukid[k - 1][^1]) && char.IsLetter(tukid[k][0]);   // \pi x, mitte \pix
                sb.Append(punkt ? " \\cdot " : tyhik ? " " : "").Append(tukid[k]);
            }
            return sb.ToString();
        }

        string tekst;
        int tase;
        if (nimetaja.Count > 0 || nimetajaArv is not null)
        {
            tekst = $"\\frac{{{Liida(lugejaArv, lugeja, true)}}}{{{Liida(nimetajaArv, nimetaja, true)}}}";
            tase = 3;
        }
        else if (lugeja.Count == 1 && lugejaArv is null)
        {
            // Ainult märk ja üks tegur, nt -x või -(x + 1).
            var osa = Tee(lugeja[0], sub);
            (tekst, tase) = (negatiivne && osa.Tase <= 1 ? Sulg(osa.Tekst) : osa.Tekst, osa.Tase);
        }
        else
        {
            tekst = Liida(lugejaArv, lugeja, false);
            tase = lugeja.Count == 0 ? 4 : 2;
        }
        return negatiivne ? new Osa("-" + tekst, 2) : new Osa(tekst, tase);
    }

    private static Osa Aste(Avaldis alus, Avaldis astendaja, string? sub)
    {
        if (astendaja is Avaldis.Number or Avaldis.Approximation)
        {
            double v = Vaartus(astendaja);
            if (v < 0)
            {
                var positiivne = new SymbolicExpression(alus).Pow(new SymbolicExpression(Vastand(astendaja))).Expression;
                return new Osa($"\\frac{{1}}{{{Tee(positiivne, sub).Tekst}}}", 3);
            }
            if (Math.Abs(v - 0.5) < 1e-12)
                return new Osa($"\\sqrt{{{Tee(alus, sub).Tekst}}}", 4);
            for (int n = 3; n <= 10; n++)
            {
                if (Math.Abs(v - 1.0 / n) < 1e-12)
                    return new Osa($"\\sqrt[{n}]{{{Tee(alus, sub).Tekst}}}", 4);
            }
        }

        if (alus is Avaldis.Constant c && c.Item.IsE)
            return new Osa($"e^{{{Tee(astendaja, sub).Tekst}}}", 3);

        var a = Tee(alus, sub);
        string aluseTekst = a.Tase >= 4 && !a.Tekst.StartsWith('-') ? a.Tekst : Sulg(a.Tekst);
        return new Osa($"{aluseTekst}^{{{Tee(astendaja, sub).Tekst}}}", 3);
    }

    private static Osa Funktsioon(Avaldis.Function f, string? sub)
    {
        if (f.Item1.IsExp)
            return new Osa($"e^{{{Tee(f.Item2, sub).Tekst}}}", 3);
        if (f.Item1.IsAbs)
            return new Osa($"\\left|{Tee(f.Item2, sub).Tekst}\\right|", 4);

        string nimi = f.Item1.IsLn ? "\\ln" : f.Item1.IsLg ? "\\lg" : f.Item1.IsSin ? "\\sin" : f.Item1.IsCos ? "\\cos"
            : f.Item1.IsTan ? "\\tan" : "\\operatorname{" + f.Item1.ToString().ToLowerInvariant() + "}";
        var arg = Tee(f.Item2, sub);
        bool lihtne = sub is null && f.Item2 is Avaldis.Identifier;
        return new Osa(lihtne ? $"{nimi} {arg.Tekst}" : $"{nimi}{Sulg(arg.Tekst)}", 3);
    }

    // ---------- Abi ----------

    public static double Vaartus(Avaldis a)
    {
        try { return new SymbolicExpression(a).RealNumberValue; }
        catch (Exception) { return double.NaN; }
    }

    private static bool OnNegatiivne(Avaldis a) => a switch
    {
        Avaldis.Number or Avaldis.Approximation => Vaartus(a) < 0,
        Avaldis.Product p => !p.Item.IsEmpty && OnNegatiivne(p.Item.Head),
        _ => a.IsNegativeInfinity,
    };

    private static Avaldis Vastand(Avaldis a) => (-new SymbolicExpression(a)).Expression;

    public static bool SisaldabX(Avaldis a) => a switch
    {
        Avaldis.Identifier => a.Equals(X),
        Avaldis.Function f => SisaldabX(f.Item2),
        Avaldis.FunctionN f => f.Item2.Any(SisaldabX),
        Avaldis.Power p => SisaldabX(p.Item1) || SisaldabX(p.Item2),
        Avaldis.Sum s => s.Item.Any(SisaldabX),
        Avaldis.Product k => k.Item.Any(SisaldabX),
        _ => false,
    };
}
