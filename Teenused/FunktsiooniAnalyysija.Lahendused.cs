using FunktsioonideUurimine.Mudelid;
using MathNet.Symbolics;
using Avaldis = MathNet.Symbolics.Expression;

namespace FunktsioonideUurimine.Teenused;

// Lahenduskäigud kõigile uurimisküsimustele (nagu Photomathis): lihtsate sammudena ja võimaluse korral mitmel viisil.
// Vastused ise arvutab Analyysi; siin selgitatakse, kuidas nendeni jõuda (täpsed lahendid, märgitabelid, piirväärtused).
public partial class FunktsiooniAnalyysija
{
    public Dictionary<string, Lahendus> Lahendused(AnalyysiTulemus tulemus) =>
        tulemus.Andmed is null ? [] : new Koostaja(tulemus).Koosta();

    private sealed class Koostaja
    {
        private static readonly SymbolicExpression XM = SymbolicExpression.Variable("x");
        private const double H = 1e-4;
        private const int MaxPunkte = 8;

        private readonly AnalyysiTulemus T;
        private readonly Vaheandmed A;
        private readonly KoostatudFunktsioon F;
        private readonly SymbolicExpression f;
        private SymbolicExpression df, d2f;
        private List<LahenduseViis> tuletiseViisid = [], teiseTuletiseViisid = [];
        private readonly List<(double V, string L)> nimed = [];   // täpsed kujud, nt 1.732… → \sqrt{3}

        public Koostaja(AnalyysiTulemus t)
        {
            T = t;
            A = t.Andmed!;
            F = t.Funktsioon;
            f = F.Avaldis;
            df = f.Differentiate(XM);
            d2f = df.Differentiate(XM);
            var trig = new List<Avaldis.Function>();
            KoguTrig(f.Expression, trig);
            onTrig = trig.Count > 0;
        }

        private readonly bool onTrig;

        public Dictionary<string, Lahendus> Koosta()
        {
            var tulemus = new Dictionary<string, Lahendus>();
            void Lisa(string voti, Func<List<LahenduseViis>> koosta)
            {
                try
                {
                    var viisid = koosta();
                    if (viisid.Count > 0) tulemus[voti] = new Lahendus(viisid);
                }
                catch (Exception)
                {
                    // Selle küsimuse lahenduskäiku ei õnnestunud koostada – vastus jääb ilma selgituseta.
                }
            }

            LeiaTuletised();
            // Võrrandid enne märgitabeleid, et tabelites oleksid täpsed lahendid (nt -\sqrt{3}).
            Lisa("nullkohad", Nullkohad);
            Lisa("kriitilisedPunktid", KriitilisedPunktid);
            Lisa("kaanupunktid", Kaanupunktid);
            Lisa("liik", Liik);
            Lisa("maaramispiirkond", Maaramispiirkond);
            Lisa("muutumispiirkond", Muutumispiirkond);
            Lisa("positiivsuspiirkond", () => Margipiirkond(+1));
            Lisa("negatiivsuspiirkond", () => Margipiirkond(-1));
            Lisa("paarsus", Paarsus);
            Lisa("perioodilisus", Perioodilisus);
            Lisa("yTeljeLoikepunkt", YLoikepunkt);
            Lisa("asumptoodid", Asumptoodid);
            Lisa("tuletis", () => tuletiseViisid);
            Lisa("kasvamisvahemikud", () => Monotoonsus(+1));
            Lisa("kahanemisvahemikud", () => Monotoonsus(-1));
            Lisa("ekstreemumid", Ekstreemumid);
            Lisa("teineTuletis", () => teiseTuletiseViisid);
            Lisa("kumerusvahemikud", () => Kumerus(-1));
            Lisa("nogususvahemikud", () => Kumerus(+1));
            return tulemus;
        }

        // ---------- Tuletised ----------

        private void LeiaTuletised()
        {
            if (Tuletised.Selgita(f, 1) is { } t1)
            {
                df = t1.Tuletis;
                tuletiseViisid = t1.Viisid;
            }
            else
            {
                tuletiseViisid = [new("Tuletis", [new("Tuletise leiame reeglite abil:", $"f'(x) = {L(df)}")])];
            }

            if (Tuletised.Selgita(df, 2) is { } t2)
            {
                d2f = t2.Tuletis;
                teiseTuletiseViisid = t2.Viisid;
            }
            else
            {
                d2f = df.Differentiate(XM);
                teiseTuletiseViisid = [new("Teine tuletis", [new("Teine tuletis on tuletise tuletis:", $"f''(x) = {L(d2f)}")])];
            }
        }

        // ---------- Võrrandid ----------

        private List<LahenduseViis> Vorrand(SymbolicExpression g, string gNimi, List<double> numbrilised, Func<double, bool> sobib,
            List<LahenduseSamm> eel)
        {
            var algus = new List<LahenduseSamm>(eel) { new($"Lahendame võrrandi ${gNimi} = 0$:", $"{L(g)} = 0") };
            var viisid = new List<LahenduseViis>();
            List<VorrandiViis> tapsed;
            try { tapsed = Vorrandid.Lahenda(g); }
            catch (Exception) { tapsed = []; }

            foreach (var v in tapsed)
            {
                var juured = v.Juured.Where(j => sobib(j.V)).ToList();
                if (v.Uldlahend is null)
                {
                    if (!Kattub(juured, numbrilised)) continue;   // kontroll: täpne lahend peab klappima numbrilisega
                    juured.ForEach(Registreeri);
                }
                viisid.Add(new(v.Nimi, [.. algus, .. v.Sammud, VastusLahendid(juured, v.Uldlahend)]));
            }

            if (viisid.Count == 0)
            {
                viisid.Add(new("Numbriliselt", [
                    .. algus,
                    new("Seda võrrandit ei saa lihtsate võtetega täpselt lahendada, seega leiame lahendid ligikaudselt."),
                    new($"Arvutame ${gNimi}$ väärtusi tihedalt (sammuga $0.01$). Kus märk muutub, seal on lahend – täpsustame selle lõiku poolitades (poolitusmeetod)."),
                    VastusLahendid(numbrilised.Select(x => new Juur(x, N(x))).ToList(), null),
                ]));
            }
            return viisid;
        }

        private static bool Kattub(List<Juur> juured, List<double> numbrilised)
        {
            var vahemikus = juured.Where(j => Math.Abs(j.V) <= VahemikuPiir + 1e-9).Select(j => j.V).Order().ToList();
            if (vahemikus.Count != numbrilised.Count) return false;
            return vahemikus.Zip(numbrilised.Order()).All(p => Math.Abs(p.First - p.Second) < 1e-6 * (1 + Math.Abs(p.First)));
        }

        private void Registreeri(Juur j)
        {
            if (j.OnTapne && Murd.FromDouble(j.V, 100) is null && !nimed.Any(n => Math.Abs(n.V - j.V) < 1e-9))
                nimed.Add((j.V, j.L));
        }

        private static LahenduseSamm VastusLahendid(List<Juur> juured, string? uldlahend)
        {
            if (uldlahend is not null) return new("Vastus:", uldlahend);
            if (juured.Count == 0) return new("Vastus: lahendid puuduvad.");
            if (juured.Count == 1) return new("Vastus:", $"x = {juured[0].Tekst}");
            var naidatud = juured.Count <= 10 ? juured : juured.OrderBy(j => Math.Abs(j.V)).Take(10).OrderBy(j => j.V).ToList();
            return new("Vastus:", string.Join(", \\quad ", naidatud.Select((j, i) => $"x_{{{i + 1}}} = {j.Tekst}"))
                + (naidatud.Count < juured.Count ? ", \\dots" : ""));
        }

        // ---------- Küsimused ----------

