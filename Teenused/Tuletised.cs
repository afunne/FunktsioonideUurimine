using MathNet.Symbolics;
using Avaldis = MathNet.Symbolics.Expression;

namespace FunktsioonideUurimine.Teenused;

/// <summary>
/// Kirjutab tuletise leidmise samm-sammult lahti: summa reegel, astme reegel, konstandi reegel, korrutise, jagatise
/// ja liitfunktsiooni (ahela) reegel. Tulemus kontrollitakse MathNeti tuletisega; erinevuse korral tagastatakse null.
/// </summary>
internal static class Tuletised
{
    private static readonly SymbolicExpression X = SymbolicExpression.Variable("x");

    public record Tulemus(List<LahenduseViis> Viisid, SymbolicExpression Tuletis);

    // jark = 1: f → f'; jark = 2: f' → f''.
    public static Tulemus? Selgita(SymbolicExpression f, int jark)
    {
        string eel = jark == 1 ? "f(x)" : "f'(x)", jarg = jark == 1 ? "f'(x)" : "f''(x)";
        var viisid = new List<LahenduseViis>();

        var pohi = Viis(f, eel, jarg, jagatisReegliga: false);
        if (pohi is null) return null;
        viisid.Add(pohi.Value.Viis);

        // Teine viis: murd konstantse lugejaga (nt \frac{3}{x - 1}) jagatise reegliga, kui esimene kasutas astme reeglit.
        if (Murruks(f) is { } m && !Latex.SisaldabX(m.Lugeja.Expression) && f.Expression is not Avaldis.Sum
            && m.Nimetaja.Expression is not Avaldis.Power
            && Viis(f, eel, jarg, jagatisReegliga: true) is { } teine && teine.Viis.Sammud.Count > 0)
        {
            viisid.Add(teine.Viis with { Nimi = "Jagatise reegel" });
        }
        return new Tulemus(viisid, pohi.Value.Tuletis);
    }

    private static (LahenduseViis Viis, SymbolicExpression Tuletis)? Viis(SymbolicExpression f, string eel, string jarg, bool jagatisReegliga)
    {
        var reeglid = new List<string>();
        var sammud = new List<LahenduseSamm> { new($"Leiame tuletise funktsioonist ${eel} = {Latex.Vorminda(f)}$.") };

        var liikmed = f.Expression is Avaldis.Sum s
            ? s.Item.OrderByDescending(Latex.Jarjestus).Select(e => new SymbolicExpression(e)).ToList()
            : [f];
        if (liikmed.Count > 1)
        {
            reeglid.Add("(u \\pm v)' = u' \\pm v'");
            sammud.Add(new("Summa tuletis on liikmete tuletiste summa, seega võtame tuletise igast liikmest eraldi:"));
        }

        var osad = new List<SymbolicExpression>();
        foreach (var liige in liikmed)
        {
            var (too, tulemus) = Liige(liige, reeglid, jagatisReegliga);
            sammud.Add(new("", $"\\left({Latex.Vorminda(liige)}\\right)' = {too}"));
            osad.Add(tulemus);
        }

        var tuletis = osad.Aggregate((a, b) => a + b);
        if (Vorrandid.UhineTegur(tuletis) is { } yhine && liikmed.Count > 1)
            tuletis = yhine.Tegur * yhine.Sulus;

        if (!Vordne(tuletis, f.Differentiate(X)))
            return null;

        sammud.Insert(1, new("Kasutame reegleid: " + string.Join(", ", reeglid.Distinct().Select(r => $"${r}$")) + "."));
        sammud.Add(new(liikmed.Count > 1 ? "Liidame tulemused:" : "Seega:", $"{jarg} = {Latex.Vorminda(tuletis)}"));
        return (new LahenduseViis(jagatisReegliga ? "Jagatise reegel" : "Tuletise reeglid", sammud), tuletis);
    }

    // Üks liige: c · g(x). Tagastab arvutuskäigu (pärast "= ") ja tulemuse.
    private static (string Too, SymbolicExpression Tulemus) Liige(SymbolicExpression liige, List<string> reeglid, bool jagatisReegliga)
    {
        if (!Latex.SisaldabX(liige.Expression))
        {
            reeglid.Add("(c)' = 0");
            return ("0", 0);
        }

        var (c, g) = Kordaja(liige);
        var (kesk, tulemus) = Pohi(g, reeglid, jagatisReegliga);
        tulemus = c * tulemus;
        string loppTekst = Latex.Vorminda(tulemus);

        string too;
        if (Latex.Vaartus(c.Expression) is 1.0)
        {
            too = kesk;
        }
        else if (Latex.Vaartus(c.Expression) is -1.0)
        {
            too = "-" + (kesk.Contains(" + ") || kesk.Contains(" - ") ? Latex.Sulg(kesk) : kesk);
        }
        else
        {
            reeglid.Add("(c \\cdot u)' = c \\cdot u'");
            string sulus = kesk.Contains(" + ") || kesk.Contains(" - ") ? Latex.Sulg(kesk) : kesk;
            too = $"{Latex.Vorminda(c)} \\cdot {sulus}";
        }
        return (too == loppTekst || too.EndsWith("= " + loppTekst) ? too : $"{too} = {loppTekst}", tulemus);
    }

