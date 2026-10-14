using System.ComponentModel.DataAnnotations;
using FunktsioonideUurimine.Mudelid;
using FunktsioonideUurimine.Teenused;
using Microsoft.AspNetCore.Mvc;

namespace FunktsioonideUurimine.Controllers;

// Arvutab graafiku jaoks f(x) väärtused suvalise tekstilise valemi põhjal (MathNet.Symbolics)
// ning tagastab kõik uurimisküsimuste vastused, et eelvaade uueneks kohe valemi sisestamisel.
[ApiController]
[Route("api/graafik")]
public class GraafikController : ControllerBase
{
    public const int MaxPunkte = 5000;

    private readonly FunktsiooniAnalyysija _analyysija;

    public GraafikController(FunktsiooniAnalyysija analyysija) => _analyysija = analyysija;

    public class GraafikuPaering
    {
        [Required(ErrorMessage = "Valem on kohustuslik.")]
        [StringLength(200)]
        public string Valem { get; set; } = string.Empty;

        [Required]
        public double[] X { get; set; } = [];
    }

    public class PuutujaPaering
    {
        [Required(ErrorMessage = "Valem on kohustuslik.")]
        [StringLength(200)]
        public string Valem { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sisesta x0.")]
        public double? X0 { get; set; }
    }

    public class LoiguPaering
    {
        [Required(ErrorMessage = "Valem on kohustuslik.")]
        [StringLength(200)]
        public string Valem { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sisesta lõigu algus a.")]
        public double? A { get; set; }

        [Required(ErrorMessage = "Sisesta lõigu lõpp b.")]
        public double? B { get; set; }
    }

    // POST: /api/graafik  { "valem": "x^3 - 3*x", "x": [-5, -4.9, ...] }
    [HttpPost]
    public IActionResult Arvuta(GraafikuPaering paering)
    {
        if (paering.X.Length is 0 or > MaxPunkte)
            return BadRequest(new { viga = $"Punktide arv peab olema 1 kuni {MaxPunkte}." });

        AnalyysiTulemus analyys;
        try
        {
            analyys = _analyysija.Analyysi(paering.Valem);
        }
        catch (ValemiViga viga)
        {
            return BadRequest(new { viga = viga.Message });
        }

        var funktsioon = analyys.Funktsioon;
        var olem = new FunktsiooniUurimine();
        analyys.KannaOlemisse(olem);

        return Ok(new
        {
            valem = funktsioon.Valem,
            y = paering.X.Select(x => Arv(funktsioon.F(x))),
            nullkohad = analyys.NullkohtadeX,
            ekstreemumid = analyys.EkstreemumidePunktid.Select(e => new { x = e.X, y = e.Y, tyyp = e.OnMaksimum ? "max" : "min" }),
            kaanupunktid = analyys.KaanupunktidePunktid.Select(p => new { x = p.X, y = p.Y }),
            asumptoodid = analyys.AsumptootideAndmed.Select(a => new { vertikaalne = a.OnVertikaalne, x = a.X, k = a.K, b = a.B }),
            yLoikepunkt = Arv(funktsioon.F(0)),
            soovitatudVahemik = new { xMin = analyys.SoovitatudXMin, xMax = analyys.SoovitatudXMax },
            vastused = Kysimused.Vastused(olem),
            lahendused = _analyysija.Lahendused(analyys),
        });
    }

    // POST: /api/graafik/puutuja  { "valem": "x^3 - 2*x", "x0": 1 }  (Teema 10)
    [HttpPost("puutuja")]
    public IActionResult Puutuja(PuutujaPaering paering)
    {
        try
        {
            var t = _analyysija.Puutuja(paering.Valem, paering.X0!.Value);
            return Ok(new
            {
                x0 = t.X0,
                y0 = t.Y0,
                k = t.K,
                nurk = t.TousunurkKraadides,
                puutuja = t.Puutuja,
                normaal = t.Normaal,
                normaaliK = t.NormaaliK,
                normaaliB = t.NormaaliB,
                tekst = new
                {
                    puutepunkt = $"({FunktsiooniAnalyysija.Vorminda(t.X0)}; {FunktsiooniAnalyysija.Vorminda(t.Y0)})",
                    k = FunktsiooniAnalyysija.Vorminda(t.K),
                    nurk = FunktsiooniAnalyysija.Vorminda(t.TousunurkKraadides) + "°",
                },
            });
        }
        catch (ValemiViga viga)
        {
            return BadRequest(new { viga = viga.Message });
        }
    }

    // POST: /api/graafik/loik  { "valem": "0.25*x^4 - 2*x^2", "a": -1, "b": 3 }  (Teema 11 ül 10)
    [HttpPost("loik")]
    public IActionResult Loik(LoiguPaering paering)
    {
        try
        {
            var t = _analyysija.SuurimJaVahim(paering.Valem, paering.A!.Value, paering.B!.Value);
            return Ok(new
            {
                suurim = new { x = t.Suurim.X, y = t.Suurim.Y, tekst = $"{FunktsiooniAnalyysija.Vorminda(t.Suurim.Y)} (x = {FunktsiooniAnalyysija.Vorminda(t.Suurim.X)})" },
                vahim = new { x = t.Vahim.X, y = t.Vahim.Y, tekst = $"{FunktsiooniAnalyysija.Vorminda(t.Vahim.Y)} (x = {FunktsiooniAnalyysija.Vorminda(t.Vahim.X)})" },
            });
        }
        catch (ValemiViga viga)
        {
            return BadRequest(new { viga = viga.Message });
        }
    }

    // JSON ei toeta NaN/∞ väärtusi – need saadetakse null'ina (graafikus katkestus).
    private static double? Arv(double v) => double.IsFinite(v) ? v : null;
}