        private List<LahenduseViis> Liik()
        {
            string nimi = FunktsiooniLiigid.Nimi(F.Liik);
            var sammud = new List<LahenduseSamm>();
            switch (F.Liik)
            {
                case FunktsiooniLiigid.Konstantne:
                    sammud.Add(new("Valemis pole muutujat $x$, seega on funktsiooni väärtus alati sama."));
                    break;
                case FunktsiooniLiigid.Lineaar or FunktsiooniLiigid.Ruut or FunktsiooniLiigid.Kuup or FunktsiooniLiigid.Polynoom:
                    int aste = f.Coefficients(XM).Length - 1;
                    sammud.Add(new("Valem on polünoom: liidetakse arvu ja $x$ naturaalarvuliste astmete korrutisi."));
                    sammud.Add(new($"Kõrgeim aste on $x^{{{aste}}}$, seega on polünoomi aste ${aste}$."));
                    sammud.Add(new(aste switch
                    {
                        1 => "Esimese astme polünoom on lineaarfunktsioon ($y = ax + b$), graafik on sirge.",
                        2 => "Teise astme polünoom on ruutfunktsioon ($y = ax^2 + bx + c$), graafik on parabool.",
                        3 => "Kolmanda astme polünoom on kuupfunktsioon ($y = ax^3 + bx^2 + cx + d$).",
                        _ => "Neljanda või kõrgema astme polünoom on polünoomfunktsioon.",
                    }));
                    break;
                case FunktsiooniLiigid.Ratsionaal:
                    sammud.Add(new("Muutuja $x$ on murru nimetajas, seega on funktsioon kahe polünoomi jagatis ehk ratsionaalfunktsioon."));
                    break;
                case FunktsiooniLiigid.Juur:
                    sammud.Add(new("Muutuja $x$ on juure all (ehk murdastmes), seega on see juurfunktsioon."));
                    break;
                case FunktsiooniLiigid.Eksponent:
                    sammud.Add(new("Muutuja $x$ on astendajas (nt $e^x$), seega on see eksponentfunktsioon."));
                    break;
                case FunktsiooniLiigid.Logaritm:
                    sammud.Add(new("Valemis on logaritm ($\\ln$) muutujast $x$, seega on see logaritmfunktsioon."));
                    break;
                case FunktsiooniLiigid.Trigonomeetriline:
                    sammud.Add(new("Valemis on $\\sin$, $\\cos$ või $\\tan$ muutujast $x$, seega on see trigonomeetriline funktsioon."));
                    break;
                default:
                    sammud.Add(new("Valemis on mitut eri liiki osi (nt eksponent ja trigonomeetriline funktsioon või polünoom ja juur), seega on see liitfunktsioon."));
                    break;
            }
            sammud.Add(new($"Vastus: {nimi}."));
            return [new("Valemi kuju järgi", sammud)];
        }

        private List<LahenduseViis> Maaramispiirkond()
        {
            var tingimused = new List<(string Tyyp, SymbolicExpression U)>();
            KoguTingimused(f.Expression, tingimused);
            tingimused = tingimused.DistinctBy(t => (t.Tyyp, t.U.ToString())).ToList();

            var sammud = new List<LahenduseSamm>();
            if (tingimused.Count == 0)
            {
                sammud.Add(new("Valemis pole murdu, mille nimetajas on $x$, paarisarvulist juurt ega logaritmi. Seega saab $x$ olla iga reaalarv."));
            }
            else
            {
                sammud.Add(new("Leiame, millised $x$ väärtused ei sobi. Kontrollime iga piirangut:"));
                foreach (var (tyyp, u) in tingimused)
                    sammud.AddRange(Tingimus(tyyp, u));
                sammud.Add(new("Määramispiirkond on kõigi tingimuste ühisosa."));
            }
            sammud.Add(VastusHulk("X", A.Piirkond, T.Maaramispiirkond, punktideKuju: true));
            return [new("Piirangud", sammud)];
        }

        private static void KoguTingimused(Avaldis a, List<(string, SymbolicExpression)> t)
        {
            switch (a)
            {
                case Avaldis.Power p:
                    if (Latex.SisaldabX(p.Item1) && !Latex.SisaldabX(p.Item2) && Murd.FromDouble(Latex.Vaartus(p.Item2)) is { } e)
                    {
                        bool paarisJuur = e.Q % 2 == 0;
                        if (e.Mark < 0) t.Add((paarisJuur ? ">0" : "!=0", new SymbolicExpression(p.Item1)));
                        else if (paarisJuur) t.Add((">=0", new SymbolicExpression(p.Item1)));
                    }
                    KoguTingimused(p.Item1, t);
                    KoguTingimused(p.Item2, t);
                    break;
                case Avaldis.Function fn:
                    if ((fn.Item1.IsLn || fn.Item1.IsLg) && Latex.SisaldabX(fn.Item2)) t.Add((">0ln", new SymbolicExpression(fn.Item2)));
                    if (fn.Item1.IsTan && Latex.SisaldabX(fn.Item2)) t.Add(("tan", new SymbolicExpression(fn.Item2)));
                    KoguTingimused(fn.Item2, t);
                    break;
                case Avaldis.Sum s:
                    foreach (var o in s.Item) KoguTingimused(o, t);
                    break;
                case Avaldis.Product k:
                    foreach (var o in k.Item) KoguTingimused(o, t);
                    break;
            }
        }

        private IEnumerable<LahenduseSamm> Tingimus(string tyyp, SymbolicExpression u)
        {
            string ul = L(u);
            if (tyyp == "tan")
            {
                yield return new($"Tangens pole määratud, kui ${ul} = \\frac{{\\pi}}{{2}} + n\\pi$ (seal on $\\cos = 0$). Need kohad jäävad välja.");
                yield break;
            }

            var juured = LahendaLihtsalt(u);
            string vorrand = juured.Count == 0 ? "" : $"{ul} = 0 \\Rightarrow " + string.Join(", \\ ", juured.Select(j => $"x = {j.L}"));
            if (tyyp == "!=0")
            {
                yield return new($"Nimetaja ei tohi olla null: ${ul} \\neq 0$.", juured.Count == 0 ? null : vorrand);
                yield return new(juured.Count == 0
                    ? $"${ul}$ ei ole kunagi null, seega piirangut pole."
                    : "Seega " + string.Join(", ", juured.Select(j => $"$x \\neq {j.L}$")) + ".");
                yield break;
            }

            bool vordne = tyyp == ">=0";
            string mark = vordne ? "\\ge" : ">";
            if (u.Expression.Equals(XM.Expression))
            {
                yield return new(tyyp == ">0ln"
                    ? $"Logaritmitav peab olema positiivne: $x > 0$."
                    : vordne ? "Paarisjuure all olev avaldis ei tohi olla negatiivne: $x \\ge 0$."
                    : "Avaldis on nii juure all kui ka nimetajas, seega peab see olema positiivne: $x > 0$.");
                yield break;
            }
            yield return new(tyyp == ">0ln"
                ? $"Logaritmitav peab olema positiivne: ${ul} > 0$."
                : vordne ? $"Paarisjuure all olev avaldis ei tohi olla negatiivne: ${ul} \\ge 0$."
                : $"Avaldis on nii juure all kui ka nimetajas, seega peab see olema positiivne: ${ul} > 0$.",
                juured.Count == 0 ? null : vorrand);

            var uF = Vorrandid.Kompileeri(u);
            var alus = new List<Intervall> { new(double.NegativeInfinity, false, double.PositiveInfinity, false) };
            var tabel = Margitabel(uF, ul, juured.Select(j => j.V), s => s > 0 ? "sobib" : "ei sobi", alus, sulgudes: true);
            yield return tabel.Samm;
            var hulk = MargiVahemikud(uF, alus, juured.Select(j => j.V).ToList(), +1, kinnisedOtsad: false, yhenda: false);
            if (vordne) hulk = SuleNullkohad(hulk, juured.Select(j => j.V));
            yield return new($"Seega ${ul} {mark} 0$, kui $x \\in {HulkL(hulk, false) ?? "\\dots"}$.");
        }

