using System.ComponentModel.DataAnnotations;

namespace FunktsioonideUurimine.Mudelid;

public class FunktsiooniUurimine
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Valem { get; set; } = string.Empty;              // nt "x^3 - 3*x"

    [MaxLength(30)]
    public string Liik { get; set; } = string.Empty;               // kuup – vt FunktsiooniLiigid

    // Teema 8: funktsiooni uurimine

    [MaxLength(500)]
    public string Maaramispiirkond { get; set; } = string.Empty;   // (-∞; ∞)

    [MaxLength(500)]
    public string Muutumispiirkond { get; set; } = string.Empty;   // [-1; ∞)

    [MaxLength(500)]
    public string Nullkohad { get; set; } = string.Empty;          // x1 = -1.7321, x2 = 0, x3 = 1.7321

    [MaxLength(500)]
    public string Positiivsuspiirkond { get; set; } = string.Empty; // (-1.7321; 0) ∪ (1.7321; ∞)

    [MaxLength(500)]
    public string Negatiivsuspiirkond { get; set; } = string.Empty; // (-∞; -1.7321) ∪ (0; 1.7321)

    [MaxLength(500)]
    public string Paarsus { get; set; } = string.Empty;            // paaritu funktsioon: f(-x) = -f(x) …

    [MaxLength(500)]
    public string Perioodilisus { get; set; } = string.Empty;      // ei ole perioodiline

    [MaxLength(500)]
    public string YTeljeLoikepunkt { get; set; } = string.Empty;   // (0; 0)

    [MaxLength(500)]
    public string Asumptoodid { get; set; } = string.Empty;        // vertikaalne: x = 1; horisontaalne: y = 0

    // Teema 11: funktsiooni uurimine tuletise abil

    [MaxLength(500)]
    public string Tuletis { get; set; } = string.Empty;            // 3*x^2 - 3

    [MaxLength(500)]
    public string KriitilisedPunktid { get; set; } = string.Empty; // x1 = -1, x2 = 1

    [MaxLength(500)]
    public string Kasvamisvahemikud { get; set; } = string.Empty;  // (-∞; -1) ∪ (1; ∞)

    [MaxLength(500)]
    public string Kahanemisvahemikud { get; set; } = string.Empty; // (-1; 1)

    [MaxLength(500)]
    public string Ekstreemumid { get; set; } = string.Empty;       // x_max = -1, y_max = 2; x_min = 1, y_min = -2

    [MaxLength(500)]
    public string TeineTuletis { get; set; } = string.Empty;       // 6*x

    [MaxLength(500)]
    public string Kumerusvahemikud { get; set; } = string.Empty;   // (-∞; 0)   – f''(x) < 0, ∩

    [MaxLength(500)]
    public string Nogususvahemikud { get; set; } = string.Empty;   // (0; ∞)    – f''(x) > 0, ∪

    [MaxLength(500)]
    public string Kaanupunktid { get; set; } = string.Empty;       // (0; 0)

    public DateTime LuodudAeg { get; set; } = DateTime.UtcNow;
}
