// Lahenduskäigud vastuste juures (nagu Photomathis): nupp „Lahendus“ avab sammud, mitme lahendusviisi korral vahelehed.
// Andmed tulevad graafiku päringuga (/api/graafik → lahendused); graafik.js kutsub Lahendused.taida(...).
// Tekstis on valemid kujul $...$ ja rõhutus kujul **...**; valemid joonistab KaTeX (kui see ei laadinud, jääb LaTeX tekstina).
// Viisi „Graafikult“ sammudes on väike graafik (Chart.js): f(x), nullkohad ja soovi korral varjutatud märgipiirkond.
window.Lahendused = (() => {
    const kast = document.querySelector('[data-vastused]');
    if (!kast) return { taida() {} };

    let andmed = null;
    let valem = null;
    const valitudViis = new Map();   // küsimuse võti → valitud lahendusviisi indeks

    // ---------- Andmed ----------

    function taida(lahendused, uusValem) {
        const sama = uusValem === valem;
        andmed = lahendused ?? null;
        valem = uusValem ?? null;
        if (!sama) valitudViis.clear();

        kast.querySelectorAll('[data-lahendusnupp]').forEach((nupp) => {
            const voti = nupp.dataset.lahendusnupp;
            const olemas = !!andmed?.[voti];
            nupp.hidden = !olemas;
            const paneel = paneelKusimusele(voti);
            if (!olemas) {
                ava(nupp, paneel, false);
            } else if (!paneel.hidden && !sama) {
                joonista(paneel, voti);   // valem muutus – avatud lahendus uueneb
            }
        });
    }

    const paneelKusimusele = (voti) => kast.querySelector(`[data-lahendus="${voti}"]`);

    function ava(nupp, paneel, lahti) {
        nupp.setAttribute('aria-expanded', String(lahti));
        nupp.textContent = lahti ? 'Peida lahendus' : 'Lahendus';
        nupp.classList.toggle('avatud', lahti);
        paneel.hidden = !lahti;
        if (!lahti) tyhjenda(paneel);
    }

    kast.addEventListener('click', (e) => {
        const nupp = e.target.closest('[data-lahendusnupp]');
        if (!nupp) return;
        const voti = nupp.dataset.lahendusnupp;
        const paneel = paneelKusimusele(voti);
        const lahti = paneel.hidden;
        ava(nupp, paneel, lahti);
        if (lahti) joonista(paneel, voti);
    });

    // ---------- Joonistamine ----------

    function joonista(paneel, voti) {
        const lahendus = andmed?.[voti];
        tyhjenda(paneel);
        if (!lahendus) return;

        const viisid = lahendus.viisid;
        let indeks = Math.min(valitudViis.get(voti) ?? 0, viisid.length - 1);

        const sisu = el('div', 'lahendus-sisu');
        if (viisid.length > 1) {
            const vahelehed = el('div', 'viisid');
            vahelehed.setAttribute('role', 'tablist');
            vahelehed.setAttribute('aria-label', 'Lahendusviisid');
            viisid.forEach((viis, i) => {
                const leht = el('button', 'viis');
                leht.type = 'button';
                leht.setAttribute('role', 'tab');
                leht.setAttribute('aria-selected', String(i === indeks));
                leht.append(el('span', 'viisi-nr', `${i + 1}. viis`));
                const nimi = el('span', 'viisi-nimi');
                kirjuta(nimi, viis.nimi);
                leht.append(nimi);
                leht.addEventListener('click', () => {
                    valitudViis.set(voti, i);
                    joonista(paneel, voti);
                });
                vahelehed.append(leht);
            });
            paneel.append(vahelehed);
        } else {
            const pealkiri = el('div', 'viisi-pealkiri');
            kirjuta(pealkiri, viisid[0].nimi);
            paneel.append(pealkiri);
        }

        const sammud = el('ol', 'sammud');
        viisid[indeks].sammud.forEach((samm) => sammud.append(sammuElement(samm)));
        sisu.append(sammud);
        paneel.append(sisu);
    }

    function sammuElement(samm) {
        const li = el('li', samm.tekst.startsWith('Vastus') ? 'samm vastus' : 'samm');
        if (samm.tekst) {
            const p = el('p');
            kirjuta(p, samm.tekst);
            li.append(p);
        }
        if (samm.joonis) li.append(joonis(samm.joonis));
        if (samm.valem) {
            const valem = el('div', 'valem');
            matemaatika(valem, samm.valem, true);
            li.append(valem);
        }
        if (samm.tabel) li.append(tabel(samm.tabel));
        return li;
    }

    function tabel(read) {
        const umbris = el('div', 'margitabel');
        const t = el('table');
        read.forEach((rida, r) => {
            const tr = el('tr');
            rida.forEach((lahter) => {
                const td = el(r === 0 ? 'th' : 'td');
                kirjuta(td, lahter);
                tr.append(td);
            });
            (r === 0 ? (t.tHead ?? t.createTHead()) : (t.tBodies[0] ?? t.createTBody())).append(tr);
        });
        umbris.append(t);
        return umbris;
    }

    // Paneeli joonised kustutatakse enne uut sisu, et Chart.js ei hoiaks vanu graafikuid alles.
    function tyhjenda(paneel) {
        paneel.querySelectorAll('canvas').forEach((c) => window.Chart?.getChart(c)?.destroy());
        paneel.replaceChildren();
    }

    // ---------- Joonis ----------

    const JOONISE_VARV = { f: '#2563eb', silt: '#4b5563', asymptoot: '#9ca3af', pluss: 'rgba(22, 163, 74, 0.2)', miinus: 'rgba(220, 38, 38, 0.18)' };

    function joonis(j) {
        const umbris = el('div', 'lahendusjoonis');
        if (!window.Chart) return umbris;   // Chart.js ei laadinud
        const louend = el('canvas');
        louend.setAttribute('role', 'img');
        louend.setAttribute('aria-label', 'Funktsiooni graafik');
        umbris.append(louend);

        const andmestikud = [{
            data: j.x.map((x, i) => ({ x, y: j.y[i] })),
            borderColor: JOONISE_VARV.f, borderWidth: 2, pointRadius: 0, order: 2,
            fill: j.varjuta ? {
                target: { value: 0 },
                above: j.varjuta > 0 ? JOONISE_VARV.pluss : 'transparent',
                below: j.varjuta < 0 ? JOONISE_VARV.miinus : 'transparent',
            } : false,
        }];
        j.asumptoodid.forEach((x) => andmestikud.push({
            data: [{ x, y: j.yMin }, { x, y: j.yMax }],
            borderColor: JOONISE_VARV.asymptoot, borderWidth: 1, borderDash: [4, 4], pointRadius: 0, order: 3,
        }));
        if (j.margid.length) andmestikud.push({
            type: 'scatter', data: j.margid, order: 1, sildid: true,
            pointStyle: 'circle', pointRadius: 5, backgroundColor: '#fff', borderColor: JOONISE_VARV.f, borderWidth: 2,
        });

        const nulljoon = (ctx) => (ctx.tick && ctx.tick.value === 0 ? '#6b7280' : '#eeeeee');
        new window.Chart(louend, {
            type: 'line',
            data: { datasets: andmestikud },
            plugins: [jooniseSildid],
            options: {
                maintainAspectRatio: false,
                animation: false,
                events: [],
                scales: {
                    x: { type: 'linear', min: j.xMin, max: j.xMax, grid: { color: nulljoon }, ticks: { includeBounds: false } },
                    y: { min: j.yMin, max: j.yMax, grid: { color: nulljoon }, ticks: { includeBounds: false } },
                },
                plugins: { legend: { display: false }, tooltip: { enabled: false } },
            },
        });
        return umbris;
    }

    // Kirjutab esile tõstetud punktide juurde sildi (nt x = 2) x-telje kohale; kui see jääks eelmise sildi peale
    // (lähedased nullkohad), siis x-telje alla.
    const jooniseSildid = {
        id: 'jooniseSildid',
        afterDatasetsDraw(chart) {
            const { ctx } = chart;
            ctx.save();
            ctx.font = '12px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
            ctx.fillStyle = JOONISE_VARV.silt;
            ctx.textAlign = 'center';
            let eelmiseLopp = -Infinity, ules = true;
            chart.data.datasets.forEach((ds, i) => {
                if (!ds.sildid) return;
                chart.getDatasetMeta(i).data.forEach((punkt, k) => {
                    const silt = ds.data[k].silt;
                    if (!silt) return;
                    const laius = ctx.measureText(silt).width;
                    ules = punkt.x - laius / 2 > eelmiseLopp + 4 ? true : !ules;
                    ctx.textBaseline = ules ? 'bottom' : 'top';
                    ctx.fillText(silt, punkt.x, ules ? punkt.y - 9 : punkt.y + 9);
                    eelmiseLopp = Math.max(eelmiseLopp, punkt.x + laius / 2);
                });
            });
            ctx.restore();
        },
    };

    // Tekst koos $valemite$ ja **rõhutusega**; kõik tekst lisatakse tekstisõlmedena (mitte HTML-ina).
    function kirjuta(sihtkoht, tekst) {
        const osad = tekst.split(/(\$[^$]+\$|\*\*[^*]+\*\*)/g);
        osad.forEach((osa) => {
            if (!osa) return;
            if (osa.startsWith('$') && osa.endsWith('$') && osa.length > 1) {
                const span = el('span', 'valem-reas');
                matemaatika(span, osa.slice(1, -1), false);
                sihtkoht.append(span);
            } else if (osa.startsWith('**') && osa.endsWith('**')) {
                const b = el('strong');
                kirjuta(b, osa.slice(2, -2));
                sihtkoht.append(b);
            } else {
                sihtkoht.append(document.createTextNode(osa));
            }
        });
    }

    function matemaatika(sihtkoht, latex, eraldi) {
        if (window.katex) {
            try {
                window.katex.render(latex, sihtkoht, { displayMode: eraldi, throwOnError: false, strict: 'ignore' });
                return;
            } catch {
                // langeb tekstiks
            }
        }
        sihtkoht.textContent = latex;
        sihtkoht.classList.add('valem-tekstina');
    }

    function el(silt, klass, tekst) {
        const e = document.createElement(silt);
        if (klass) e.className = klass;
        if (tekst !== undefined) e.textContent = tekst;
        return e;
    }

    return { taida };
})();