        // Võrratuse ≥ 0 lahend: nullkohad kuuluvad hulka (vahemikud kinnised, kokku puutuvad liidetakse).
        private static List<Intervall> SuleNullkohad(List<Intervall> hulk, IEnumerable<double> nullkohad)
        {
            var tulemus = new List<Intervall>();
            foreach (var iv in hulk)
            {
                var uus = iv with { VasakKinnine = double.IsFinite(iv.Vasak), ParemKinnine = double.IsFinite(iv.Parem) };
                if (tulemus.Count > 0 && tulemus[^1].Parem == uus.Vasak)
                    tulemus[^1] = tulemus[^1] with { Parem = uus.Parem, ParemKinnine = uus.ParemKinnine };
                else
                    tulemus.Add(uus);
            }
            foreach (var x in nullkohad)
            {
                if (!tulemus.Any(iv => iv.Vasak <= x && x <= iv.Parem))
                    tulemus.Add(new Intervall(x, true, x, true));
            }
            return tulemus.OrderBy(iv => iv.Vasak).ToList();
        }

        private List<Juur> LahendaLihtsalt(SymbolicExpression u)
        {
            var uF = Vorrandid.Kompileeri(u);
            var numbrilised = NumbrilisedNullkohad(uF);
            try
            {
                foreach (var v in Vorrandid.Lahenda(u))
                {
                    if (v.Uldlahend is null && Kattub(v.Juured, numbrilised))
                    {
                        v.Juured.ForEach(Registreeri);
                        return v.Juured;
                    }
                }
            }
            catch (Exception) { }
            return numbrilised.Select(x => new Juur(x, N(x))).ToList();
        }

        private static List<double> NumbrilisedNullkohad(Func<double, double> g)
        {
            var gv = new double[Samme + 1];
            var kasutatav = new bool[Samme + 1];
            for (int i = 0; i <= Samme; i++)
            {
                gv[i] = g(X(i));
                kasutatav[i] = double.IsFinite(gv[i]);
            }
            return LeiaNullkohad(g, gv, kasutatav).Kohad;
        }

        private List<LahenduseViis> Muutumispiirkond()
        {
            if (A.DfOnNull)
                return [new("Konstantne funktsioon", [new($"Funktsioon on konstantne, seega on tal ainult üks väärtus."), new($"Vastus: $Y = {T.Muutumispiirkond.Replace("{", "\\{").Replace("}", "\\}")}$.")])];

            var viisid = new List<LahenduseViis>();
            var sammud = new List<LahenduseSamm> { new("Uurime, milliseid väärtusi $f(x)$ saavutab: vaatame määramispiirkonna otsi ja ekstreemumeid.") };

            var otsad = new List<string>();
            foreach (var iv in A.Piirkond.Take(4))
            {
                otsad.Add(Ots(iv.Vasak, iv.VasakKinnine, +1));
                otsad.Add(Ots(iv.Parem, iv.ParemKinnine, -1));
            }
            sammud.Add(new("Määramispiirkonna otstes:", string.Join(", \\quad ", otsad)));

            var ekstreemumid = T.EkstreemumidePunktid.OrderBy(e => Math.Abs(e.X)).Take(MaxPunkte).OrderBy(e => e.X).ToList();
            if (ekstreemumid.Count > 0)
            {
                sammud.Add(new("Ekstreemumites:", string.Join(", \\quad ", ekstreemumid.Select(e =>
                    $"{(e.OnMaksimum ? "y_{max}" : "y_{min}")} = f({N(e.X)}) {Vord(e.Y)}"))));
            }
            else
            {
                sammud.Add(new("Ekstreemumeid pole – funktsioon ainult kasvab või ainult kahaneb (igas määramispiirkonna osas)."));
            }
            sammud.Add(new("Väikseim ja suurim väärtus (või piirväärtus) annavad muutumispiirkonna. Kui piirväärtust ei saavutata, on vahemik lahtine."));
            sammud.Add(new($"Vastus: Y = {T.Muutumispiirkond}"));
            viisid.Add(new("Piirväärtused ja ekstreemumid", sammud));

            if (Vorrandid.Kordajad(f) is { Length: >= 2 } c)
            {
                int n = c.Length - 1;
                var juht = c[^1];
                var reegel = new List<LahenduseSamm> { new($"Polünoomi aste on ${n}$ ja kõrgeima astme kordaja on ${juht.Latex()}$.") };
                if (n % 2 == 1)
                {
                    reegel.Add(new("Paaritu astmega polünoom läheb ühes suunas $-\\infty$ ja teises $+\\infty$ ning on pidev, seega saavutab ta kõik väärtused."));
                }
                else
                {
                    var aar = juht.Mark > 0
                        ? T.EkstreemumidePunktid.Where(e => !e.OnMaksimum).MinBy(e => e.Y)
                        : T.EkstreemumidePunktid.Where(e => e.OnMaksimum).MaxBy(e => e.Y);
                    reegel.Add(new(juht.Mark > 0
                        ? "Paarisastmega polünoom positiivse kordajaga läheb mõlemas suunas $+\\infty$, seega on tal vähim väärtus – see on väikseim miinimum."
                        : "Paarisastmega polünoom negatiivse kordajaga läheb mõlemas suunas $-\\infty$, seega on tal suurim väärtus – see on suurim maksimum."));
                    if (aar is not null)
                        reegel.Add(new("", $"{(juht.Mark > 0 ? "y_{min}" : "y_{max}")} = f({N(aar.X)}) {Vord(aar.Y)}"));
                }
                reegel.Add(new($"Vastus: Y = {T.Muutumispiirkond}"));
                viisid.Add(new("Polünoomi astme järgi", reegel));
            }
            return viisid;
        }

        private string Ots(double ots, bool kinnine, int suund)
        {
            if (double.IsInfinity(ots))
            {
                string kuhu = ots < 0 ? "-\\infty" : "\\infty";
                return $"\\lim_{{x \\to {kuhu}}} f(x) = {PiirL(PiirLopmatuses(F.F, Math.Sign(ots)))}";
            }
            if (kinnine)
                return $"f({N(ots)}) {Vord(F.F(ots))}";
            return $"\\lim_{{x \\to {N(ots)}^{(suund > 0 ? "+" : "-")}}} f(x) = {PiirL(YhepoolnePiir(F.F, ots, suund))}";
        }

        private List<LahenduseViis> Nullkohad()
        {
            if (A.FOnNull)
                return [new("Nullfunktsioon", [new("$f(x) = 0$ iga $x$ korral, seega on iga määramispiirkonna punkt nullkoht.")])];

            var viisid = Vorrand(f, "f(x)", A.Nullkohad, x => double.IsFinite(F.F(x)),
                [new("Nullkohad on kohad, kus funktsiooni väärtus on null ehk graafik lõikab $x$-telge.")]);

            var graafik = new List<LahenduseSamm>
            {
                new("Nullkohad on kohad, kus graafik lõikab $x$-telge (seal $y = 0$).", Joonis: Joonis(A.Nullkohad)),
            };
            graafik.Add(A.Nullkohad.Count == 0
                ? new("Graafik ei lõika $x$-telge, seega nullkohti pole.")
                : new("Graafikult (punktid $x$-teljel) loeme:", string.Join(", \\quad ", A.Nullkohad.OrderBy(Math.Abs).Take(10).Order().Select(x =>
                    Murd.FromDouble(x, 100) is { } m ? $"x = {m.Latex()}" : $"x \\approx {Latex.Arv(x)}"))
                    + (A.Nullkohad.Count > 10 ? ", \\dots" : "")));
            graafik.Add(new("Graafikult loetud väärtus võib olla ligikaudne – täpse vastuse annab võrrandi lahendamine."));
            viisid.Add(new("Graafikult", graafik));
            return viisid;
        }

