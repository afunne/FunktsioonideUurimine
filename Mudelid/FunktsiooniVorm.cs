using System.ComponentModel.DataAnnotations;

namespace FunktsioonideUurimine.Mudelid;

// Loomise ja muutmise vorm: kasutaja sisestab ainult valemi, ülejäänud väljad arvutab server.
public class FunktsiooniVorm
{
    public int Id { get; set; }

    [Display(Name = "Valem f(x)")]
    [Required(ErrorMessage = "Valem on kohustuslik.")]
    [StringLength(200, ErrorMessage = "Valem võib olla kuni 200 märki pikk.")]
    [RegularExpression(@"^[0-9a-zA-Z+\-*/^().\s]+$",
        ErrorMessage = "Valem sisaldab lubamatuid märke. Kasuta kümnendkohtade jaoks punkti, mitte koma.")]
    public string Valem { get; set; } = string.Empty;
}

// Graafiku osavaate seaded: kas kindel valem (detailvaade) või vormi sisendvälja id (eelvaade).
public record GraafikuSeaded(string? Valem, string? SisendiId);
