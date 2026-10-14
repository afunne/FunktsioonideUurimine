using FunktsioonideUurimine.Mudelid;
using MathNet.Symbolics;
using Avaldis = MathNet.Symbolics.Expression;

namespace FunktsioonideUurimine.Teenused;

/// <summary>
/// Määrab valemi liigi avaldise puu järgi: polünoomi aste, x nimetajas (ratsionaal), murdastendaja (juur),
/// x astendajas või exp (eksponent), ln (logaritm), sin/cos/tan (trigonomeetriline). Mitu transtsendentset liiki → liitfunktsioon.
/// </summary>
public static class FunktsiooniLiigitaja
{
    private static readonly SymbolicExpression XMuutuja = SymbolicExpression.Variable("x");

    public static string Liigita(SymbolicExpression avaldis)
    {
        var tunnused = new Tunnused();
        Kogu(avaldis.Expression, tunnused);

        int transtsendentseid = (tunnused.Trigonomeetriline ? 1 : 0) + (tunnused.Logaritm ? 1 : 0) + (tunnused.Eksponent ? 1 : 0);
        if (transtsendentseid > 1)
            return FunktsiooniLiigid.Liit;
        if (tunnused.Trigonomeetriline)
            return FunktsiooniLiigid.Trigonomeetriline;
        if (tunnused.Logaritm)
            return FunktsiooniLiigid.Logaritm;
        if (tunnused.Eksponent)
            return FunktsiooniLiigid.Eksponent;
        if (tunnused.Juur)
            return FunktsiooniLiigid.Juur;

        try
        {
            if (avaldis.IsPolynomial(XMuutuja))
            {
                return (avaldis.Coefficients(XMuutuja).Length - 1) switch
                {
                    <= 0 => FunktsiooniLiigid.Konstantne,
                    1 => FunktsiooniLiigid.Lineaar,
                    2 => FunktsiooniLiigid.Ruut,
                    3 => FunktsiooniLiigid.Kuup,
                    _ => FunktsiooniLiigid.Polynoom,
                };
            }
        }
        catch (Exception)
        {
            // Polünoomi kontroll ebaõnnestus – liigitame allpool.
        }

        return tunnused.XNimetajas ? FunktsiooniLiigid.Ratsionaal : FunktsiooniLiigid.Liit;
    }

    private sealed class Tunnused
    {
        public bool Trigonomeetriline, Logaritm, Eksponent, Juur, XNimetajas;
    }

    private static void Kogu(Avaldis a, Tunnused t)
    {
        switch (a)
        {
            case Avaldis.Function f:
                if (f.Item1.IsSin || f.Item1.IsCos || f.Item1.IsTan)
                    t.Trigonomeetriline = true;
                else if (f.Item1.IsLn || f.Item1.IsLg)
                    t.Logaritm = true;
                else if (f.Item1.IsExp)
                    t.Eksponent = true;
                Kogu(f.Item2, t);         // sqrt(x) on MathNetis x^(1/2) – vt Power
                break;

            case Avaldis.FunctionN f:
                if (f.Item1.IsLog)
                    t.Logaritm = true;
                foreach (var arg in f.Item2)
                    Kogu(arg, t);
                break;

            case Avaldis.Power p:
                if (SisaldabX(p.Item2))
                {
                    t.Eksponent = true;          // e^x, 2^x
                }
                else if (SisaldabX(p.Item1))
                {
                    double astendaja = Arv(p.Item2);
                    if (double.IsNaN(astendaja) || astendaja != Math.Floor(astendaja))
                        t.Juur = true;           // x^(1/2), x^(1/3)
                    if (astendaja < 0)
                        t.XNimetajas = true;     // x^(-1) – MathNet esitab jagamise negatiivse astmena
                }
                Kogu(p.Item1, t);
                Kogu(p.Item2, t);
                break;

            case Avaldis.Sum s:
                foreach (var liige in s.Item)
                    Kogu(liige, t);
                break;

            case Avaldis.Product k:
                foreach (var tegur in k.Item)
                    Kogu(tegur, t);
                break;
        }
    }

    private static bool SisaldabX(Avaldis a) => a switch
    {
        Avaldis.Identifier => a.Equals(XMuutuja.Expression),
        Avaldis.Function f => SisaldabX(f.Item2),
        Avaldis.FunctionN f => f.Item2.Any(SisaldabX),
        Avaldis.Power p => SisaldabX(p.Item1) || SisaldabX(p.Item2),
        Avaldis.Sum s => s.Item.Any(SisaldabX),
        Avaldis.Product k => k.Item.Any(SisaldabX),
        _ => false,
    };

    private static double Arv(Avaldis a)
    {
        try
        {
            var tulemus = new SymbolicExpression(a).Evaluate(new Dictionary<string, FloatingPoint>());
            return tulemus.IsReal ? tulemus.RealValue : double.NaN;
        }
        catch (Exception)
        {
            return double.NaN;
        }
    }
}