        private List<LahenduseViis> Margipiirkond(int mark)
        {
            if (A.FOnNull)
                return [new("Nullfunktsioon", [new("$f(x) = 0$ kõikjal, seega pole see kusagil positiivne ega negatiivne.")])];

            string vorratus = mark > 0 ? "f(x) > 0" : "f(x) < 0";
            string nimi = mark > 0 ? "X^{+}" : "X^{-}";
            var hulk = MargiVahemikud(F.F, A.Piirkond, A.Nullkohad, mark, kinnisedOtsad: true, yhenda: false);
            var sammud = new List<LahenduseSamm>
            {
                new($"Otsime, kus ${vorratus}$. Kasutame intervallmeetodit: nullkohad jagavad $x$-telje vahemikeks ja igas vahemikus on märk sama."),
                new(A.Nullkohad.Count == 0 ? "Nullkohti pole." : "Nullkohad:", A.Nullkohad.Count == 0 ? null : KohadL(A.Nullkohad)),
                Margitabel(F.F, "f", A.Nullkohad, s => s > 0 ? "$f(x) > 0$" : s < 0 ? "$f(x) < 0$" : "$0$").Samm,
                VastusHulk(nimi, hulk, mark > 0 ? T.Positiivsuspiirkond : T.Negatiivsuspiirkond, punktideKuju: true),
            };
            var graafik = new List<LahenduseSamm>
            {
                new(mark > 0
                    ? "$f(x) > 0$ seal, kus graafik on $x$-teljest kõrgemal (joonisel roheline ala)."
                    : "$f(x) < 0$ seal, kus graafik on $x$-teljest madalamal (joonisel punane ala).",
                    Joonis: Joonis(A.Nullkohad, mark)),
                new("Graafiku ja $x$-telje lõikepunktid (nullkohad) on vahemike piirid; nullkohad ise ei kuulu vastusesse, sest seal $f(x) = 0$."),
                VastusHulk(nimi, hulk, mark > 0 ? T.Positiivsuspiirkond : T.Negatiivsuspiirkond, punktideKuju: true),
            };
            return [new("Intervallmeetod", sammud), new("Graafikult", graafik)];
        }

        private List<LahenduseViis> Paarsus()
        {
            var viisid = new List<LahenduseViis>();
            if (T.Paarsus.Contains("määramispiirkond ei ole sümmeetriline"))
            {
                viisid.Add(new("Määramispiirkond", [
                    new($"Paaris- või paaritu saab olla ainult funktsioon, mille määramispiirkond on nullpunkti suhtes sümmeetriline (koos $x$-iga kuulub sinna ka $-x$)."),
                    VastusHulk("X", A.Piirkond, T.Maaramispiirkond, punktideKuju: true),
                    new("See ei ole sümmeetriline, seega funktsioon ei ole paaris ega paaritu."),
                ]));
                return viisid;
            }

            bool paaris = T.Paarsus.StartsWith("paarisfunktsioon") || T.Paarsus.StartsWith("nii paaris");
            bool paaritu = T.Paarsus.StartsWith("paaritu") || T.Paarsus.StartsWith("nii paaris");
            var fm = f.Substitute(XM, -XM);
            var miinusF = -f;
            if (Vorrandid.Kordajad(f) is not null)
            {
                fm = fm.Expand();
                miinusF = miinusF.Expand();
            }

            var sammud = new List<LahenduseSamm>
            {
                new("Paarsuse kontrollimiseks asendame valemis $x$ asemel $-x$:", $"f(-x) = {Latex.Vorminda(f, "-x")} = {L(fm)}"),
            };
            if (paaris)
                sammud.Add(new($"Saime sama avaldise mis $f(x)$, seega $f(-x) = f(x)$."));
            else if (paaritu)
                sammud.Add(new($"Saime vastandavaldise: $-f(x) = {L(miinusF)}$, seega $f(-x) = -f(x)$."));
            else
                sammud.Add(new($"Võrdleme: $f(x) = {L(f)}$ ja $-f(x) = {L(miinusF)}$. Kumbki ei võrdu avaldisega $f(-x)$."));
            sammud.Add(new($"Vastus: {T.Paarsus}."));
            viisid.Add(new("Asendame $-x$", sammud));

            if (Vorrandid.Kordajad(f) is { } c)
            {
                var astmed = Enumerable.Range(0, c.Length).Where(i => !c[i].OnNull).ToList();
                string loetelu = string.Join(", ", astmed.Select(i => i switch { 0 => "vabaliige ($x^0$)", 1 => "$x$ ($x^1$)", _ => $"$x^{{{i}}}$" }));
                bool koikPaaris = astmed.All(i => i % 2 == 0), koikPaaritud = astmed.All(i => i % 2 == 1);
                viisid.Add(new("Astmete järgi", [
                    new($"Polünoomi korral piisab astmetest. Valemis on: {loetelu}."),
                    new("Kui kõik astmed on paarisarvud, on polünoom paaris; kui kõik on paaritud, on paaritu; segamini – ei kumbki."),
                    new(koikPaaris ? "Kõik astmed on paarisarvud ⇒ paarisfunktsioon."
                        : koikPaaritud ? "Kõik astmed on paaritud ⇒ paaritu funktsioon."
                        : "Paaris- ja paaritud astmed on segamini ⇒ ei ole paaris ega paaritu."),
                ]));
            }

            var naited = new[] { 1.0, 2.0 }.Where(x => double.IsFinite(F.F(x)) && double.IsFinite(F.F(-x))).ToList();
            if (naited.Count > 0)
            {
                viisid.Add(new("Kontroll arvudega", [
                    new("Arvutame mõne väärtuse ja võrdleme $f(-x)$ ja $f(x)$ (see ei tõesta, aga aitab kontrollida):",
                        string.Join(", \\quad ", naited.Select(x => $"f({N(x)}) {Vord(F.F(x))}, \\ f({N(-x)}) {Vord(F.F(-x))}"))),
                    new(paaris ? "Väärtused on võrdsed – sobib paarisfunktsiooniga."
                        : paaritu ? "Väärtused on vastandarvud – sobib paaritu funktsiooniga."
                        : "Väärtused pole võrdsed ega vastandarvud ⇒ ei ole paaris ega paaritu."),
                ]));
            }
            return viisid;
        }

        private List<LahenduseViis> Perioodilisus()
        {
            var trig = new List<Avaldis.Function>();
            KoguTrig(f.Expression, trig);
            if (A.DfOnNull)
                return [new("Konstantne", [new($"Konstantne funktsioon kordub iga sammu järel, seega iga $T > 0$ on periood (vähimat perioodi pole).")])];
            if (trig.Count == 0)
            {
                return [new("Valemi kuju järgi", [
                    new("Perioodiline funktsioon kordab oma väärtusi kindla sammu $T$ järel: $f(x + T) = f(x)$."),
                    new("Valemis pole trigonomeetrilisi funktsioone ($\\sin$, $\\cos$, $\\tan$). Polünoomid, murrud, juured, astmed ja logaritmid ei kordu."),
                    new("Vastus: ei ole perioodiline."),
                ])];
            }

            var sammud = new List<LahenduseSamm> { new("Perioodiline funktsioon kordab oma väärtusi kindla sammu $T$ järel: $f(x + T) = f(x)$.") };
            foreach (var t in trig.DistinctBy(t => t.ToString()).Take(4))
            {
                var u = new SymbolicExpression(t.Item2);
                string nimi = t.Item1.IsTan ? "\\tan" : t.Item1.IsSin ? "\\sin" : "\\cos";
                if (Vorrandid.Kordajad(u) is { Length: 2 } k && !k[1].OnNull)
                {
                    var kAbs = k[1].Mark < 0 ? -k[1] : k[1];
                    var tegur = (t.Item1.IsTan ? new Murd(1) : new Murd(2)) / kAbs;
                    sammud.Add(new($"${nimi} x$ periood on ${(t.Item1.IsTan ? "\\pi" : "2\\pi")}$, seega ${L(new SymbolicExpression(t))}$ periood on:",
                        $"T = \\frac{{{(t.Item1.IsTan ? "\\pi" : "2\\pi")}}}{{{kAbs.Latex()}}} = {PiKordne(tegur)}"));
                }
            }
            sammud.Add(new(A.Periood is { } p
                ? $"Kogu funktsiooni periood on vähim arv, mis sobib kõigile osadele: $T = {p.Tekst.Replace("π", "\\pi")}$."
                : "Valemis on ka mitteperioodiline osa (nt $x$ või $x^2$), mis väärtuste kordumist rikub."));
            sammud.Add(new($"Vastus: {T.Perioodilisus}."));
            return [new("Perioodide järgi", sammud)];
        }

