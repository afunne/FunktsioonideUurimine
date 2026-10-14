// Joonistab funktsiooni f(x) graafiku (Chart.js) koos asümptootide ja oluliste punktidega (nullkohad ○, maksimumid ▲, miinimumid ▼,
// käänupunktid ◆, lõikepunkt y-teljega ■), mille juurde on kirjutatud koordinaadid.
// x-väärtuste massiiv genereeritakse siin valitud vahemikus; y-väärtused ja uurimisküsimuste vastused arvutab server.
// Graafik on alati nähtav: ilma valemita joonistatakse tühi teljestik, eelvaates uueneb see valemi sisestamise ajal.
(() => {
    const kast = document.querySelector('[data-graafik]');
    if (!kast) return;

    const louend = document.getElementById('funktsiooniGraafik');
    const veaKast = kast.querySelector('[data-viga]');
    const sisend = kast.dataset.sisend ? document.getElementById(kast.dataset.sisend) : null;
    const vastusteValjad = document.querySelectorAll('[data-vastused] [data-vastus]');

    const VARV = { f: '#2563eb', silt: '#4b5563', asymptoot: '#9ca3af' };
    // Punktide märgid graafikul ja legendis. kiht: väiksem on pealmine (õõnes nullkoha ring kõige peal, et see jääks
    // nähtavaks ka käänupunkti ümber); jark = järjekord legendis.
    const PUNKTILIIGID = [
        { id: 'yLoikepunkt', nimi: 'Lõikepunkt y-teljega', silt: 'Lõikepunkt y-teljega', kuju: 'rect', raadius: 4, taide: '#6b7280', joon: '#fff', jark: 5, kiht: 4 },
        { id: 'kaanupunktid', nimi: 'Käänupunkt', silt: 'Käänupunktid', kuju: 'rectRot', raadius: 6, taide: '#ea580c', joon: '#fff', jark: 4, kiht: 3 },
        { id: 'maksimumid', nimi: 'Maksimum', silt: 'Maksimumid', kuju: 'triangle', raadius: 9, taide: '#dc2626', joon: '#fff', jark: 2, kiht: 2 },
        { id: 'miinimumid', nimi: 'Miinimum', silt: 'Miinimumid', kuju: 'triangle', poore: 180, raadius: 9, taide: '#16a34a', joon: '#fff', jark: 3, kiht: 2 },
        { id: 'nullkohad', nimi: 'Nullkoht', silt: 'Nullkohad', kuju: 'circle', raadius: 7, taide: 'rgba(255, 255, 255, 0)', joon: '#2563eb', jark: 1, kiht: 1 },
    ];
    const MAX_SILTE = 12;                  // rohkemate punktide korral (nt sin(x)) koordinaate ei kirjutata

    let graafik = null;
    let paringuNr = 0;
    let kasutajaVahemik = false;          // kasutaja muutis x min/max käsitsi – ära sobita automaatselt
    let yVaade = null;                     // suumimise/liigutamise järel kasutaja valitud y-vahemik; null = automaatne
    let viimased = null;                   // viimase eduka joonistamise andmed
    const peidetud = new Set();            // legendil peidetud jooned

    const loeValem = () => (sisend ? sisend.value : kast.dataset.valem ?? '').trim();
    const vali = (nimi) => kast.querySelector(`[name="${nimi}"]`);
    const loeArv = (nimi) => parseFloat(vali(nimi).value);

    // ---------- Joonistamine ----------

    async function joonista({ sobita = false } = {}) {
        const nr = ++paringuNr;
        veaKast.textContent = '';
        const valem = loeValem();

        let xMin = loeArv('xMin'), xMax = loeArv('xMax'), samm = loeArv('samm');
        if (!Number.isFinite(xMin) || !Number.isFinite(xMax) || xMin >= xMax) return naitaViga('x min peab olema väiksem kui x max.');
        if (!Number.isFinite(samm) || samm <= 0) return naitaViga('Samm peab olema positiivne arv.');

        if (!valem) {
            viimased = null;
            taidaVastused(null);
            window.Lahendused?.taida(null, null);
            return uuendaGraafik({ xMin, xMax });
        }

        // Liiga väikese sammu korral suurendatakse seda, et graafik ikka joonistuks.
        if ((xMax - xMin) / samm > 5000) {
            samm = ilusSamm((xMax - xMin) / 1000);
            vali('samm').value = samm;
        }
        const xVaartused = genereeriX(xMin, xMax, samm);

        let andmed;
        try {
            const vastus = await fetch('/api/graafik', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ valem, x: xVaartused }),
            });
            andmed = await vastus.json();
            if (nr !== paringuNr) return;   // vahepeal alustati uut joonistamist
            if (!vastus.ok) return naitaViga(veateade(andmed, 'Graafiku andmeid ei õnnestunud laadida.'));
        } catch {
            if (nr === paringuNr) naitaViga('Serveriga ei saanud ühendust.');
            return;
        }

        // Automaatne vahemik: nullkohad, ekstreemumid ja käänupunktid peavad olema nähtavad.
        const soovitus = andmed.soovitatudVahemik;
        if (sobita && soovitus && (soovitus.xMin !== xMin || soovitus.xMax !== xMax)) {
            vali('xMin').value = soovitus.xMin;
            vali('xMax').value = soovitus.xMax;
            const laius = soovitus.xMax - soovitus.xMin;
            vali('samm').value = laius / 0.05 > 2000 ? ilusSamm(laius / 1000) : 0.05;
            return joonista();
        }

        viimased = { ...andmed, x: xVaartused };
        if (sisend) taidaVastused(andmed.vastused);
        window.Lahendused?.taida(andmed.lahendused, valem);
        uuendaGraafik({ xMin, xMax });
    }

    function naitaViga(tekst) {
        veaKast.textContent = tekst;
        viimased = null;
        if (sisend) taidaVastused(null);
        window.Lahendused?.taida(null, null);
        const xMin = loeArv('xMin'), xMax = loeArv('xMax');
        uuendaGraafik(Number.isFinite(xMin) && Number.isFinite(xMax) && xMin < xMax ? { xMin, xMax } : { xMin: -5, xMax: 5 });
    }

    function taidaVastused(vastused) {
        vastusteValjad.forEach((v) => { v.textContent = vastused?.[v.dataset.vastus] ?? '—'; });
    }

    // Loob graafiku esimesel korral, edaspidi uuendab andmeid ja telgi.
    function uuendaGraafik({ xMin, xMax }) {
        const a = viimased;
        const punktid = a ? olulisedPunktid(a, xMin, xMax) : [];
        let yMin = -10, yMax = 10;
        if (yVaade) {
            ({ yMin, yMax } = yVaade);
        } else if (a) {
            [yMin, yMax] = umarda(...yPiirid(a, punktid));
        }

        const sirge = (k, b) => [{ x: xMin, y: k * xMin + b }, { x: xMax, y: k * xMax + b }];
        const pystjoon = (x) => [{ x, y: yMin - (yMax - yMin) }, { x, y: yMax + (yMax - yMin) }];

        const andmestikud = [];
        const lisa = (id, seaded) => andmestikud.push({ id, hidden: peidetud.has(id), pointRadius: 0, ...seaded });

        if (a) {
            lisa('f', {
                label: `f(x) = ${window.Valemikuju?.tekst(a.valem) ?? a.valem}`,
                data: katkestaAsymptoodil(a.x.map((x, i) => ({ x, y: a.y[i] })), yMin, yMax),
                borderColor: VARV.f, backgroundColor: VARV.f, borderWidth: 2.5,
                order: 10,   // Chart.js joonistab suurema order'iga andmestiku esimesena – kõver jääb punktide alla
            });

            a.asumptoodid.forEach((s, i) => lisa(`asymptoot${i}`, {
                label: 'Asümptoodid',
                data: s.vertikaalne ? pystjoon(s.x) : sirge(s.k, s.b),
                borderColor: VARV.asymptoot, backgroundColor: VARV.asymptoot, borderWidth: 1, borderDash: [4, 4], order: 11,
            }));

            // Iga punktiliik eraldi andmestikus: oma märk, värv ja legendi kirje (legendil saab neid peita).
            // Punkt on igas andmestikus, kuhu ta kuulub (nt (0; 0) on nii nullkoht kui ka käänupunkt), aga koordinaadid
            // kirjutatakse ainult tema peamise liigi juurde.
            PUNKTILIIGID.forEach((liik) => {
                const data = punktid.filter((p) => p.liigid.includes(liik.nimi))
                    .map((p) => (p.liik === liik.nimi ? p : { ...p, silt: null }));
                if (data.length === 0) return;
                lisa(liik.id, {
                    type: 'scatter', label: liik.silt, data, sildid: true, jark: liik.jark, order: liik.kiht,
                    pointStyle: liik.kuju, rotation: liik.poore ?? 0, pointRadius: liik.raadius, pointHoverRadius: liik.raadius + 2,
                    backgroundColor: liik.taide, borderColor: liik.joon, borderWidth: 2,
                });
            });
        }

        const nulljoon = (ctx) => (ctx.tick && ctx.tick.value === 0 ? '#6b7280' : '#eeeeee');
        const skaalad = {
            // includeBounds: false – suumitud vaate servaväärtusi (nt 68.7) ei kirjutata teljele, ainult ilusad jaotised.
            x: { type: 'linear', min: xMin, max: xMax, title: { display: true, text: 'x' }, grid: { color: nulljoon }, ticks: { includeBounds: false } },
            y: { min: yMin, max: yMax, title: { display: true, text: 'f(x)' }, grid: { color: nulljoon }, ticks: { includeBounds: false } },
        };

        if (graafik) {
            graafik.data.datasets = andmestikud;
            graafik.options.scales = skaalad;
            graafik.update();
            return;
        }

        graafik = new Chart(louend, {
            type: 'line',
            data: { datasets: andmestikud },
            plugins: [punktiSildid],
            options: {
                maintainAspectRatio: false,
                animation: false,
                parsing: true,
                interaction: { mode: 'nearest', intersect: false },
                scales: skaalad,
                plugins: {
                    legend: {
                        labels: {
                            usePointStyle: true,
                            boxHeight: 8,
                            sort: (a, b, andmed) => (andmed.datasets[a.datasetIndex].jark ?? 0) - (andmed.datasets[b.datasetIndex].jark ?? 0),
                            // Mitu asümptooti näidatakse legendis ühe kirjena.
                            filter: (kirje, andmed) => andmed.datasets.findIndex((d) => d.label === kirje.text) === kirje.datasetIndex,
                        },
                        onClick: (e, kirje, legend) => {
                            const ds = legend.chart.data.datasets;
                            const id = ds[kirje.datasetIndex].id;
                            const peida = !peidetud.has(id);
                            const grupp = id.startsWith('asymptoot') ? ds.filter((d) => d.id.startsWith('asymptoot')) : [ds[kirje.datasetIndex]];
                            grupp.forEach((d) => { d.hidden = peida; peida ? peidetud.add(d.id) : peidetud.delete(d.id); });
                            legend.chart.update();
                        },
                    },
                    tooltip: {
                        // Mitmes andmestikus olev punkt (nt nullkoht ja käänupunkt) näidatakse vihjes üks kord.
                        filter: (c) => !c.dataset.id.startsWith('asymptoot') && c.raw?.silt !== null,
                        callbacks: {
                            label: (c) => c.raw.liigid
                                ? `${c.raw.liigid.join(', ')}: ${c.raw.silt}`
                                : `f(${ymarda(c.parsed.x)}) = ${ymarda(c.parsed.y)}`,
                        },
                    },
                    // Suumimine (kerimisratas, kahe sõrmega, Shift + lohistamine alale) ja liigutamine (lohistamine).
                    // Lõpus arvutatakse graafik uue vahemiku jaoks uuesti, et joon ulatuks üle kogu nähtava ala.
                    zoom: {
                        limits: {
                            x: { min: -1e6, max: 1e6, minRange: 1e-3 },
                            y: { min: -1e9, max: 1e9, minRange: 1e-3 },
                        },
                        pan: { enabled: true, mode: 'xy', onPanComplete: vaadeMuutus },
                        zoom: {
                            mode: 'xy',
                            wheel: { enabled: true },
                            pinch: { enabled: true },
                            drag: { enabled: true, modifierKey: 'shift', backgroundColor: 'rgba(37, 99, 235, 0.1)', borderColor: VARV.f, borderWidth: 1 },
                            onZoomComplete: vaadeMuutus,
                        },
                    },
                },
            },
        });
    }

    // Olulised punktid; samas kohas olevad punktid (nt nullkoht ja miinimum (0; 0)) liidetakse üheks.
    // Punkti liik (märk graafikul) on esimene liik: ekstreemum > nullkoht > käänupunkt > lõikepunkt y-teljega (lisamise järjekord).
    function olulisedPunktid(a, xMin, xMax) {
        const kohad = new Map();
        const lisa = (x, y, liik, asend) => {
            if (y === null || !Number.isFinite(y) || x < xMin || x > xMax) return;
            const voti = x.toFixed(4);
            const p = kohad.get(voti);
            if (p) {
                if (!p.liigid.includes(liik)) p.liigid.push(liik);
            } else {
                const ees = liik === 'Maksimum' ? 'max ' : liik === 'Miinimum' ? 'min ' : '';
                kohad.set(voti, { x, y, liik, liigid: [liik], asend, silt: `${ees}(${ymarda(x)}; ${ymarda(y)})` });
            }
        };
        a.ekstreemumid.forEach((e) => lisa(e.x, e.y, e.tyyp === 'max' ? 'Maksimum' : 'Miinimum', e.tyyp === 'max' ? 'ules' : 'alla'));
        a.nullkohad.forEach((x) => lisa(x, 0, 'Nullkoht', 'kylg'));
        a.kaanupunktid.forEach((p) => lisa(p.x, p.y, 'Käänupunkt', 'kylg'));
        if (a.yLoikepunkt !== null) lisa(0, a.yLoikepunkt, 'Lõikepunkt y-teljega', 'kylg');
        return [...kohad.values()].sort((p, q) => p.x - q.x);
    }

    // Kirjutab oluliste punktide juurde koordinaadid: maksimumi kohale, miinimumi alla, teised paremale.
    const punktiSildid = {
        id: 'punktiSildid',
        afterDatasetsDraw(chart) {
            const { ctx, chartArea: ala } = chart;
            const elemendid = chart.data.datasets.flatMap((ds, i) => (ds.sildid && chart.isDatasetVisible(i)
                ? chart.getDatasetMeta(i).data.map((el, j) => ({ el, p: ds.data[j] }))
                : []))
                .filter(({ el, p }) => p.silt && el.x >= ala.left && el.x <= ala.right && el.y >= ala.top && el.y <= ala.bottom);
            if (elemendid.length > MAX_SILTE) return;

            ctx.save();
            ctx.font = '12px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
            ctx.fillStyle = VARV.silt;
            elemendid.forEach(({ el, p }) => {
                if (p.asend === 'ules') {
                    ctx.textAlign = 'center'; ctx.textBaseline = 'bottom';
                    ctx.fillText(p.silt, el.x, el.y - 14);
                } else if (p.asend === 'alla') {
                    ctx.textAlign = 'center'; ctx.textBaseline = 'top';
                    ctx.fillText(p.silt, el.x, el.y + 14);
                } else {
                    const laius = ctx.measureText(p.silt).width;
                    const paremale = el.x + 8 + laius <= ala.right;
                    ctx.textAlign = paremale ? 'left' : 'right'; ctx.textBaseline = 'bottom';
                    ctx.fillText(p.silt, paremale ? el.x + 8 : el.x - 8, el.y - 4);
                }
            });
            ctx.restore();
        },
    };

    // ---------- Suumimine ja liigutamine ----------

    // Suumi- või liigutusplugina lõpus: uus vahemik vormi väljadele ja andmed uuesti serverist.
    function vaadeMuutus({ chart }) {
        seaVaade(chart.scales.x.min, chart.scales.x.max, chart.scales.y.min, chart.scales.y.max);
    }

    function seaVaade(xMin, xMax, yMin, yMax) {
        const laius = xMax - xMin;
        const komakohti = Math.max(0, Math.ceil(-Math.log10(laius)) + 3);
        vali('xMin').value = Number(xMin.toFixed(komakohti));
        vali('xMax').value = Number(xMax.toFixed(komakohti));
        vali('samm').value = ilusSamm(laius / 800);
        yVaade = { yMin, yMax };
        kasutajaVahemik = true;
        joonista();
    }

    // Nupud + ja −: suumib graafiku keskpunkti ümber.
    function suumi(tegur) {
        if (!graafik) return;
        const { x, y } = graafik.scales;
        const xKesk = (x.min + x.max) / 2, yKesk = (y.min + y.max) / 2;
        const xPool = (x.max - x.min) / 2 / tegur, yPool = (y.max - y.min) / 2 / tegur;
        if (xPool * 2 < 1e-3 || yPool * 2 < 1e-3 || xPool > 1e6) return;
        seaVaade(xKesk - xPool, xKesk + xPool, yKesk - yPool, yKesk + yPool);
    }

    // Tagasi automaatsele vaatele: nullkohad, ekstreemumid ja käänupunktid nähtavad, y-telg nende järgi.
    function lahtesta() {
        kasutajaVahemik = false;
        yVaade = null;
        joonista({ sobita: true });
    }

    // ---------- Abifunktsioonid ----------

    const ymarda = (v) => Number(v.toFixed(3)).toString();

    function veateade(andmed, vaikimisi) {
        if (andmed?.viga) return andmed.viga;
        const vead = andmed?.errors && Object.values(andmed.errors).flat();
        return vead?.length ? vead.join(' ') : andmed?.title ?? vaikimisi;
    }

    function genereeriX(xMin, xMax, samm) {
        const arv = Math.floor((xMax - xMin) / samm + 1e-9) + 1;
        const x = [];
        for (let i = 0; i < arv; i++) x.push(Number((xMin + i * samm).toFixed(10)));
        if (x[x.length - 1] < xMax - 1e-9) x.push(xMax);
        return x;
    }

    // 1, 2 või 5 korda kümne aste.
    function ilusSamm(v) {
        const aste = Math.pow(10, Math.floor(Math.log10(v)));
        const m = v / aste;
        return Number(((m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * aste).toPrecision(6));
    }

    // y-telje ulatus. Kui funktsioonil on ekstreemumid või käänupunktid (ja pole vertikaalseid asümptoote),
    // valitakse telg oluliste punktide järgi – muidu nt x^4 - 4*x^3 puhul kasvaks telg servades sadadesse ja
    // miinimum (3; -27) jääks peaaegu nähtamatuks. Muul juhul kogu kõvera järgi.
    function yPiirid(a, punktid) {
        const [alumine, ulemine] = kooveraPiirid(a.y);
        const onKuju = punktid.some((p) => p.liigid.some((l) => l !== 'Nullkoht' && l !== 'Lõikepunkt y-teljega'));
        if (!onKuju || a.asumptoodid.some((s) => s.vertikaalne)) return [alumine, ulemine];

        const y = punktid.map((p) => p.y);
        a.asumptoodid.filter((s) => s.k === 0).forEach((s) => y.push(s.b));
        const pMin = Math.min(...y), pMax = Math.max(...y), ulatus = pMax - pMin;
        if (ulatus < 1e-9 || ulemine - alumine <= 4 * ulatus) return [alumine, ulemine];
        return [Math.max(alumine, pMin - ulatus), Math.min(ulemine, pMax + ulatus)];
    }

    // Kõvera y-ulatus: tavaliselt kõik väärtused; asümptootide korral lõigatakse äärmused ära.
    function kooveraPiirid(vaartused) {
        const arvud = vaartused.filter((v) => v !== null && Number.isFinite(v)).sort((a, b) => a - b);
        if (arvud.length === 0) return [Infinity, -Infinity];
        const kvantiil = (p) => arvud[Math.floor(p * (arvud.length - 1))];
        const q05 = kvantiil(0.05), q95 = kvantiil(0.95);
        const alumine = arvud[0], ulemine = arvud[arvud.length - 1];
        if (ulemine - alumine <= 5 * (q95 - q05)) return [alumine, ulemine];
        const varu = (q95 - q05) * 0.5;
        return [Math.max(alumine, q05 - varu), Math.min(ulemine, q95 + varu)];
    }

    // Lisab varu ja ümardab telje otspunktid ilusateks arvudeks.
    function umarda(alumine, ulemine) {
        if (!Number.isFinite(alumine) || !Number.isFinite(ulemine)) return [-10, 10];
        if (ulemine - alumine < 1e-9) { alumine -= 1; ulemine += 1; }
        const varu = (ulemine - alumine) * 0.08;
        const jark = Math.pow(10, Math.floor(Math.log10(ulemine - alumine))) / 2;
        return [Math.floor((alumine - varu) / jark) * jark, Math.ceil((ulemine + varu) / jark) * jark];
    }

    // Ei ühenda punkte, mis asuvad vertikaalse asümptoodi eri pooltel.
    function katkestaAsymptoodil(punktid, alumine, ulemine) {
        for (let i = 1; i < punktid.length; i++) {
            const a = punktid[i - 1].y, b = punktid[i].y;
            if (a === null || b === null) continue;
            if ((a > ulemine && b < alumine) || (a < alumine && b > ulemine)) {
                punktid[i - 1] = { x: punktid[i - 1].x, y: null };
            }
        }
        return punktid;
    }

    // ---------- Sündmused ----------

    kast.querySelector('[data-joonista]').addEventListener('click', () => joonista());
    kast.querySelector('[data-sobita]').addEventListener('click', lahtesta);
    kast.querySelector('[data-suurenda]').addEventListener('click', () => suumi(1.5));
    kast.querySelector('[data-vahenda]').addEventListener('click', () => suumi(1 / 1.5));
    louend.addEventListener('dblclick', lahtesta);
    kast.querySelectorAll('input').forEach((i) => {
        i.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); joonista(); } });
        // Käsitsi muudetud x-vahemiku korral valitakse y-telg jälle automaatselt.
        i.addEventListener('change', () => { if (i.name !== 'samm') { kasutajaVahemik = true; yVaade = null; } joonista(); });
    });

    // Päise lüliti „Ilus valem“ muudab legendi valemi kuju.
    window.Valemikuju?.kuula(() => { if (viimased) uuendaGraafik({ xMin: loeArv('xMin'), xMax: loeArv('xMax') }); });

    if (sisend) {
        // Graafik ja vastused uuenevad valemi sisestamise ajal (väike viivitus, et iga klahvivajutus ei tekitaks päringut).
        let taimer = null;
        sisend.addEventListener('input', () => {
            clearTimeout(taimer);
            taimer = setTimeout(() => joonista({ sobita: !kasutajaVahemik }), 350);
        });
    }

    joonista({ sobita: true });
})();