    // Arvuline kordaja ja ülejäänud osa: -4x^3 → (-4, x^3).
    private static (SymbolicExpression C, SymbolicExpression G) Kordaja(SymbolicExpression liige)
    {
        if (liige.Expression is Avaldis.Product p && p.Item.Head is Avaldis.Number or Avaldis.Approximation)
        {
            var c = new SymbolicExpression(p.Item.Head);
            var g = p.Item.Tail.Select(t => new SymbolicExpression(t)).Aggregate((a, b) => a * b);
            return (c, g);
        }
        return (1, liige);
    }

    private static (string Kesk, SymbolicExpression Tulemus) Pohi(SymbolicExpression g, List<string> reeglid, bool jagatisReegliga)
    {
        var a = g.Expression;
        string L(SymbolicExpression e) => Latex.Vorminda(e);
        string S(SymbolicExpression e) => e.Expression is Avaldis.Sum || Latex.Vorminda(e).StartsWith('-') ? Latex.Sulg(L(e)) : L(e);

        if (a.Equals(X.Expression))
        {
            reeglid.Add("(x)' = 1");
            return ("1", 1);
        }

        // x^n (ka \sqrt{x} = x^{1/2} ja \frac{1}{x^2} = x^{-2})
        if (a is Avaldis.Power p && p.Item1.Equals(X.Expression) && !Latex.SisaldabX(p.Item2)
            && Murd.FromDouble(Latex.Vaartus(p.Item2)) is { } n && !(jagatisReegliga && n.Mark < 0))
        {
            reeglid.Add("(x^n)' = nx^{n-1}");
            if (n.Q == 1 && n.P > 0)
            {
                long uus = n.P - 1;
                string aste = uus == 0 ? "" : uus == 1 ? "x" : $"x^{{{uus}}}";
                return (uus == 0 ? $"{n.P}" : $"{n.P}{aste}", Taisarvuks(n.P) * X.Pow(uus));
            }
            return ($"\\left(x^{{{n.Latex()}}}\\right)' = {n.Latex()}x^{{{(n - 1).Latex()}}}", Taisarvuks(n) * X.Pow(Taisarvuks(n - 1)));
        }

        // Murd: jagatise reegel (konstantse lugeja korral astme reegel, v.a teises viisis).
        if (Murruks(g) is { } m && (Latex.SisaldabX(m.Lugeja.Expression) || jagatisReegliga))
        {
            reeglid.Add("\\left(\\frac{u}{v}\\right)' = \\frac{u'v - uv'}{v^2}");
            var u = m.Lugeja; var v = m.Nimetaja;
            var du = u.Differentiate(X); var dv = v.Differentiate(X);
            var lugeja = Lihtsusta(du * v - u * dv);
            var tulemus = lugeja / v.Pow(2);
            string vRuut = v.Expression is Avaldis.Sum or Avaldis.Power or Avaldis.Product ? $"{Latex.Sulg(L(v))}^{{2}}" : $"{L(v)}^{{2}}";
            string kesk = $"\\frac{{{S(du)} \\cdot {S(v)} - {S(u)} \\cdot {S(dv)}}}{{{vRuut}}} = \\frac{{{L(lugeja)}}}{{{vRuut}}}";
            return (kesk, tulemus);
        }

        // Korrutis: (uv)' = u'v + uv'
        if (a is Avaldis.Product k && k.Item.Count(Latex.SisaldabX) >= 2)
        {
            reeglid.Add("(uv)' = u'v + uv'");
            var u = new SymbolicExpression(k.Item.Head);
            var v = k.Item.Tail.Select(t => new SymbolicExpression(t)).Aggregate((x, y) => x * y);
            var du = u.Differentiate(X); var dv = v.Differentiate(X);
            var tulemus = du * v + u * dv;
            string kesk = $"{S(du)} \\cdot {S(v)} + {S(u)} \\cdot {S(dv)}";
            if (Vorrandid.UhineTegur(tulemus) is { } yhine)
            {
                kesk += $" = {L(tulemus)}";
                tulemus = yhine.Tegur * yhine.Sulus;
            }
            return (kesk, tulemus);
        }

        // e^u
        if (a is Avaldis.Function { Item1.IsExp: true } || a is Avaldis.Power { Item1: Avaldis.Constant { Item.IsE: true } })
        {
            var u = new SymbolicExpression(a is Avaldis.Function f ? f.Item2 : ((Avaldis.Power)a).Item2);
            if (u.Expression.Equals(X.Expression))
            {
                reeglid.Add("(e^x)' = e^x");
                return ("e^{x}", g);
            }
            reeglid.Add("(e^u)' = e^u \\cdot u'");
            var du = u.Differentiate(X);
            return ($"e^{{{L(u)}}} \\cdot {S(du)}", g * du);
        }

        // u^n, kus u ≠ x (ahelreegel); \sqrt{u} eraldi
        if (a is Avaldis.Power pw && !Latex.SisaldabX(pw.Item2))
        {
            var u = new SymbolicExpression(pw.Item1);
            var nArv = new SymbolicExpression(pw.Item2);
            var du = u.Differentiate(X);
            if (Math.Abs(Latex.Vaartus(pw.Item2) - 0.5) < 1e-12)
            {
                reeglid.Add("(\\sqrt{u})' = \\frac{u'}{2\\sqrt{u}}");
                return ($"\\frac{{{L(du)}}}{{2\\sqrt{{{L(u)}}}}}", du / (2 * g));
            }
            reeglid.Add("(u^n)' = nu^{n-1} \\cdot u'");
            var aste = u.Pow(nArv - 1);
            string ahelOsa = Latex.Vaartus(du.Expression) is 1.0 ? "" : $" \\cdot {S(du)}";
            return ($"{L(nArv)} \\cdot {S(aste)}{ahelOsa}", nArv * aste * du);
        }

        if (a is Avaldis.Function fn)
        {
            var u = new SymbolicExpression(fn.Item2);
            var du = u.Differentiate(X);
            bool lihtne = u.Expression.Equals(X.Expression);
            string ahel = lihtne ? "" : $" \\cdot {S(du)}";
            if (fn.Item1.IsLn)
            {
                reeglid.Add(lihtne ? "(\\ln x)' = \\frac{1}{x}" : "(\\ln u)' = \\frac{u'}{u}");
                return ($"\\frac{{{L(du)}}}{{{L(u)}}}", du / u);
            }
            if (fn.Item1.IsSin)
            {
                reeglid.Add(lihtne ? "(\\sin x)' = \\cos x" : "(\\sin u)' = \\cos u \\cdot u'");
                var cos = SymbolicExpression.Parse("cos(x)").Substitute(X, u);
                return ($"{L(cos)}{ahel}", cos * du);
            }
            if (fn.Item1.IsCos)
            {
                reeglid.Add(lihtne ? "(\\cos x)' = -\\sin x" : "(\\cos u)' = -\\sin u \\cdot u'");
                var sin = SymbolicExpression.Parse("sin(x)").Substitute(X, u);
                return ($"-{L(sin)}{ahel}", -sin * du);
            }
            if (fn.Item1.IsTan)
            {
                reeglid.Add(lihtne ? "(\\tan x)' = \\frac{1}{\\cos^2 x}" : "(\\tan u)' = \\frac{u'}{\\cos^2 u}");
                var cos = SymbolicExpression.Parse("cos(x)").Substitute(X, u);
                return ($"\\frac{{{L(du)}}}{{{S(cos)}^{{2}}}}", du / cos.Pow(2));
            }
        }

        // Muu (nt 2^x): MathNeti tuletis ilma vahesammudeta.
        var d = g.Differentiate(X);
        return (L(d), d);
    }