        private static void KoguTrig(Avaldis a, List<Avaldis.Function> t)
        {
            switch (a)
            {
                case Avaldis.Function fn:
                    if (fn.Item1.IsSin || fn.Item1.IsCos || fn.Item1.IsTan) t.Add(fn);
                    KoguTrig(fn.Item2, t);
                    break;
                case Avaldis.Power p: KoguTrig(p.Item1, t); KoguTrig(p.Item2, t); break;
                case Avaldis.Sum s: foreach (var o in s.Item) KoguTrig(o, t); break;
                case Avaldis.Product k: foreach (var o in k.Item) KoguTrig(o, t); break;
            }
        }

        private static string PiKordne(Murd m)
        {
            string lugeja = m.P == 1 ? "\\pi" : $"{m.P}\\pi";
            return m.Q == 1 ? lugeja : $"\\frac{{{lugeja}}}{{{m.Q}}}";
        }

        private List<LahenduseViis> YLoikepunkt()
        {
            double y = F.F(0);
            if (!double.IsFinite(y))
                return [new("Asendame $x = 0$", [new("Lõikepunktis $y$-teljega on $x = 0$."), new("$x = 0$ ei kuulu määramispiirkonda, seega lõikepunkti pole.")])];

            var viisid = new List<LahenduseViis>
            {
                new("Asendame $x = 0$", [
                    new("Lõikepunktis $y$-teljega on $x = 0$. Arvutame $f(0)$:", $"f(0) = {Latex.Vorminda(f, "0")} {Vord(y)}"),
                    new("Vastus:", $"(0;\\ {VaartusL(y)})"),
                ]),
            };
            if (Vorrandid.Kordajad(f) is { Length: > 1 } c)
            {
                viisid.Add(new("Vabaliige", [
                    new($"Polünoomi korral on $f(0)$ võrdne vabaliikmega, sest kõik $x$-iga liikmed muutuvad nulliks."),
                    new($"Vabaliige on ${c[0].Latex()}$.", $"(0;\\ {c[0].Latex()})"),
                ]));
            }
            return viisid;
        }

        private List<LahenduseViis> Asumptoodid()
        {
            if (Vorrandid.Kordajad(f) is { } poly)
            {
                return [new("Polünoom", [
                    new("Polünoom on määratud kõikjal ja ei katke, seega vertikaalseid asümptoote pole."),
                    new(poly.Length - 1 <= 1
                        ? "Graafik on ise sirge, seega eraldi asümptooti pole."
                        : $"Kui $x \\to \\pm\\infty$, siis $f(x) \\to \\pm\\infty$ ja kasvab kiiremini kui ükski sirge (aste ${poly.Length - 1} \\ge 2$), seega horisontaal- ja kaldasümptoote pole."),
                    new($"Vastus: {T.Asumptoodid}."),
                ])];
            }

            var viisid = new List<LahenduseViis>();
            var sammud = new List<LahenduseSamm>();
            var vertikaalsed = T.AsumptootideAndmed.Where(a => a.OnVertikaalne).Select(a => a.X).ToList();

            sammud.Add(new("**Vertikaalne asümptoot** võib olla seal, kus funktsioon pole määratud (nt nimetaja on null) ja $f(x) \\to \\pm\\infty$."));
            if (vertikaalsed.Count == 0)
            {
                sammud.Add(new(A.Piirkond.All(iv => double.IsInfinity(iv.Vasak) && double.IsInfinity(iv.Parem))
                    ? "Funktsioon on määratud kõikjal, seega vertikaalseid asümptoote pole."
                    : "Määramispiirkonna otspunktides jääb $f(x)$ lõplikuks, seega vertikaalseid asümptoote pole."));
            }
            foreach (var a in vertikaalsed.OrderBy(Math.Abs).Take(4).Order())
            {
                var piirid = new List<string>();
                if (YhepoolnePiir(F.F, a, -1) is double v1) piirid.Add($"\\lim_{{x \\to {N(a)}^-}} f(x) = {PiirL(v1)}");
                if (YhepoolnePiir(F.F, a, +1) is double v2) piirid.Add($"\\lim_{{x \\to {N(a)}^+}} f(x) = {PiirL(v2)}");
                sammud.Add(new($"$x = {N(a)}$ ei kuulu määramispiirkonda. Ühepoolsed piirväärtused:", string.Join(", \\quad ", piirid)));
                sammud.Add(new($"Piirväärtus on lõpmatu, seega $x = {N(a)}$ on vertikaalne asümptoot."));
            }

            sammud.Add(new("**Horisontaalne asümptoot** $y = b$ on olemas, kui $f(x)$ läheneb arvule $b$, kui $x \\to \\pm\\infty$."));
            foreach (var suund in new[] { -1, +1 })
            {
                bool lopmatusse = suund < 0 ? double.IsNegativeInfinity(A.Piirkond[0].Vasak) : double.IsPositiveInfinity(A.Piirkond[^1].Parem);
                if (!lopmatusse) continue;
                string kuhu = suund < 0 ? "-\\infty" : "\\infty";
                var piir = PiirLopmatuses(F.F, suund);
                sammud.Add(new("", $"\\lim_{{x \\to {kuhu}}} f(x) = {PiirL(piir)}"));
                if (piir is double l && double.IsFinite(l))
                {
                    sammud.Add(new($"Piirväärtus on lõplik, seega $y {Vord(l)}$ on horisontaalne asümptoot (kui $x \\to {kuhu}$)."));
                }
                else if (piir is double lop && double.IsInfinity(lop))
                {
                    var k = PiirLopmatuses(x => F.F(x) / x, suund);
                    sammud.Add(new($"Horisontaalset asümptooti pole. Kontrollime **kaldasümptooti** $y = kx + b$:",
                        $"k = \\lim_{{x \\to {kuhu}}} \\frac{{f(x)}}{{x}} = {PiirL(k)}"));
                    if (k is double kk && double.IsFinite(kk) && kk != 0)
                    {
                        kk = Ilusta(kk, 1e-6);
                        var b = PiirLopmatuses(x => F.F(x) - kk * x, suund, maxAste: 8, tol: 1e-3);
                        sammud.Add(new("", $"b = \\lim_{{x \\to {kuhu}}} \\left(f(x) - {VaartusL(kk)}x\\right) = {PiirL(b)}"));
                    }
                    else
                    {
                        sammud.Add(new($"$k$ ei ole lõplik nullist erinev arv, seega kaldasümptooti pole."));
                    }
                }
                else
                {
                    sammud.Add(new("Piirväärtust pole (funktsioon võngub), seega asümptooti pole."));
                }
            }
            sammud.Add(new($"Vastus: {T.Asumptoodid}."));
            viisid.Add(new("Piirväärtuste abil", sammud));

            // Ratsionaalfunktsioon: lugeja ja nimetaja astmete võrdlus.
            try
            {
                var lugeja = f.Numerator();
                var nimetaja = f.Denominator();
                if (Latex.SisaldabX(nimetaja.Expression) && Vorrandid.Kordajad(lugeja) is { } p && Vorrandid.Kordajad(nimetaja) is { } q)
                    viisid.Add(Astmed(p, q));
            }
            catch (Exception) { }
            return viisid;
        }

