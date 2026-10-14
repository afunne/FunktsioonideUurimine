using FunktsioonideUurimine.Andmed;
using FunktsioonideUurimine.Mudelid;
using FunktsioonideUurimine.Teenused;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FunktsioonideUurimine.Controllers;

public class FunktsioonidController : Controller
{
    private readonly FunktsioonideKontekst _kontekst;
    private readonly FunktsiooniAnalyysija _analyysija;

    public FunktsioonidController(FunktsioonideKontekst kontekst, FunktsiooniAnalyysija analyysija)
    {
        _kontekst = kontekst;
        _analyysija = analyysija;
    }

    // GET: /Funktsioonid?liik=ruut  – ilma liigita näidatakse kõik funktsioonid liikide kaupa rühmitatult
    public async Task<IActionResult> Index(string? liik)
    {
        var loendus = await _kontekst.FunktsiooniUurimised
            .GroupBy(f => f.Liik)
            .Select(g => new { Liik = g.Key, Arv = g.Count() })
            .ToDictionaryAsync(g => g.Liik, g => g.Arv);

        var paring = _kontekst.FunktsiooniUurimised.AsQueryable();
        if (!string.IsNullOrEmpty(liik))
            paring = paring.Where(f => f.Liik == liik);

        var funktsioonid = await paring.OrderByDescending(f => f.LuodudAeg).ToListAsync();

        var liigid = FunktsiooniLiigid.Koik
            .Where(l => loendus.ContainsKey(l.Voti))
            .Select(l => new LiigiFilter(l, loendus[l.Voti]))
            .ToList();

        return View(new NimekirjaVaade(funktsioonid, string.IsNullOrEmpty(liik) ? null : liik, liigid, loendus.Values.Sum()));
    }

    // GET: /Funktsioonid/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var olem = await _kontekst.FunktsiooniUurimised.FirstOrDefaultAsync(f => f.Id == id);
        return olem is null ? NotFound() : View(olem);
    }

    // GET: /Funktsioonid/Create?valem=x^2-4  – valem on valikuline (teooria lehe „Proovi rakenduses“ lingid)
    public IActionResult Create(string? valem) => View(new FunktsiooniVorm { Valem = valem ?? string.Empty });

    // POST: /Funktsioonid/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FunktsiooniVorm vorm)
    {
        var tulemus = Analyysi(vorm);
        if (tulemus is null)
            return View(vorm);

        var olem = new FunktsiooniUurimine();
        tulemus.KannaOlemisse(olem);

        await _kontekst.FunktsiooniUurimised.AddAsync(olem);
        await _kontekst.SaveChangesAsync();

        TempData["Teade"] = "Funktsioon on analüüsitud ja salvestatud.";
        return RedirectToAction(nameof(Details), new { id = olem.Id });
    }

    // GET: /Funktsioonid/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var olem = await _kontekst.FunktsiooniUurimised.FindAsync(id);
        if (olem is null)
            return NotFound();
        return View(new FunktsiooniVorm { Id = olem.Id, Valem = olem.Valem });
    }

    // POST: /Funktsioonid/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FunktsiooniVorm vorm)
    {
        if (id != vorm.Id)
            return BadRequest();

        var olem = await _kontekst.FunktsiooniUurimised.FindAsync(id);
        if (olem is null)
            return NotFound();

        var tulemus = Analyysi(vorm);
        if (tulemus is null)
            return View(vorm);

        tulemus.KannaOlemisse(olem);
        await _kontekst.SaveChangesAsync();

        TempData["Teade"] = "Muudatused on salvestatud.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: /Funktsioonid/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var olem = await _kontekst.FunktsiooniUurimised.FirstOrDefaultAsync(f => f.Id == id);
        return olem is null ? NotFound() : View(olem);
    }

    // POST: /Funktsioonid/Delete/5
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var olem = await _kontekst.FunktsiooniUurimised.FindAsync(id);
        if (olem is not null)
        {
            _kontekst.FunktsiooniUurimised.Remove(olem);
            await _kontekst.SaveChangesAsync();
            TempData["Teade"] = "Funktsioon on kustutatud.";
        }
        return RedirectToAction(nameof(Index));
    }

    // Valideerib vormi ja analüüsib valemi; vea korral lisab selle ModelState'i ja tagastab null.
    private AnalyysiTulemus? Analyysi(FunktsiooniVorm vorm)
    {
        if (!ModelState.IsValid)
            return null;
        try
        {
            return _analyysija.Analyysi(vorm.Valem);
        }
        catch (ValemiViga viga)
        {
            ModelState.AddModelError(nameof(FunktsiooniVorm.Valem), viga.Message);
            return null;
        }
    }
}
