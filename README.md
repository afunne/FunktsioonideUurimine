# Funktsioonide uurimise veebirakendus

ASP.NET Core MVC (.NET 10) + EF Core (SQLite) + Chart.js + MathNet.Symbolics.

## Käivitamine

```
dotnet run
```

Ava http://localhost:5080. Käivitamisel rakendatakse migratsioonid automaatselt; tühja andmebaasi lisatakse näidisfunktsioonid ja Moodle'i ülesannete funktsioonid.

Migratsioonide käsitsi haldamine (tööriist on projekti kohalikus manifestis):

```
dotnet tool restore
dotnet ef migrations add <Nimi>
dotnet ef database update
```

## Struktuur

| Fail | Sisu |
|---|---|
| `Mudelid/FunktsiooniUurimine.cs` | EF Core olem (tabel `FunktsiooniUurimised`) – valem ja kõigi uurimisküsimuste vastused |
| `Mudelid/Kysimused.cs` | Uurimisküsimuste loetelu (silt, rühm, väli) – kasutavad detailvaade, eelvaade ja API |
| `Andmed/FunktsioonideKontekst.cs` | `DbContext`, registreeritud `Program.cs`-is |
| `Andmed/Naidisandmed.cs` | Näidisandmed ja Moodle'i ülesannete funktsioonid (Teema 10 ja 11) |
| `Migrations/` | EF Core migratsioonid |
| `Controllers/FunktsioonidController.cs` | CRUD (nimekiri, detailvaade, loomine, muutmine, kustutamine) |
| `Controllers/TeooriaController.cs`, `Views/Teooria/Index.cshtml` | Teooria leht (`/Teooria`): Moodle'i teemad 1–12 KaTeX-valemitega; „Proovi“ nupud avavad `/Funktsioonid/Create?valem=…` |
| `Controllers/GraafikController.cs` | `POST /api/graafik` – y-väärtused ja kõik vastused; `/api/graafik/puutuja`; `/api/graafik/loik` |
| `Teenused/FunktsiooniAnalyysija.cs` | Valemi parsimine, tuletised ja kogu funktsiooni uurimine |
| `Teenused/FunktsiooniLiigitaja.cs` | Funktsiooni liigi määramine avaldise puu järgi |
| `Mudelid/FunktsiooniLiigid.cs` | Liikide loetelu (võti, nimetus) ja nimekirja vaatemudel |
| `wwwroot/js/valemiklaviatuur.js` | Visuaalne valemiväli (MathLive) ja valemi klaviatuur; sünkroonis tekstiväljaga |
| `wwwroot/js/valemiteisendus.js` | Teisendus LaTeX ↔ tekstivalem (`\frac{x+2}{x-1}` ↔ `(x + 2)/(x - 1)`) |
| `wwwroot/js/graafik.js` | Graafik (`<canvas id="funktsiooniGraafik">`): f(x) sinine joon, asümptoodid hallid katkendjooned, olulised punktid (nullkohad, ekstreemumid, käänupunktid, lõikepunkt y-teljega) koos koordinaatidega |
| `wwwroot/js/valemikuju.js` | Päise lüliti „Ilus valem“: valemid KaTeXiga (nimekiri, pealkiri) ja unicode-tekstina (graafiku legend) |
| `wwwroot/js/lahendused.js` | Lahenduskäigud vastuste juures (nupp „Lahendus“, vahelehed mitme viisi korral, valemid KaTeXiga) |
| `Teenused/FunktsiooniAnalyysija.Lahendused.cs` | Lahenduskäigud kõigile küsimustele (märgitabelid, piirväärtused, mitu lahendusviisi) |
| `Teenused/Vorrandid.cs` | Võrrandi g(x) = 0 lahendamine koolivõtetega koos sammudega |
| `Teenused/Tuletised.cs` | Tuletise leidmine reeglite kaupa (summa, aste, korrutis, jagatis, ahelreegel) |
| `Teenused/Lahendus.cs` | Lahenduse mudel, ratsionaalarv `Murd`, avaldis koolipärases LaTeXis |

## Millele rakendus vastab

**Funktsiooni uurimine (Teema 8):** määramispiirkond X, muutumispiirkond Y, nullkohad X₀, positiivsus- ja
negatiivsuspiirkond X⁺/X⁻, paarsus, perioodilisus, lõikepunkt y-teljega, asümptoodid (vertikaalne, horisontaalne, kald).

**Uurimine tuletise abil (Teema 11):** f'(x), kriitilised punktid, kasvamis- ja kahanemisvahemikud X↑/X↓,
ekstreemumkohad (x_max, x_min koos väärtustega), f''(x), kumerus (∩, f'' < 0) ja nõgusus (∪, f'' > 0), käänupunktid.

**API (lehel ei näidata):** puutuja ja normaali võrrand, tõus ja tõusunurk kohal x₀ (`/api/graafik/puutuja`, Teema 10); suurim ja vähim väärtus lõigul [a; b] (`/api/graafik/loik`).

Graafik on alati nähtav: ilma valemita joonistatakse tühi teljestik, loomise ja muutmise vaates uuenevad graafik ja kõik
vastused valemi sisestamise ajal. x-vahemik valitakse automaatselt nii, et nullkohad, ekstreemumid ja käänupunktid oleksid
näha (nupp „Sobita“); käsitsi muudetud vahemikku ei muudeta.