        private LahenduseViis Astmed(Murd[] p, Murd[] q)
        {
            int n = p.Length - 1, m = q.Length - 1;
            string pl = Vorrandid.Polynoom(p), ql = Vorrandid.Polynoom(q);
            var sammud = new List<LahenduseSamm>
            {
                new($"Funktsioon on murd $\\frac{{{pl}}}{{{ql}}}$. Lugeja aste on ${n}$, nimetaja aste on ${m}$."),
            };

            var poolused = T.AsumptootideAndmed.Where(a => a.OnVertikaalne).Select(a => a.X).ToList();
            var nimetajaJuured = LahendaLihtsalt(new SymbolicExpression(f.Denominator().Expression));
            sammud.Add(new(nimetajaJuured.Count == 0
                ? $"Nimetaja ${ql}$ ei ole kunagi null, seega vertikaalseid asümptoote pole."
                : "Nimetaja nullkohad (vertikaalse asümptoodi kandidaadid):",
                nimetajaJuured.Count == 0 ? null : string.Join(", \\quad ", nimetajaJuured.Select(j => $"x = {j.L}"))));
            if (nimetajaJuured.Count > 0)
            {
                sammud.Add(new(poolused.Count == nimetajaJuured.Count
                    ? "Lugeja ei ole seal null, seega need on vertikaalsed asümptoodid."
                    : "Kus ka lugeja on null, võib murdu taandada – seal asümptooti pole."));
            }

            if (n < m)
            {
                sammud.Add(new("Lugeja aste on väiksem kui nimetaja aste ⇒ $f(x) \\to 0$, kui $x \\to \\pm\\infty$.", "y = 0"));
            }
            else if (n == m)
            {
                var b = p[^1] / q[^1];
                sammud.Add(new("Astmed on võrdsed ⇒ horisontaalne asümptoot on kõrgeimate astmete kordajate jagatis:",
                    $"y = \\frac{{{p[^1].Latex()}}}{{{q[^1].Latex()}}} = {b.Latex()}"));
            }
            else if (n == m + 1)
            {
                var (jagatis, jaak) = Jaga(p, q);
                sammud.Add(new("Lugeja aste on ühe võrra suurem ⇒ on kaldasümptoot. Jagame lugeja nimetajaga:",
                    $"\\frac{{{pl}}}{{{ql}}} = {Vorrandid.Polynoom(jagatis)} + \\frac{{{Vorrandid.Polynoom(jaak)}}}{{{ql}}}"));
                sammud.Add(new("Murdosa läheneb nullile, seega kaldasümptoot on:", $"y = {Vorrandid.Polynoom(jagatis)}"));
            }
            else
            {
                sammud.Add(new("Lugeja aste on vähemalt 2 võrra suurem ⇒ $x \\to \\pm\\infty$ korral asümptoote pole."));
            }
            sammud.Add(new($"Vastus: {T.Asumptoodid}."));
            return new("Astmete võrdlus", sammud);
        }

        // Polünoomide jagamine: p = jagatis · q + jääk.
        private static (Murd[] Jagatis, Murd[] Jaak) Jaga(Murd[] p, Murd[] q)
        {
            var jaak = (Murd[])p.Clone();
            int n = p.Length - 1, m = q.Length - 1;
            var jagatis = new Murd[n - m + 1];
            for (int i = n - m; i >= 0; i--)
            {
                var k = jaak[i + m] / q[m];
                jagatis[i] = k;
                for (int j = 0; j <= m; j++) jaak[i + j] = jaak[i + j] - k * q[j];
            }
            return (jagatis, jaak[..Math.Max(1, m)]);
        }

        private List<LahenduseViis> KriitilisedPunktid()
        {
            if (A.DfOnNull)
                return [new("Konstantne funktsioon", [new("$f'(x) = 0$ iga $x$ korral, sest funktsioon on konstantne.")])];
            return Vorrand(df, "f'(x)", A.Kriitilised, x => double.IsFinite(F.F(x)) && double.IsFinite(F.Tuletisfunktsioon(x)),
                [new("Kriitilised punktid on kohad, kus tuletis on null (seal võib olla tipp või põhi).", $"f'(x) = {L(df)}")]);
        }

        private List<LahenduseViis> Monotoonsus(int mark)
        {
            if (A.DfOnNull)
                return [new("Konstantne funktsioon", [new("$f'(x) = 0$ kõikjal – funktsioon ei kasva ega kahane.")])];

            var hulk = MargiVahemikud(F.Tuletisfunktsioon, A.Piirkond, A.Kriitilised, mark, kinnisedOtsad: false, yhenda: true);
            var sammud = new List<LahenduseSamm>
            {
                new("Funktsioon kasvab, kus $f'(x) > 0$, ja kahaneb, kus $f'(x) < 0$.", $"f'(x) = {L(df)}"),
                new(A.Kriitilised.Count == 0 ? "Kriitilisi punkte ($f'(x) = 0$) pole." : "Kriitilised punktid ($f'(x) = 0$):",
                    A.Kriitilised.Count == 0 ? null : KohadL(A.Kriitilised)),
                Margitabel(F.Tuletisfunktsioon, "f'", A.Kriitilised, s => s > 0 ? "kasvab $\\nearrow$" : s < 0 ? "kahaneb $\\searrow$" : "$0$").Samm,
                VastusHulk(mark > 0 ? "X_{\\uparrow}" : "X_{\\downarrow}", hulk, mark > 0 ? T.Kasvamisvahemikud : T.Kahanemisvahemikud, punktideKuju: false),
            };
            return [new("Tuletise märk", sammud)];
        }

        private List<LahenduseViis> Ekstreemumid()
        {
            if (A.DfOnNull)
                return [new("Konstantne funktsioon", [new("Konstantsel funktsioonil pole ekstreemumeid.")])];
            if (A.Kriitilised.Count == 0)
            {
                return [new("Tuletise märk", [
                    new("Ekstreemum saab olla ainult kriitilises punktis, kus $f'(x) = 0$.", $"f'(x) = {L(df)}"),
                    new("Võrrandil $f'(x) = 0$ pole lahendeid, seega ekstreemumeid pole."),
                ])];
            }

            var kohad = A.Kriitilised.OrderBy(Math.Abs).Take(MaxPunkte).Order().ToList();
            var esimene = new List<LahenduseSamm>
            {
                new("Ekstreemum saab olla ainult kriitilises punktis ($f'(x) = 0$). Kriitilised punktid:", KohadL(A.Kriitilised)),
                new("Kui $f'$ märk muutub $+ \\to -$, on seal maksimum (tipp); kui $- \\to +$, on miinimum (põhi)."),
                Margitabel(F.Tuletisfunktsioon, "f'", A.Kriitilised, s => s > 0 ? "kasvab $\\nearrow$" : s < 0 ? "kahaneb $\\searrow$" : "$0$").Samm,
            };
            foreach (var x in kohad)
            {
                int vasak = Math.Sign(F.Tuletisfunktsioon(x - H)), parem = Math.Sign(F.Tuletisfunktsioon(x + H));
                string muutus = $"$f'$ märk: ${MarkL(vasak)} \\to {MarkL(parem)}$";
                if (vasak > 0 && parem < 0)
                    esimene.Add(new($"$x = {N(x)}$: {muutus} ⇒ **maksimum**.", SubValem(x)));
                else if (vasak < 0 && parem > 0)
                    esimene.Add(new($"$x = {N(x)}$: {muutus} ⇒ **miinimum**.", SubValem(x)));
                else
                    esimene.Add(new($"$x = {N(x)}$: {muutus}; märk ei muutu, seega ekstreemumit pole."));
            }
            esimene.Add(new($"Vastus: {T.Ekstreemumid}"));

            var teine = new List<LahenduseSamm>
            {
                new("Kriitilises punktis: kui $f''(x) > 0$, on miinimum (graafik on nõgus $\\cup$); kui $f''(x) < 0$, on maksimum ($\\cap$).", $"f''(x) = {L(d2f)}"),
            };
            foreach (var x in kohad)
            {
                double v = F.TeineTuletisfunktsioon(x);
                string arvutus = $"f''({N(x)}) = {Latex.Vorminda(d2f, N(x))} {Vord(v)}";
                if (arvutus.Length > 160) arvutus = $"f''({N(x)}) {Vord(v)}";
                if (Math.Abs(v) < 1e-9)
                    teine.Add(new($"$x = {N(x)}$: $f'' = 0$ – see test ei otsusta, vaata esimest viisi (tuletise märk).", arvutus));
                else
                    teine.Add(new($"$x = {N(x)}$: {(v > 0 ? "$f'' > 0$ ⇒ **miinimum**" : "$f'' < 0$ ⇒ **maksimum**")}.", arvutus));
            }
            teine.Add(new($"Vastus: {T.Ekstreemumid}"));
            return [new("Tuletise märgi muutus", esimene), new("Teise tuletise abil", teine)];
        }

