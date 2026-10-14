using MathNet.Symbolics;
using Avaldis = MathNet.Symbolics.Expression;

namespace FunktsioonideUurimine.Teenused;

// Üks võrrandi lahendusviis: sammud ja leitud lahendid. Perioodilise võrrandi (sin x = 0) korral on lahendeid lõpmata palju
// ja Uldlahend on nende üldkuju (x = n\pi), Juured on lahendid vahemikus [-50; 50].
internal record VorrandiViis(string Nimi, List<LahenduseSamm> Sammud, List<Juur> Juured, string? Uldlahend = null);

/// <summary>
/// Lahendab võrrandi g(x) = 0 koolis õpitud võtetega ja kirjutab lahenduskäigu: lineaarvõrrand, ruutvõrrand (diskriminant,
/// taandatud ruutvõrrandi valem, Viète, tegurdamine), x sulgude ette, asendus t = x², ratsionaalsed lahendid ja Horneri skeem,
/// murd (lugeja = 0), korrutis (iga tegur = 0), e^u, ln u, sin/cos/tan. Kui ükski võte ei sobi, tagastatakse tühi loend.
/// </summary>
internal static class Vorrandid
{
    private static readonly SymbolicExpression X = SymbolicExpression.Variable("x");
    private const int MaxViise = 3;

    public static List<VorrandiViis> Lahenda(SymbolicExpression g, int sygavus = 0)
    {
        if (sygavus > 4)
            return [];

        if (!Latex.SisaldabX(g.Expression) && Latex.Vaartus(g.Expression) is var arv && double.IsFinite(arv) && arv != 0)
            return [new("Konstant", [new($"${Latex.Vorminda(g)} \\neq 0$, seega lahendid puuduvad.")], [])];

        if (Kordajad(g) is { } c)
        {
            // Sulgudega kuju (nt (x - 1)^2 + 2(x - 1)(x + 2)) avatakse enne lahendamist.
            var viisid = LahendaPolynoom(c);
            string avatud = Polynoom(c);
            if (Latex.Vorminda(g) == avatud) return viisid;
            var samm = new LahenduseSamm("Avame sulud ja koondame sarnased liikmed:", avatud + " = 0");
            return viisid.Select(v => v with { Sammud = [samm, .. v.Sammud] }).ToList();
        }

        var a = g.Expression;

        if (OnEksponent(a, out _))
        {
            return [new("Eksponendi omadus",
                [new($"${Latex.Vorminda(a)} > 0$ iga $x$ korral (e astmed on alati positiivsed), seega ei saa see olla null.")], [])];
        }

        if (Isoleeri(g) is { } isoleeritud)
            return [isoleeritud];

        // Murd: lugeja = 0, nimetaja ≠ 0.
        if (Murruks(g) is { } murd)
            return Lahenda(murd.Lugeja, sygavus + 1).Select(v => Murrust(murd.Lugeja, murd.Nimetaja, v)).ToList();

        // Summa ühise teguriga, nt e^{-x} - xe^{-x} = e^{-x}(1 - x).
        if (a is Avaldis.Sum && UhineTegur(g) is { } yhine)
        {
            var sammud = new List<LahenduseSamm>
            {
                new($"Toome ühise teguri ${Latex.Vorminda(yhine.Tegur)}$ sulgude ette:",
                    $"{Latex.Vorminda(yhine.Tegur)}{Latex.Sulg(Latex.Vorminda(yhine.Sulus))} = 0"),
            };
            return Korrutis([yhine.Tegur, yhine.Sulus], sygavus)
                .Select(v => v with { Sammud = [.. sammud, .. v.Sammud] }).ToList();
        }

        if (a is Avaldis.Product p)
            return Korrutis(p.Item.Where(Latex.SisaldabX).Select(t => new SymbolicExpression(t)).ToList(), sygavus);

        // Aste on null ainult siis, kui alus on null (nt \sqrt{x - 1} = 0).
        if (a is Avaldis.Power pw && !Latex.SisaldabX(pw.Item2) && Latex.Vaartus(pw.Item2) > 0)
        {
            var alus = new SymbolicExpression(pw.Item1);
            var samm = new LahenduseSamm($"Aste (või juur) on null ainult siis, kui alus on null:", $"{Latex.Vorminda(alus)} = 0");
            return Lahenda(alus, sygavus + 1).Select(v => v with { Sammud = [samm, .. v.Sammud] }).ToList();
        }

        if (a is Avaldis.Function f)
        {
            var u = new SymbolicExpression(f.Item2);
            if (f.Item1.IsLn)
            {
                var samm = new LahenduseSamm($"Logaritm on null, kui logaritmitav on $1$ (sest $\\ln 1 = 0$):", $"{Latex.Vorminda(u)} = 1");
                if (u.Expression.Equals(X.Expression))
                    return [new("Logaritmi omadus", [samm], [new(1, "1")])];
                return Lahenda(u - 1, sygavus + 1).Select(v => v with { Sammud = [samm, .. v.Sammud] }).ToList();
            }
            if ((f.Item1.IsSin || f.Item1.IsCos || f.Item1.IsTan) && Kordajad(u) is { Length: 2 } lin && !lin[1].OnNull)
                return [Trigonomeetriline(f, lin[1], lin[0])];
        }

        return [];
    }