    // Täpne ratsionaalarv MathNeti avaldisena (mitte ligikaudne double).
    private static SymbolicExpression Taisarvuks(Murd m) =>
        SymbolicExpression.Parse(m.Q == 1 ? $"({m.P})" : $"({m.P})/{m.Q}");

    private static SymbolicExpression Lihtsusta(SymbolicExpression e)
    {
        try { return e.Expand(); }
        catch (Exception) { return e; }
    }

    private static (SymbolicExpression Lugeja, SymbolicExpression Nimetaja)? Murruks(SymbolicExpression g)
    {
        try
        {
            var nimetaja = g.Denominator();
            return Latex.SisaldabX(nimetaja.Expression) ? (g.Numerator(), nimetaja) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Kas kaks avaldist on võrdsed (võrdleme väärtusi mitmes punktis).
    public static bool Vordne(SymbolicExpression a, SymbolicExpression b)
    {
        var fa = Vorrandid.Kompileeri(a);
        var fb = Vorrandid.Kompileeri(b);
        int vorreldud = 0;
        foreach (var x in new[] { -3.7, -2.2, -1.3, -0.45, 0.17, 0.6, 1.35, 2.2, 3.1, 4.9 })
        {
            double va = fa(x), vb = fb(x);
            if (!double.IsFinite(va) || !double.IsFinite(vb)) continue;
            if (Math.Abs(va - vb) > 1e-7 * (1 + Math.Abs(va) + Math.Abs(vb))) return false;
            vorreldud++;
        }
        return vorreldud >= 2;
    }
}