        private List<LahenduseViis> Kumerus(int mark)
        {
            if (A.D2OnNull)
                return [new("Sirge", [new("$f''(x) = 0$ kõikjal, seega graafik on sirge – see pole kumer ega nõgus.")])];

            var hulk = MargiVahemikud(F.TeineTuletisfunktsioon, A.Piirkond, A.D2Nullid, mark, kinnisedOtsad: false, yhenda: true);
            var sammud = new List<LahenduseSamm>
            {
                new("Graafik on kumer ($\\cap$), kus $f''(x) < 0$, ja nõgus ($\\cup$), kus $f''(x) > 0$.", $"f''(x) = {L(d2f)}"),
                new(A.D2Nullid.Count == 0 ? "$f''(x) = 0$ lahendeid pole." : "$f''(x) = 0$ lahendid:", A.D2Nullid.Count == 0 ? null : KohadL(A.D2Nullid)),
                Margitabel(F.TeineTuletisfunktsioon, "f''", A.D2Nullid, s => s > 0 ? "nõgus $\\cup$" : s < 0 ? "kumer $\\cap$" : "$0$").Samm,
                VastusHulk(mark > 0 ? "X_{\\cup}" : "X_{\\cap}", hulk, mark > 0 ? T.Nogususvahemikud : T.Kumerusvahemikud, punktideKuju: false),
            };
            return [new("Teise tuletise märk", sammud)];
        }

        private List<LahenduseViis> Kaanupunktid()
        {
            if (A.D2OnNull)
                return [new("Sirge", [new("$f''(x) = 0$ kõikjal – graafik on sirge, käänupunkte pole.")])];

            // Võrrand enne kontrolli, et kontrollis oleksid täpsed kujud (nt \frac{\sqrt{2}}{2}).
            var viisid = Vorrand(d2f, "f''(x)", A.D2Nullid, x => double.IsFinite(F.F(x)),
                [new("Käänupunkt saab olla seal, kus $f''(x) = 0$.", $"f''(x) = {L(d2f)}")]);

            var kontroll = new List<LahenduseSamm> { new("Käänupunktis $f''$ märk muutub (graafik läheb kumerast nõgusaks või vastupidi). Kontrollime:") };
            foreach (var x in A.D2Nullid.OrderBy(Math.Abs).Take(MaxPunkte).Order())
            {
                int vasak = Math.Sign(F.TeineTuletisfunktsioon(x - H)), parem = Math.Sign(F.TeineTuletisfunktsioon(x + H));
                string muutus = $"$f''$ märk: ${MarkL(vasak)} \\to {MarkL(parem)}$";
                kontroll.Add(vasak != 0 && parem != 0 && vasak != parem
                    ? new($"$x = {N(x)}$: {muutus} ⇒ käänupunkt.", SubValem(x))
                    : new($"$x = {N(x)}$: {muutus}; märk ei muutu, seega käänupunkti pole."));
            }
            kontroll.Add(new($"Vastus: {T.Kaanupunktid}"));

            if (A.D2Nullid.Count == 0)
                return viisid.Select(v => v with { Sammud = [.. v.Sammud.Take(v.Sammud.Count - 1), new("Võrrandil pole lahendeid, seega käänupunkte pole.")] }).ToList();
            return viisid.Select(v => v with { Sammud = [.. v.Sammud.Take(v.Sammud.Count - 1), v.Sammud[^1] with { Tekst = "Saame:" }, .. kontroll] }).ToList();
        }

        // ---------- Märgitabel ----------

        private (LahenduseSamm Samm, int Vahemikke) Margitabel(Func<double, double> g, string gNimi, IEnumerable<double> punktid,
            Func<int, string> jareldus, List<Intervall>? alus = null, bool sulgudes = false)
        {
            var read = new List<(Intervall Iv, double Proov, double V)>();
            var lopud = punktid.Order().ToList();
            foreach (var iv in alus ?? A.Piirkond)
            {
                var piirid = new List<double> { iv.Vasak };
                piirid.AddRange(lopud.Where(p => p > iv.Vasak && p < iv.Parem));
                piirid.Add(iv.Parem);
                for (int k = 0; k + 1 < piirid.Count; k++)
                {
                    double t = Proov(piirid[k], piirid[k + 1]);
                    read.Add((new Intervall(piirid[k], false, piirid[k + 1], false), t, g(t)));
                }
            }

            bool karbitud = read.Count > 9;
            if (karbitud)
            {
                static double Kaugus(Intervall iv) => iv.Vasak <= 0 && iv.Parem >= 0 ? 0 : Math.Min(Math.Abs(iv.Vasak), Math.Abs(iv.Parem));
                read = read.OrderBy(r => Kaugus(r.Iv)).Take(9).OrderBy(r => r.Iv.Vasak).ToList();
            }

            var tabel = new List<IReadOnlyList<string>> { new[] { "Vahemik", "Proovipunkt", "Märk", "Järeldus" } };
            foreach (var (iv, t, v) in read)
            {
                tabel.Add([
                    $"${IntervallL(iv)}$",
                    sulgudes ? $"$x = {N(t)}$: ${VaartusL(v)}$" : $"${gNimi}({N(t)}) {Vord(v)}$",
                    $"${MarkL(Math.Sign(v))}$",
                    jareldus(Math.Sign(v)),
                ]);
            }
            string tekst = "Jagame punktidega vahemikeks ja valime igast vahemikust proovipunkti:"
                + (karbitud ? " (näidatud on nullpunktile lähimad vahemikud, edasi kordub muster)" : "");
            return (new LahenduseSamm(tekst, null, tabel), read.Count);
        }

        // Lihtne proovipunkt vahemikus: täisarv, siis pool, kümnendik…
        private static double Proov(double a, double b)
        {
            if (double.IsNegativeInfinity(a) && double.IsPositiveInfinity(b)) return 0;
            if (double.IsNegativeInfinity(a)) return Math.Ceiling(b) - 1;
            if (double.IsPositiveInfinity(b)) return Math.Floor(a) + 1;
            double kesk = (a + b) / 2;
            foreach (var skaala in new[] { 1.0, 2, 10, 100, 1000 })
            {
                double lo = Math.Floor(a * skaala) + 1, hi = Math.Ceiling(b * skaala) - 1;
                if (lo <= hi) return Math.Clamp(Math.Round(kesk * skaala), lo, hi) / skaala;
            }
            return kesk;
        }

        // ---------- Joonis ----------

        private const int JooniseSamme = 400;
        private const int MaxJooniseSilte = 8;   // rohkemate punktide korral (nt sin(x)) koordinaate ei kirjutata

