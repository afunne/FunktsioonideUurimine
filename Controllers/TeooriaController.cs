using Microsoft.AspNetCore.Mvc;

namespace FunktsioonideUurimine.Controllers;

// Teooria leht: Moodle'i kursuse „Matemaatika“ sektsiooni teemad 1–12 (jadad, funktsioonid ja tuletis).
public class TeooriaController : Controller
{
    // GET: /Teooria
    public IActionResult Index() => View();
}