**Suumimine ja liigutamine** ([chartjs-plugin-zoom](https://www.chartjs.org/chartjs-plugin-zoom/)): kerimisratas või kaks
sõrme suumivad, lohistamine liigutab, Shift + lohistamine suumib valitud alale, nupud + ja − suumivad keskpunkti ümber,
topeltklõps või „Sobita“ viib tagasi algvaatele. Pärast iga suumimist/liigutamist arvutab server punktid uue vahemiku jaoks
(umbes 800 punkti), nii et joon on terav ka sisse suumides ja ulatub välja suumides üle kogu graafiku. Eelvaates jääb
suumitud vaade alles ka siis, kui valemit muudetakse.

**Lahenduskäigud (nagu Photomathis):** iga vastuse juures on nupp „Lahendus“, mis näitab lihtsate sammudena, kuidas
vastuseni jõuda; kui võimalik, mitmel viisil (vahelehed). Nt nullkohad: diskriminant, taandatud ruutvõrrandi valem, Viète’i
teoreem, x sulgude ette, asendus t = x², Horneri skeem, murd (lugeja = 0), korrutis, ln/eˣ, sin/cos/tan üldlahend ja graafikult;
ekstreemumid tuletise märgi muutuse ja teise tuletise abil; paarsus f(−x) asendusega, astmete järgi ja arvudega;
asümptoodid piirväärtustega ja (ratsionaalfunktsioonil) astmete võrdlusega. Märgipiirkondade, monotoonsuse ja kumeruse
juures on märgitabel proovipunktidega. Lahendid on täpsel kujul (−√3, (3 + √5)/2, π/2), kõrval ligikaudne väärtus.
Viisis „Graafikult“ (nullkohad, X⁺, X⁻) on väike graafik: nullkohad on märgitud ja X⁺/X⁻ juures on vastav ala
värvitud (roheline f(x) > 0, punane f(x) < 0). Täpne lahend kontrollitakse alati numbrilise vastu; kui ükski koolivõte ei sobi (nt x³ + x + 1 = 0), näidatakse numbrilist
lahendamist. Lahendused arvutatakse graafiku päringus (`/api/graafik` → `lahendused`), andmebaasi neid ei salvestata.

**Lülitid:** päises olev „Ilus valem“ näitab valemeid matemaatikana (x³ − 3x, murrujoon, √, eˣ) või kirjutatud kujul
(`x^3 - 3*x`) – nimekirjas (f(x) ja f'(x)), funktsiooni pealkirjas ja graafiku legendis (`valemikuju.js`). Vastuseid ja
lahenduskäike see ei muuda. Loomise ja muutmise vormis saab sisse/välja lülitada visuaalse valemivälja ja valemi
klaviatuuri. Valikud jäetakse brauserisse meelde (localStorage).

**Funktsioonide liigid:** iga valemi liik määratakse automaatselt – konstantne, lineaar-, ruut-, kuup-, polünoom- (aste ≥ 4),
ratsionaal-, juur-, eksponent-, logaritm- või trigonomeetriline funktsioon; mitu transtsendentset osa korraga (nt `exp(x)*sin(x)`)
annab liitfunktsiooni. Nimekirjas on funktsioonid rühmitatud liikide kaupa ning ülal olevate nuppudega saab näidata kõiki või
ainult ühte liiki (`/?liik=ruut`). Liik on näha ka vastuste hulgas ja detailvaate pealkirja all.

**Valemi klaviatuur:** loomise ja muutmise vormis on visuaalne valemiväli ([MathLive](https://mathlive.io)), kus aste ja murd
näevad välja nagu vihikus (x², murrujoonega murd). Klaviatuuril on mallid (▢², ▢ⁿ, murd, √, ⁿ√, sulud, eˣ), funktsioonid
(sin, cos, tan, ln, exp, π, e), numbrid ja tehted. Visuaalses väljas saab kirjutada ka tavaklaviatuuriga: `^` teeb astme,
`/` murru. Allpool olev tekstiväli töötab endiselt – mõlemad on sünkroonis ja salvestatakse tekstikujul (`x^2/2 - sqrt(x)`).
Kui MathLive ei laadi (nt internetita), jääb alles tavaline tekstiväli.

## Kuidas analüüs töötab

1. Valem parsitakse teegiga **MathNet.Symbolics**, f'(x) ja f''(x) leitakse **sümbolselt** (nt `x^3 - 3*x` → `3*x^2 - 3` → `6*x`).
2. f, f' ja f'' arvutatakse vahemikus [-50; 50] sammuga 0.01.
3. **Määramispiirkond**: lõigud, kus f on lõplik; otspunktid täpsustatakse poolitusmeetodiga, poolused leitakse eraldi.
4. **Nullkohad**, **kriitilised punktid** ja f'' nullkohad: märgimuutus + poolitusmeetod, puutepunktid kuldlõike otsinguga.
5. **Märgipiirkonnad** (X⁺, X⁻, X↑, X↓, kumerus, nõgusus): intervallmeetod – määramispiirkond jagatakse nullkohtadega ja märk leitakse igas osas.
6. **Ekstreemumid**: f' märk muutub + → − (max) või − → + (min). **Käänupunktid**: f'' märk muutub.
7. **Muutumispiirkond** ja **asümptoodid**: väärtused ekstreemumites ja otspunktides ning piirväärtused lõpmatuses ja poolustel.
8. **Paarsus**: f(-x) võrreldakse f(x)-ga; **perioodilisus**: kontrollitakse perioode kπ/m ja täisarve.

Valideerimine: tühi valem, lubamatud märgid, koma kümnendkohtades, tundmatud muutujad/funktsioonid ja süntaksivead annavad eestikeelse veateate.

Unaarne miinus loetakse nagu koolis: `-x^2` = −(x²) (MathNet loeks (−x)²).

Toetatud: `x`, `+ - * / ^`, `sin cos tan exp ln sqrt`, `pi`, `e`. Korrutusmärk on kohustuslik (`3*x`).