        // Väike graafik viisi „Graafikult“ jaoks: sama x-vahemik nagu suurel graafikul (nullkohad, ekstreemumid ja
        // käänupunktid nähtavad); x-telg on alati nähtav, sest joonisel loetakse vastus x-telje järgi.
        private Joonis Joonis(IEnumerable<double> kohad, int varjuta = 0)
        {
            double xMin = T.SoovitatudXMin, xMax = T.SoovitatudXMax;
            var x = Enumerable.Range(0, JooniseSamme + 1).Select(i => xMin + (xMax - xMin) * i / JooniseSamme).ToList();
            var y = x.Select(v => F.F(v) is var fv && double.IsFinite(fv) ? fv : (double?)null).ToList();

            var naha = kohad.Where(k => k >= xMin && k <= xMax).Order().ToList();
            var margid = naha.Select(k => new JooniseMark(k, 0, naha.Count <= MaxJooniseSilte ? KohaSilt(k) : null)).ToList();

            // y-telg: asümptootide korral lõigatakse äärmused ära (nagu graafik.js-is).
            var arvud = y.OfType<double>().Order().ToList();
            double alumine = 0, ulemine = 0;
            if (arvud.Count > 0)
            {
                double Kvantiil(double p) => arvud[(int)(p * (arvud.Count - 1))];
                double q05 = Kvantiil(0.05), q95 = Kvantiil(0.95);
                (alumine, ulemine) = (arvud[0], arvud[^1]);
                if (ulemine - alumine > 5 * (q95 - q05))
                {
                    double varu = (q95 - q05) * 0.5;
                    (alumine, ulemine) = (Math.Max(alumine, q05 - varu), Math.Min(ulemine, q95 + varu));
                }
            }
            // Ekstreemumite või käänupunktide korral (ja ilma vertikaalsete asümptootideta) telg nende järgi, et kõver
            // ei jääks lamedaks (nt x^3 - 3x vahemikus [-3; 3]: [-6; 6], mitte [-18; 18]).
            var olulised = T.EkstreemumidePunktid.Select(e => e.Y).Concat(T.KaanupunktidePunktid.Select(p => p.Y)).ToList();
            if (olulised.Count > 0 && !T.AsumptootideAndmed.Any(s => s.OnVertikaalne))
            {
                olulised.Add(0);
                double pMin = olulised.Min(), pMax = olulised.Max(), ulatus = pMax - pMin;
                if (ulatus > 1e-9 && ulemine - alumine > 4 * ulatus)
                    (alumine, ulemine) = (Math.Max(alumine, pMin - ulatus), Math.Min(ulemine, pMax + ulatus));
            }
            alumine = Math.Min(alumine, 0);
            ulemine = Math.Max(ulemine, 0);
            if (ulemine - alumine < 1e-9) (alumine, ulemine) = (alumine - 1, ulemine + 1);
            double lisa = (ulemine - alumine) * 0.1;
            (alumine, ulemine) = (alumine - lisa, ulemine + lisa);

            // Vertikaalse asümptoodi eri pooltel olevaid punkte ei ühendata.
            for (int i = 1; i < y.Count; i++)
            {
                if (y[i - 1] is double a && y[i] is double b && ((a > ulemine && b < alumine) || (a < alumine && b > ulemine)))
                    y[i - 1] = null;
            }

            var asumptoodid = T.AsumptootideAndmed.Where(s => s.OnVertikaalne && s.X > xMin && s.X < xMax).Select(s => s.X).ToList();
            return new Joonis(xMin, xMax, alumine, ulemine, x, y, margid, asumptoodid, varjuta);
        }

        // Punkti silt joonisel (lihttekst, mitte LaTeX): x = 2, x = 1/2 või x ≈ 1.73.
        private static string KohaSilt(double x) =>
            Murd.FromDouble(x, 100) is { } m
                ? "x = " + (m.OnTaisarv ? m.P.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{m.P}/{m.Q}").Replace('-', '−')
                : "x ≈ " + Vorminda(Math.Round(x, 2)).Replace('-', '−');

        // ---------- Vormindamine ----------

        private static string L(SymbolicExpression e) => Latex.Vorminda(e);

        // Arv tabelites ja vastustes: täpne kuju, kui see on teada (nt -\sqrt{3}), muidu lihtne murd või kümnendmurd.
        private string N(double v)
        {
            if (double.IsInfinity(v)) return v > 0 ? "\\infty" : "-\\infty";
            foreach (var (nv, nl) in nimed)
            {
                if (Math.Abs(nv - v) < 1e-9 * (1 + Math.Abs(v))) return nl;
            }
            if (Murd.FromDouble(v, 100) is { } m) return m.Latex();
            // Trigonomeetrilise funktsiooni kohad on tavaliselt π murdosad: 1.5708 → \frac{\pi}{2}.
            if (onTrig && Murd.FromDouble(v / Math.PI, 12) is { } p && !p.OnNull)
                return (p.Mark < 0 ? "-" : "") + PiKordne(p.Mark < 0 ? -p : p);
            return Latex.Arv(v);
        }

        private static string VaartusL(double v)
        {
            if (!double.IsFinite(v)) return Latex.Arv(v);
            if (Murd.FromDouble(v, 1000) is { } m) return m.Latex();
            return "\\approx " + Latex.Arv(v);
        }

        // "= 5" või "\approx 0.2707" (mitte "= \approx 0.2707").
        private static string Vord(double v)
        {
            string s = VaartusL(v);
            return s.StartsWith("\\approx") ? s : "= " + s;
        }

        private static string PiirL(double? v) => v is double d ? VaartusL(d) : "\\text{puudub}";

        private static string MarkL(int s) => s > 0 ? "+" : s < 0 ? "-" : "0";

        private string KohadL(List<double> kohad)
        {
            var naidatud = kohad.Count <= 10 ? kohad : kohad.OrderBy(Math.Abs).Take(10).Order().ToList();
            return string.Join(", \\quad ", naidatud.Select(x => $"x = {N(x)}")) + (naidatud.Count < kohad.Count ? ", \\dots" : "");
        }

        private string SubValem(double x)
        {
            double y = F.F(x);
            string s = $"y = f({N(x)}) = {Latex.Vorminda(f, N(x))} {Vord(y)}";
            return s.Length > 180 ? $"y = f({N(x)}) {Vord(y)}" : s;
        }

        private string IntervallL(Intervall iv) =>
            iv.Vasak == iv.Parem
                ? $"\\{{{N(iv.Vasak)}\\}}"
                : (iv.VasakKinnine ? "[" : "(") + N(iv.Vasak) + ";\\ " + N(iv.Parem) + (iv.ParemKinnine ? "]" : ")");

        private string? HulkL(List<Intervall> hulk, bool punktideKuju)
        {
            if (hulk.Count == 0) return "\\varnothing";
            if (hulk.Count == 1 && double.IsNegativeInfinity(hulk[0].Vasak) && double.IsPositiveInfinity(hulk[0].Parem))
                return "(-\\infty;\\ \\infty)";
            if (hulk.Count > 6) return null;

            bool ainultPunktid = punktideKuju && double.IsNegativeInfinity(hulk[0].Vasak) && double.IsPositiveInfinity(hulk[^1].Parem);
            for (int k = 0; ainultPunktid && k < hulk.Count - 1; k++)
                ainultPunktid = hulk[k].Parem == hulk[k + 1].Vasak && !hulk[k].ParemKinnine && !hulk[k + 1].VasakKinnine;
            if (ainultPunktid)
                return "\\mathbb{R} \\setminus \\{" + string.Join(";\\ ", hulk.Take(hulk.Count - 1).Select(iv => N(iv.Parem))) + "\\}";

            return string.Join(" \\cup ", hulk.Select(IntervallL));
        }

        private LahenduseSamm VastusHulk(string nimi, List<Intervall> hulk, string tekst, bool punktideKuju) =>
            HulkL(hulk, punktideKuju) is { } l
                ? new LahenduseSamm("Vastus:", $"{nimi} = {l}")
                : new LahenduseSamm($"Vastus: {tekst}");
    }
}