    // ---------- Polünoomid ----------

    // Polünoomi kordajad ratsionaalarvudena (c[i] on x^i kordaja) või null, kui see pole ratsionaalsete kordajatega polünoom.
    public static Murd[]? Kordajad(SymbolicExpression g)
    {
        try
        {
            g = g.Expand();
            if (!g.IsPolynomial(X))
                return null;
            var k = g.Coefficients(X);
            var c = new Murd[k.Length];
            for (int i = 0; i < k.Length; i++)
            {
                if (Murd.FromDouble(Latex.Vaartus(k[i].Expression)) is not { } m)
                    return null;
                c[i] = m;
            }
            return Karbi(c);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Murd[] Karbi(Murd[] c)
    {
        int n = c.Length;
        while (n > 1 && c[n - 1].OnNull) n--;
        return c[..n];
    }

    public static string Polynoom(Murd[] c, string m = "x")
    {
        var sb = new System.Text.StringBuilder();
        bool esimene = true;
        for (int i = c.Length - 1; i >= 0; i--)
        {
            if (c[i].OnNull) continue;
            bool negatiivne = c[i].P < 0;
            var abs = negatiivne ? -c[i] : c[i];
            string aste = i == 0 ? "" : i == 1 ? m : $"{m}^{{{i}}}";
            string kordaja = i > 0 && abs.P == 1 && abs.Q == 1 ? "" : abs.Latex();
            if (esimene) sb.Append(negatiivne ? "-" : "");
            else sb.Append(negatiivne ? " - " : " + ");
            sb.Append(kordaja).Append(aste);
            esimene = false;
        }
        return esimene ? "0" : sb.ToString();
    }

    public static Murd Vaartus(Murd[] c, Murd x)
    {
        Murd s = 0;
        for (int i = c.Length - 1; i >= 0; i--) s = s * x + c[i];
        return s;
    }

    public static List<VorrandiViis> LahendaPolynoom(Murd[] c, string m = "x")
    {
        try
        {
            c = Karbi(c);
            int n = c.Length - 1;
            if (n == 0)
            {
                return [new("Konstant", [new(c[0].OnNull
                    ? "Võrdus kehtib iga $x$ korral."
                    : $"${c[0].Latex()} \\neq 0$, seega lahendid puuduvad.")], [])];
            }
            if (n == 1)
                return [Lineaar(c, m)];
            if (n == 2)
                return Ruut(c[2], c[1], c[0], m);

            var viisid = new List<VorrandiViis>();
            int k = Array.FindIndex(c, t => !t.OnNull);
            if (k > 0)
                viisid.AddRange(SulgudeEtte(c, k, m));
            if (n == 4 && c[1].OnNull && c[3].OnNull)
                viisid.AddRange(Bikvadraat(c, m));
            if (k == 0 && Ratsionaalsed(c, m) is { } r)
                viisid.Add(r);
            return viisid.Take(MaxViise).ToList();
        }
        catch (OverflowException)
        {
            return [];
        }
    }

    private static VorrandiViis Lineaar(Murd[] c, string m)
    {
        Murd a = c[1], b = c[0], r = -b / a;
        var sammud = new List<LahenduseSamm>();
        if (!b.OnNull)
            sammud.Add(new("Viime vabaliikme teisele poole (märk muutub):", $"{Polynoom([0, a], m)} = {(-b).Latex()}"));
        if (!(a.P == 1 && a.Q == 1))
            sammud.Add(new($"Jagame mõlemad pooled arvuga ${a.Latex()}$:", $"{m} = {r.Latex()}"));
        if (sammud.Count == 0)
            sammud.Add(new("", $"{m} = 0"));
        return new("Lineaarvõrrand", sammud, [new(r.V, r.Latex())]);
    }

    private static List<VorrandiViis> Ruut(Murd a, Murd b, Murd c, string m)
    {
        var juured = RuutJuured(a, b, c);
        string vorrand = Polynoom([c, b, a], m) + " = 0";
        var viisid = new List<VorrandiViis>();

        if (b.OnNull)
        {
            var w = -c / a;
            string jagamine = a.P == 1 && a.Q == 1 ? "" : $" ja jagame arvuga ${a.Latex()}$";
            var sammud = new List<LahenduseSamm>
            {
                new($"Liiget ${m}$ pole, seega avaldame ${m}^2$. Viime vabaliikme teisele poole{jagamine}:", $"{m}^{{2}} = {w.Latex()}"),
            };
            if (w.Mark < 0)
            {
                sammud.Add(new($"Ruut ei saa olla negatiivne, seega lahendid puuduvad."));
            }
            else if (w.OnNull)
            {
                sammud.Add(new("", $"{m} = 0"));
            }
            else
            {
                string lihtne = Juurena(w, "\\pm");
                string valem = $"{m} = \\pm\\sqrt{{{w.Latex()}}}";
                sammud.Add(new("Võtame mõlemast poolest ruutjuure (pluss- ja miinusmärgiga):", lihtne == $"\\pm\\sqrt{{{w.Latex()}}}" ? valem : $"{valem} = {lihtne}"));
            }
            viisid.Add(new($"Avaldame ${m}^2$", sammud, juured));
        }
        else if (c.OnNull)
        {
            var r = -b / a;
            viisid.Add(new("Tegurdamine", [
                new($"Vabaliiget pole, seega toome ${m}$ sulgude ette:", $"{m}{Latex.Sulg(Polynoom([b, a], m))} = 0"),
                new("Korrutis on null, kui vähemalt üks tegur on null:", $"{m} = 0 \\quad \\text{{või}} \\quad {Polynoom([b, a], m)} = 0"),
                new("", $"{m}_1 = 0, \\quad {m}_2 = {r.Latex()}"),
            ], juured));
        }

        viisid.Add(Diskriminant(a, b, c, m, vorrand, juured));

        if (a.P == 1 && a.Q == 1 && !b.OnNull && !c.OnNull)
        {
            viisid.Add(Taandatud(b, c, m, juured));
            if (juured.Count == 2 && juured.All(j => Murd.FromDouble(j.V, 1) is not null))
            {
                viisid.Add(new("Viète'i teoreem", [
                    new($"Taandatud ruutvõrrandi ${m}^2 + p{m} + q = 0$ lahendite summa on $-p$ ja korrutis on $q$:",
                        $"{m}_1 + {m}_2 = {(-b).Latex()}, \\quad {m}_1 \\cdot {m}_2 = {c.Latex()}"),
                    new($"Otsime kaks arvu, mille summa on ${(-b).Latex()}$ ja korrutis ${c.Latex()}$. Need on ${juured[0].L}$ ja ${juured[1].L}$.",
                        $"{m}_1 = {juured[0].L}, \\quad {m}_2 = {juured[1].L}"),
                ], juured));
            }
        }
        return viisid.Take(MaxViise).ToList();
    }

    private static VorrandiViis Diskriminant(Murd a, Murd b, Murd c, string m, string vorrand, List<Juur> juured)
    {
        var d = b * b - 4 * a * c;
        var sammud = new List<LahenduseSamm>
        {
            new($"Ruutvõrrandi $a{m}^2 + b{m} + c = 0$ kordajad: $a = {a.Latex()}$, $b = {b.Latex()}$, $c = {c.Latex()}$."),
            new("Leiame diskriminandi:", $"D = b^2 - 4ac = {b.LatexSulgudes()}^{{2}} - 4 \\cdot {a.LatexSulgudes()} \\cdot {c.LatexSulgudes()} = {d.Latex()}"),
        };
        if (d.Mark < 0)
        {
            sammud.Add(new("Kuna $D < 0$, siis reaalarvulisi lahendeid pole."));
        }
        else if (d.OnNull)
        {
            sammud.Add(new("Kuna $D = 0$, on üks (kahekordne) lahend:",
                $"{m} = \\frac{{-b}}{{2a}} = \\frac{{{(-b).Latex()}}}{{{(2 * a).Latex()}}} = {(-b / (2 * a)).Latex()}"));
        }
        else
        {
            string juur = d.Ruutjuur() is { M: 1 } r ? $" = \\frac{{{(-b).Latex()} \\pm {r.S.Latex()}}}{{{(2 * a).Latex()}}}" : "";
            sammud.Add(new("Kuna $D > 0$, on kaks lahendit:",
                $"{m}_{{1,2}} = \\frac{{-b \\pm \\sqrt{{D}}}}{{2a}} = \\frac{{{(-b).Latex()} \\pm \\sqrt{{{d.Latex()}}}}}{{{(2 * a).Latex()}}}{juur}"));
            sammud.Add(new("", $"{m}_1 = {juured[0].L}, \\quad {m}_2 = {juured[1].L}"));
        }
        return new("Diskriminant", sammud, juured);
    }

    private static VorrandiViis Taandatud(Murd p, Murd q, string m, List<Juur> juured)
    {
        var pool = -p / 2;
        var w = p / 2 * (p / 2) - q;
        var sammud = new List<LahenduseSamm>
        {
            new($"Võrrand on taandatud kujul ${m}^2 + p{m} + q = 0$, kus $p = {p.Latex()}$ ja $q = {q.Latex()}$. Kasutame valemit:",
                $"{m}_{{1,2}} = -\\frac{{p}}{{2}} \\pm \\sqrt{{\\left(\\frac{{p}}{{2}}\\right)^2 - q}}"),
            new("Paneme arvud sisse:",
                $"{m}_{{1,2}} = {pool.Latex()} \\pm \\sqrt{{{(p / 2).LatexSulgudes()}^{{2}} - {q.LatexSulgudes()}}} = {pool.Latex()} \\pm \\sqrt{{{w.Latex()}}}"),
        };
        if (w.Mark < 0)
            sammud.Add(new("Juure all on negatiivne arv, seega lahendid puuduvad."));
        else if (juured.Count == 2)
            sammud.Add(new("", $"{m}_1 = {juured[0].L}, \\quad {m}_2 = {juured[1].L}"));
        return new("Taandatud ruutvõrrand", sammud, juured);
    }

    // Ruutvõrrandi lahendid täpsel kujul, nt \frac{3 - \sqrt{5}}{2}.
    public static List<Juur> RuutJuured(Murd a, Murd b, Murd c)
    {
        var d = b * b - 4 * a * c;
        if (d.Mark < 0) return [];
        var keskkoht = -b / (2 * a);
        if (d.OnNull) return [new(keskkoht.V, keskkoht.Latex())];

        if (d.Ruutjuur() is not { } r)
            return [];
        var nihe = r.S / (2 * a);
        if (nihe.Mark < 0) nihe = -nihe;
        if (r.M == 1)
        {
            Murd x1 = keskkoht - nihe, x2 = keskkoht + nihe;
            return [new(x1.V, x1.Latex()), new(x2.V, x2.Latex())];
        }
        double s = Math.Sqrt(r.M);
        return [
            new(keskkoht.V - nihe.V * s, KeskPlussJuur(keskkoht, nihe, r.M, "-")),
            new(keskkoht.V + nihe.V * s, KeskPlussJuur(keskkoht, nihe, r.M, "+")),
        ];
    }

    // A ± B√M ühise nimetajaga: \frac{3 + \sqrt{5}}{2}, 2 - \sqrt{3}, -\sqrt{2}.
    private static string KeskPlussJuur(Murd a, Murd b, long m, string mark)
    {
        long nim = Murd.Vyk(a.Q, b.Q);
        long aa = checked(a.P * (nim / a.Q)), bb = checked(b.P * (nim / b.Q));
        string juur = (bb == 1 ? "" : bb.ToString()) + $"\\sqrt{{{m}}}";
        string lugeja = aa == 0
            ? (mark == "+" ? "" : mark) + juur
            : $"{aa} {mark} {juur}";
        if (nim == 1) return lugeja;
        if (aa == 0 && mark == "-") return $"-\\frac{{{juur}}}{{{nim}}}";
        return $"\\frac{{{lugeja}}}{{{nim}}}";
    }

    // ±√w lihtsustatult: \pm 2\sqrt{2}, \pm\frac{1}{2}.
    private static string Juurena(Murd w, string mark)
    {
        if (w.Ruutjuur() is not { } r) return $"{mark}\\sqrt{{{w.Latex()}}}";
        if (r.M == 1) return mark + r.S.Latex();
        string kordaja = r.S.P == 1 ? "" : r.S.P.ToString();
        string juur = $"{kordaja}\\sqrt{{{r.M}}}";
        return mark + (r.S.Q == 1 ? juur : $"\\frac{{{juur}}}{{{r.S.Q}}}");
    }

    private static List<Juur> JuuredRuudust(Juur t)
    {
        if (t.V < -1e-12) return [];
        if (Math.Abs(t.V) < 1e-12) return [new(0, "0")];
        double v = Math.Sqrt(t.V);
        if (Murd.FromDouble(t.V) is { } w)
        {
            string plus = Juurena(w, "");
            return [new(-v, "-" + plus), new(v, plus)];
        }
        return [new(-v, $"-\\sqrt{{{t.L}}}"), new(v, $"\\sqrt{{{t.L}}}")];
    }

    private static IEnumerable<VorrandiViis> SulgudeEtte(Murd[] c, int k, string m)
    {
        var jaak = c[k..];
        string tegur = k == 1 ? m : $"{m}^{{{k}}}";
        if (jaak.Length == 1)
        {
            yield return new("Astme omadus", [new($"${tegur} = 0$ ainult siis, kui ${m} = 0$.", $"{m} = 0")], [new(0, "0")]);
            yield break;
        }

        string jaagiTekst = Polynoom(jaak, m);
        var algus = new List<LahenduseSamm>
        {
            new($"Toome ${tegur}$ sulgude ette:", $"{tegur}{Latex.Sulg(jaagiTekst)} = 0"),
            new("Korrutis on null, kui vähemalt üks tegur on null:", $"{tegur} = 0 \\quad \\text{{või}} \\quad {jaagiTekst} = 0"),
            new($"Esimesest saame ${m} = 0$. Lahendame teise võrrandi ${jaagiTekst} = 0$:"),
        };
        var alam = LahendaPolynoom(jaak, m);
        foreach (var v in alam.Take(2))
        {
            var juured = Yhenda([new(0, "0"), .. v.Juured]);
            string nimi = alam.Count > 1 ? $"${tegur}$ sulgude ette + {v.Nimi}" : $"${tegur}$ sulgude ette";
            yield return new(nimi, [.. algus, .. v.Sammud], juured);
        }
    }

    private static IEnumerable<VorrandiViis> Bikvadraat(Murd[] c, string m)
    {
        var tViisid = Ruut(c[4], c[2], c[0], "t");
        if (tViisid.Count == 0) yield break;
        var tViis = tViisid.FirstOrDefault(v => v.Nimi == "Diskriminant") ?? tViisid[0];

        List<LahenduseSamm> sammud =
        [
            new($"Võrrandis on ainult paarisastmed. Asendame $t = {m}^2$ (siis ${m}^4 = t^2$):", Polynoom([c[0], c[2], c[4]], "t") + " = 0"),
            .. tViis.Sammud,
            new($"Läheme tagasi ${m}$ juurde: ${m}^2 = t$."),
        ];
        var juured = new List<Juur>();
        foreach (var t in tViis.Juured)
        {
            var x = JuuredRuudust(t);
            if (x.Count == 0)
                sammud.Add(new($"${m}^2 = {t.L}$ – lahendeid pole, sest ruut ei ole negatiivne."));
            else if (x.Count == 1)
                sammud.Add(new("", $"{m}^2 = 0 \\Rightarrow {m} = 0"));
            else
                sammud.Add(new("", $"{m}^2 = {t.L} \\Rightarrow {m} = \\pm {x[1].L}"));
            juured.AddRange(x);
        }
        yield return new($"Asendus $t = {m}^2$", sammud, Yhenda(juured));
    }

    private static VorrandiViis? Ratsionaalsed(Murd[] c, string m)
    {
        // Täisarvulised kordajad.
        long kordaja = c.Aggregate(1L, (s, t) => Murd.Vyk(s, t.Q));
        var a = c.Select(t => (t * kordaja).P).ToArray();
        long a0 = Math.Abs(a[0]), an = Math.Abs(a[^1]);
        if (a0 == 0 || a0 > 10_000_000 || an > 10_000_000) return null;

        var kandidaadid = new List<Murd>();
        foreach (var p in Jagajad(a0))
            foreach (var q in Jagajad(an))
            {
                kandidaadid.Add(new Murd(p, q));
                kandidaadid.Add(new Murd(-p, q));
            }
        kandidaadid = kandidaadid.Distinct().OrderBy(k => Math.Abs(k.V)).ThenBy(k => k.V < 0).Take(400).ToList();

        int indeks = kandidaadid.FindIndex(r => Vaartus(c, r).OnNull);
        if (indeks < 0) return null;
        var juur = kandidaadid[indeks];

        // Horneri skeem: jagatis Q(x) ja jääk 0.
        int n = c.Length - 1;
        var jagatis = new Murd[n];
        Murd jarg = 0;
        var rida = new List<string>();
        for (int i = n; i >= 1; i--)
        {
            jarg = jarg * juur + c[i];
            jagatis[i - 1] = jarg;
            rida.Add(jarg.Latex());
        }
        rida.Add((jarg * juur + c[0]).Latex());

        var sammud = new List<LahenduseSamm>();
        string pTekst = Polynoom(c, m);
        if (kordaja != 1)
            sammud.Add(new($"Korrutame võrrandi arvuga ${kordaja}$, et kõik kordajad oleksid täisarvud:", Polynoom(a.Select(t => new Murd(t)).ToArray(), m) + " = 0"));
        string jagajad = string.Join(", ", Jagajad(a0).Take(6).Select(d => $"\\pm {d}")) + (Jagajad(a0).Count() > 6 ? ", \\dots" : "");
        sammud.Add(new(an == 1
            ? $"Täisarvuline lahend peab olema vabaliikme ${a[0]}$ jagaja. Proovime arve ${jagajad}$."
            : $"Ratsionaalne lahend on kujul $\\frac{{p}}{{q}}$, kus $p$ jagab arvu ${a[0]}$ ja $q$ jagab arvu ${a[^1]}$. Proovime neid."));
        sammud.Add(new($"Sobib ${m} = {juur.Latex()}$:", $"P\\left({juur.Latex()}\\right) = {AsendaPolynoom(c, juur)} = 0"));

        var tabel = new List<IReadOnlyList<string>>
        {
            new[] { "" }.Concat(Enumerable.Range(0, n + 1).Select(i => $"${c[n - i].Latex()}$")).ToList(),
            new[] { $"${m} = {juur.Latex()}$" }.Concat(rida.Select(t => $"${t}$")).ToList(),
        };
        string tegur = juur.OnNull ? m : Polynoom([-juur, 1], m);
        string qTekst = Polynoom(jagatis, m);
        sammud.Add(new($"Jagame polünoomi tegurile ${tegur}$ Horneri skeemiga (viimane arv on jääk $0$):", null, tabel));
        sammud.Add(new("Saame tegurdatud kuju:", $"{Latex.Sulg(tegur)}{Latex.Sulg(qTekst)} = 0"));

        var alam = LahendaPolynoom(jagatis, m);
        if (alam.Count == 0) return null;
        sammud.Add(new($"Lahendame võrrandi ${qTekst} = 0$:"));
        sammud.AddRange(alam[0].Sammud);
        return new("Horneri skeem", sammud, Yhenda([new(juur.V, juur.Latex()), .. alam[0].Juured]));
    }

    private static string AsendaPolynoom(Murd[] c, Murd x)
    {
        var sb = new System.Text.StringBuilder();
        bool esimene = true;
        string xt = x.Q == 1 && x.P >= 0 ? x.Latex() : Latex.Sulg(x.Latex());
        for (int i = c.Length - 1; i >= 0; i--)
        {
            if (c[i].OnNull) continue;
            bool negatiivne = c[i].P < 0;
            var abs = negatiivne ? -c[i] : c[i];
            string aste = i == 0 ? "" : i == 1 ? xt : $"{xt}^{{{i}}}";
            string liige = i == 0 ? abs.Latex() : abs.P == 1 && abs.Q == 1 ? aste : $"{abs.Latex()} \\cdot {aste}";
            sb.Append(esimene ? (negatiivne ? "-" : "") : negatiivne ? " - " : " + ").Append(liige);
            esimene = false;
        }
        return sb.ToString();
    }

    private static IEnumerable<long> Jagajad(long n)
    {
        var vaiksed = new List<long>();
        var suured = new List<long>();
        for (long d = 1; d * d <= n; d++)
        {
            if (n % d != 0) continue;
            vaiksed.Add(d);
            if (d != n / d) suured.Add(n / d);
        }
        suured.Reverse();
        return vaiksed.Concat(suured);
    }

    private static List<Juur> Yhenda(IEnumerable<Juur> juured)
    {
        var tulemus = new List<Juur>();
        foreach (var j in juured.OrderBy(j => j.V))
        {
            if (tulemus.Count == 0 || Math.Abs(j.V - tulemus[^1].V) > 1e-9 * (1 + Math.Abs(j.V)))
                tulemus.Add(j);
        }
        return tulemus;
    }

    // ---------- Muud võrrandid ----------

    // ln x = r ja e^x = r (koos vabaliikmega), nt \ln x + 1 = 0 ⇒ x = e^{-1}.
    private static VorrandiViis? Isoleeri(SymbolicExpression g)
    {
        if (g.Expression is not Avaldis.Sum s || s.Item.Length != 2) return null;
        var vaba = s.Item.FirstOrDefault(t => !Latex.SisaldabX(t));
        var liige = s.Item.FirstOrDefault(Latex.SisaldabX);
        if (vaba is null || liige is null) return null;

        double k = 1;
        var funktsioon = liige;
        if (liige is Avaldis.Product p && p.Item.Length == 2 && !Latex.SisaldabX(p.Item.Head))
        {
            k = Latex.Vaartus(p.Item.Head);
            funktsioon = p.Item.Tail.Head;
        }
        bool ln = funktsioon is Avaldis.Function { Item1.IsLn: true } fl && fl.Item2.Equals(X.Expression);
        bool exp = funktsioon is Avaldis.Function { Item1.IsExp: true } fe && fe.Item2.Equals(X.Expression)
            || funktsioon is Avaldis.Power { Item1: Avaldis.Constant { Item.IsE: true } } pe && pe.Item2.Equals(X.Expression);
        if (!(ln || exp) || Murd.FromDouble(-Latex.Vaartus(vaba) / k) is not { } r) return null;

        string f = ln ? "\\ln x" : "e^{x}";
        var sammud = new List<LahenduseSamm>
        {
            new("Jätame funktsiooni ühele poole ja arvud teisele poole:", $"{f} = {r.Latex()}"),
        };
        if (ln)
        {
            sammud.Add(new("Logaritmi definitsiooni järgi: $\\ln x = r \\Leftrightarrow x = e^r$.", $"x = e^{{{r.Latex()}}}"));
            string l = r.OnNull ? "1" : r.P == 1 && r.Q == 1 ? "e" : $"e^{{{r.Latex()}}}";
            return new("Logaritmi definitsioon", sammud, [new(Math.Exp(r.V), l)]);
        }
        if (r.Mark <= 0)
        {
            sammud.Add(new("$e^x > 0$ iga $x$ korral, seega lahendid puuduvad."));
            return new("Eksponendi omadus", sammud, []);
        }
        string lx = r.P == 1 && r.Q == 1 ? "0" : $"\\ln {r.Latex()}";
        sammud.Add(new("Logaritmime mõlemad pooled: $e^x = r \\Leftrightarrow x = \\ln r$.", $"x = {lx}"));
        return new("Logaritmimine", sammud, [new(Math.Log(r.V), lx)]);
    }

    // Kas avaldis on ainult e astmete korrutis (siis seda murru nimetajana ei käsitleta: e^{-x} pole murd).
    private static bool AinultEksponent(Avaldis a) => a switch
    {
        _ when !Latex.SisaldabX(a) => true,
        Avaldis.Product p => p.Item.All(AinultEksponent),
        _ => OnEksponent(a, out _),
    };

    private static (SymbolicExpression Lugeja, SymbolicExpression Nimetaja)? Murruks(SymbolicExpression g)
    {
        foreach (var kuju in new Func<SymbolicExpression>[] { () => g, () => g.RationalSimplify(X) })
        {
            try
            {
                var s = kuju();
                var nimetaja = s.Denominator();
                if (Latex.SisaldabX(nimetaja.Expression) && !AinultEksponent(nimetaja.Expression))
                    return (s.Numerator(), nimetaja);
            }
            catch (Exception) { }
        }
        return null;
    }

    private static VorrandiViis Murrust(SymbolicExpression lugeja, SymbolicExpression nimetaja, VorrandiViis v)
    {
        var sammud = new List<LahenduseSamm>
        {
            new("Murd on null, kui lugeja on null (nimetaja ei tohi olla null):", $"{Latex.Vorminda(lugeja)} = 0"),
        };
        sammud.AddRange(v.Sammud);
        var nimetajaF = Kompileeri(nimetaja);
        var sobivad = new List<Juur>();
        foreach (var j in v.Juured)
        {
            if (Math.Abs(nimetajaF(j.V)) < 1e-12)
                sammud.Add(new($"${j.L}$ ei sobi, sest siis on nimetaja ${Latex.Vorminda(nimetaja)}$ null."));
            else
                sobivad.Add(j);
        }
        return v with { Sammud = sammud, Juured = sobivad };
    }

    private static List<VorrandiViis> Korrutis(List<SymbolicExpression> tegurid, int sygavus)
    {
        if (tegurid.Count == 0) return [];
        if (tegurid.Count == 1) return Lahenda(tegurid[0], sygavus + 1);

        var sammud = new List<LahenduseSamm>
        {
            new("Korrutis on null, kui vähemalt üks tegur on null:",
                string.Join(" \\quad \\text{või} \\quad ", tegurid.Select(t => Latex.Vorminda(t) + " = 0"))),
        };
        var juured = new List<Juur>();
        foreach (var t in tegurid)
        {
            var alam = Lahenda(t, sygavus + 1);
            if (alam.Count == 0) return [];
            sammud.Add(new($"Tegur ${Latex.Vorminda(t)} = 0$:"));
            sammud.AddRange(alam[0].Sammud);
            juured.AddRange(alam[0].Juured);
        }
        return [new("Korrutis võrdub nulliga", sammud, Yhenda(juured))];
    }

    public static (SymbolicExpression Tegur, SymbolicExpression Sulus)? UhineTegur(SymbolicExpression g)
    {
        if (g.Expression is not Avaldis.Sum s) return null;
        var liikmed = s.Item.ToList();
        static IEnumerable<Avaldis> Tegurid(Avaldis a) => a is Avaldis.Product p ? p.Item : [a];
        foreach (var kandidaat in Tegurid(liikmed[0]).Where(t => Latex.SisaldabX(t) && t is not Avaldis.Identifier))
        {
            if (!liikmed.All(l => Tegurid(l).Any(t => t.Equals(kandidaat))))
                continue;
            var tegur = new SymbolicExpression(kandidaat);
            var sulus = liikmed.Select(l => new SymbolicExpression(l) / tegur).Aggregate((a, b) => a + b);
            try { sulus = sulus.Expand(); } catch (Exception) { }   // -(1 - x) - 1 → x - 2
            return (tegur, sulus);
        }
        return null;
    }

    private static bool OnEksponent(Avaldis a, out Avaldis? astendaja)
    {
        astendaja = a switch
        {
            Avaldis.Function f when f.Item1.IsExp => f.Item2,
            Avaldis.Power p when p.Item1 is Avaldis.Constant c && c.Item.IsE => p.Item2,
            Avaldis.Power p when !Latex.SisaldabX(p.Item1) && Latex.Vaartus(p.Item1) > 0 && Latex.SisaldabX(p.Item2) => p.Item2,
            _ => null,
        };
        return astendaja is not null;
    }

    // sin(kx + b) = 0, cos(kx + b) = 0, tan(kx + b) = 0: üldlahend.
    private static VorrandiViis Trigonomeetriline(Avaldis.Function f, Murd k, Murd b)
    {
        string u = Polynoom([b, k]);
        bool cos = f.Item1.IsCos;
        string nimi = f.Item1.IsSin ? "Siinus" : cos ? "Koosinus" : "Tangens";
        string nurk = cos ? "\\frac{\\pi}{2} + n\\pi" : "n\\pi";
        string lugeja = b.OnNull ? nurk : $"{nurk} {(b.Mark > 0 ? "-" : "+")} {(b.Mark > 0 ? b : -b).Latex()}";
        string uld = k.P == 1 && k.Q == 1 ? lugeja : $"\\frac{{{lugeja}}}{{{k.Latex()}}}";
        if (k.P == 1 && k.Q == 1 && b.OnNull) uld = nurk;

        var sammud = new List<LahenduseSamm>
        {
            new($"{nimi} on null, kui nurk on {(cos ? "$\\frac{\\pi}{2}$ pluss $\\pi$ kordne" : "$\\pi$ kordne")}:", $"{u} = {nurk}, \\quad n \\in \\mathbb{{Z}}"),
        };
        if (u != "x")
            sammud.Add(new("Avaldame $x$:", $"x = {uld}"));

        var juured = new List<Juur>();
        double nihe = cos ? Math.PI / 2 : 0;
        for (int n = -200; n <= 200; n++)
        {
            double x = (nihe + n * Math.PI - b.V) / k.V;
            if (Math.Abs(x) <= FunktsiooniAnalyysija.VahemikuPiir)
                juured.Add(new(x, Latex.Arv(x)));
        }
        return new("Üldlahend", sammud, juured, $"x = {uld}, \\quad n \\in \\mathbb{{Z}}");
    }

    public static Func<double, double> Kompileeri(SymbolicExpression a)
    {
        try
        {
            var f = a.Compile("x");
            return x => { try { return f(x); } catch (Exception) { return double.NaN; } };
        }
        catch (Exception)
        {
            return x =>
            {
                try
                {
                    var t = a.Evaluate(new Dictionary<string, FloatingPoint> { ["x"] = FloatingPoint.NewReal(x) });
                    return t.IsReal ? t.RealValue : double.NaN;
                }
                catch (Exception) { return double.NaN; }
            };
        }
    }
}
