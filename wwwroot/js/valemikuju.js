// Valemite kuju lüliti (päises): „Ilus valem“ näitab valemeid matemaatikana (x³ − 3x, murrujoon, √), väljas on need
// kirjutatud kujul (x^3 - 3*x). Mõjutab nimekirja valemeid, funktsiooni pealkirja ja graafiku legendi – mitte vastuseid
// ega lahenduskäike. Valemi elemendid: <code data-ilus-valem>x^3 - 3*x</code>. Valik jäetakse brauserisse meelde.
window.Valemikuju = (() => {
    const VOTI = 'valemid.ilus';
    const kuulajad = [];
    let ilus = true;
    try { ilus = localStorage.getItem(VOTI) !== '0'; } catch { /* privaatne aken – vaikimisi sees */ }

    // Tekstivalem → LaTeX (valemiteisendus.js) koos kooliliku viimistlusega: \exp(-x) → e^{-x}, 1.0x → x.
    function latexiks(valem) {
        const teisendus = window.Valemiteisendus?.valemLatexiks;
        let latex = teisendus ? teisendus(valem) : null;
        if (latex === null || latex === undefined) return null;
        latex = latex.replace(/(^|[^\d.])1\.0 (?=[a-z\\])/g, '$1');
        for (;;) {
            const algus = latex.indexOf('\\exp\\left(');
            if (algus < 0) break;
            let sygavus = 0, lopp = -1;
            for (let i = algus + 10; i < latex.length; i++) {
                if (latex.startsWith('\\left(', i)) sygavus++;
                else if (latex.startsWith('\\right)', i)) {
                    if (sygavus === 0) { lopp = i; break; }
                    sygavus--;
                }
            }
            if (lopp < 0) break;
            latex = `${latex.slice(0, algus)}e^{${latex.slice(algus + 10, lopp)}}${latex.slice(lopp + 7)}`;
        }
        return latex;
    }

    // LaTeX → tekst unicode-märkidega (graafiku legendi jaoks, kuhu valemit joonistada ei saa): x^{3}-3 x → x³ − 3x.
    const YLA = { 0: '⁰', 1: '¹', 2: '²', 3: '³', 4: '⁴', 5: '⁵', 6: '⁶', 7: '⁷', 8: '⁸', 9: '⁹', '-': '⁻', '+': '⁺', n: 'ⁿ', x: 'ˣ' };
    const KASUD = { pi: 'π', cdot: '·', sin: 'sin', cos: 'cos', tan: 'tan', ln: 'ln', exp: 'exp', infty: '∞' };

    function unicode(latex) {
        let i = 0;
        const lihtne = (s) => /^[\p{L}\p{N}.π]+$/u.test(s);
        const sulgu = (s) => (lihtne(s) ? s : `(${s})`);

        function grupp() {
            if (latex[i] !== '{') return element();
            i++;
            let s = '';
            while (i < latex.length && latex[i] !== '}') s += element();
            i++;
            return s;
        }

        function element() {
            const c = latex[i];
            if (c === '\\') {
                i++;
                const nimi = /[a-zA-Z]/.test(latex[i]) ? latex.slice(i).match(/^[a-zA-Z]+/)[0] : latex[i];
                i += nimi.length;
                if (nimi === 'frac') { const a = grupp(), b = grupp(); return `${sulgu(a)}/${sulgu(b)}`; }
                if (nimi === 'sqrt') {
                    let n = '';
                    if (latex[i] === '[') { const lopp = latex.indexOf(']', i); n = latex.slice(i + 1, lopp); i = lopp + 1; }
                    const a = grupp();
                    return `${[...n].map((m) => YLA[m] ?? m).join('')}√${sulgu(a)}`;
                }
                if (nimi === 'left' || nimi === 'right') { const s = latex[i]; i++; return s === '.' ? '' : s; }
                return KASUD[nimi] ?? nimi;
            }
            if (c === '^') {
                i++;
                const a = grupp();
                return [...a].every((m) => m in YLA) ? [...a].map((m) => YLA[m]).join('') : `^${sulgu(a)}`;
            }
            if (c === '{') return grupp();
            i++;
            return c;
        }

        let s = '';
        while (i < latex.length) s += element();
        return s
            .replace(/(\d) (?=[\p{L}(√π])/gu, '$1')                       // 3 x → 3x
            .replace(/(?<=[\p{L}⁰¹²³⁴⁵⁶⁷⁸⁹ⁿˣ)]) (?=[\p{L}(√])/gu, '')       // x e⁻ˣ → xe⁻ˣ
            .replace(/(?<=[\p{L}\p{N})⁰¹²³⁴⁵⁶⁷⁸⁹ⁿˣπ]) ?([+-]) ?/gu, ' $1 ')  // binaarne + ja - tühikutega
            .replace(/-/g, '−');
    }

    // Graafiku legendi tekst (valitud kujul).
    function tekst(valem) {
        if (!ilus) return valem;
        const latex = latexiks(valem);
        return latex === null ? valem : unicode(latex);
    }

    function joonistaElement(el) {
        const valem = el.dataset.ilusValem || el.textContent;
        el.dataset.ilusValem = valem;
        const latex = ilus && window.katex ? latexiks(valem) : null;
        el.classList.toggle('valem-ilus', latex !== null);
        if (latex === null) {
            el.textContent = valem;
            return;
        }
        try {
            window.katex.render(latex, el, { throwOnError: true, strict: 'ignore' });
        } catch {
            el.textContent = valem;
            el.classList.remove('valem-ilus');
        }
    }

    const joonista = () => document.querySelectorAll('[data-ilus-valem]').forEach(joonistaElement);

    const lyliti = document.querySelector('[data-valemikuju]');
    if (lyliti) {
        lyliti.checked = ilus;
        lyliti.addEventListener('change', () => {
            ilus = lyliti.checked;
            try { localStorage.setItem(VOTI, ilus ? '1' : '0'); } catch { /* ei salvestu */ }
            joonista();
            kuulajad.forEach((k) => k());
        });
    }
    joonista();

    return { tekst, kuula: (k) => kuulajad.push(k) };
})();
