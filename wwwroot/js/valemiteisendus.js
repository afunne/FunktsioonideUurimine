// Teisendused visuaalse valemivälja (MathLive, LaTeX) ja tekstivalemi (nt "(x + 2)/(x - 1)") vahel.
//   latexValemiks('\\frac{x^{2}}{3}-4x')  →  'x^2/3 - 4*x'
//   valemLatexiks('(x + 2)/(x - 1)')       →  '\\frac{x+2}{x-1}'
(function (eksport) {
    const FUNKTSIOONID = ['sqrt', 'sin', 'cos', 'tan', 'exp', 'ln'];
    const NIMED = [...FUNKTSIOONID, 'pi'];   // pikemad enne lühemaid pole vaja: ükski ei ole teise algus
    const VAHED = new Set(['\\,', '\\;', '\\:', '\\!', '\\ ', '\\quad', '\\qquad', '\\left.', '\\right.']);

    // ---------- LaTeX → tekstivalem ----------

    function latexiTokenid(latex) {
        const tokenid = [];
        let i = 0;
        while (i < latex.length) {
            const m = latex[i];
            if (/\s/.test(m)) { i++; continue; }
            if (m === '\\') {
                const nimi = /^\\([a-zA-Z]+)/.exec(latex.slice(i));
                if (nimi) { tokenid.push(nimi[0]); i += nimi[0].length; }
                else { tokenid.push(latex.slice(i, i + 2)); i += 2; }
                continue;
            }
            const arv = /^\d+(\.\d+)?|^\.\d+/.exec(latex.slice(i));
            if (arv) { tokenid.push(arv[0]); i += arv[0].length; continue; }
            tokenid.push(m);
            i++;
        }
        return tokenid;
    }

    const onArv = (s) => /^\d*\.?\d+$/.test(s);
    const onNimi = (s) => /^[a-z]+$/.test(s);
    const onTaht = (s) => /^[a-zA-Z]$/.test(s);

    // Kas s on terviklikult sulgudes, nt "(x + 1)" (aga mitte "(x)/(y)").
    function onSulgudes(s) {
        if (!s.startsWith('(') || !s.endsWith(')')) return false;
        let sygavus = 0;
        for (let i = 0; i < s.length; i++) {
            if (s[i] === '(') sygavus++;
            else if (s[i] === ')') sygavus--;
            if (sygavus === 0 && i < s.length - 1) return false;
        }
        return true;
    }

    const onFunktsioon = (s) => /^[a-z]+\(/.test(s) && onSulgudes(s.slice(s.indexOf('(')));
    const lihtne = (s) => onArv(s) || onNimi(s) || onSulgudes(s);

    // Kas s on üksainus aste lihtsatest osadest, nt "x^3" või "(x + 1)^(1/2)" – murru lugejas ei vaja sulge.
    function onAste(s) {
        let sygavus = 0, koht = -1;
        for (let i = 0; i < s.length; i++) {
            if (s[i] === '(') sygavus++;
            else if (s[i] === ')') sygavus--;
            else if (sygavus === 0 && /[-+*/ ]/.test(s[i])) return false;
            else if (sygavus === 0 && s[i] === '^') { if (koht >= 0) return false; koht = i; }
        }
        return koht > 0 && lihtne(s.slice(0, koht)) && lihtne(s.slice(koht + 1));
    }
    const sulgu = (s, tingimus) => (tingimus(s) ? s : `(${s})`);

    function latexValemiks(latex) {
        const t = latexiTokenid(latex || '');
        let i = 0;
        const vaata = () => t[i];
        const vota = () => t[i++];
        const jataVahed = () => { while (i < t.length && VAHED.has(t[i])) i++; };

        // Järjestikused tähed, mis moodustavad funktsiooni nime (nt kui MathLive jättis "sin" tähtedeks).
        function loeNimi(esimene) {
            for (const nimi of NIMED) {
                if (!nimi.startsWith(esimene)) continue;
                if (t.slice(i, i + nimi.length - 1).join('') === nimi.slice(1)) {
                    i += nimi.length - 1;
                    return nimi;
                }
            }
            return null;
        }

        // Argument: {…}, üks märk/arv või üks käsk.
        function argument() {
            jataVahed();
            if (vaata() === '{') {
                vota();
                const sisu = jada((x) => x === '}');
                vota();
                return sisu;
            }
            const tok = vota();
            if (tok === undefined) return '';
            const a = aatom(tok);
            return a ? a.s : '';
        }

        function funktsioon(nimi) {
            let aste = null;
            jataVahed();
            if (vaata() === '^') { vota(); aste = argument(); }
            jataVahed();
            let arg;
            const jargmine = vaata();
            if (jargmine === undefined || ['}', ']', ')', '\\right', '\\mright', '+', '-', '*', '/'].includes(jargmine)) {
                arg = '()';                                  // argument puudub (veel)
            } else if (jargmine === '(' || jargmine === '\\left' || jargmine === '\\mleft') {
                arg = aatom(vota()).s;                       // juba sulgudes
            } else if (jargmine === '{') {
                arg = `(${argument()})`;
            } else {
                const tok = vota();
                let a = tok === undefined ? '' : (aatom(tok)?.s ?? '');
                while (vaata() === '^') { vota(); a = `${sulgu(a, lihtne)}^${sulgu(argument(), (s) => onArv(s) || onNimi(s))}`; }
                arg = `(${a})`;
            }
            const s = nimi + arg;
            return aste === null
                ? { s, liit: false }
                : { s: `(${s})^${sulgu(aste, (x) => onArv(x) || onNimi(x))}`, liit: true };
        }

        // Üks väärtus. liit = sisaldab tehtemärke ilma ümbritsevate sulgudeta (nt murd "a/b").
        function aatom(tok) {
            if (onArv(tok)) return { s: tok, liit: false };
            if (onTaht(tok)) {
                const nimi = loeNimi(tok);
                if (nimi === 'pi') return { s: 'pi', liit: false };
                if (nimi) return funktsioon(nimi);
                return { s: tok.toLowerCase(), liit: false };
            }
            if (tok === '{') {
                const sisu = jada((x) => x === '}');
                vota();
                return { s: lihtne(sisu) ? sisu : `(${sisu})`, liit: false };
            }
            if (tok === '(' || tok === '[') {
                const sulg = tok === '(' ? ')' : ']';
                const sisu = jada((x) => x === sulg);
                if (vaata() === sulg) vota();
                return { s: `(${sisu})`, liit: false };
            }
            if (tok === '\\left' || tok === '\\mleft') {
                const eraldaja = vota();
                const sisu = jada((x) => x === '\\right' || x === '\\mright');
                vota();
                vota();                                    // parem eraldaja
                return { s: eraldaja === '|' ? `abs(${sisu})` : `(${sisu})`, liit: false };
            }
            if (tok === '|') {
                const sisu = jada((x) => x === '|');
                vota();
                return { s: `abs(${sisu})`, liit: false };
            }
            if (tok === '\\frac' || tok === '\\dfrac' || tok === '\\tfrac') {
                const lugeja = argument(), nimetaja = argument();
                const murruosa = (s) => sulgu(s, (x) => lihtne(x) || onFunktsioon(x) || onAste(x));
                return { s: `${murruosa(lugeja)}/${murruosa(nimetaja)}`, liit: true };
            }
            if (tok === '\\sqrt') {
                let astendaja = null;
                if (vaata() === '[') {
                    vota();
                    astendaja = jada((x) => x === ']');
                    vota();
                }
                const arg = argument();
                return astendaja
                    ? { s: `${sulgu(arg, lihtne)}^(1/${sulgu(astendaja, lihtne)})`, liit: true }
                    : { s: `sqrt(${arg})`, liit: false };
            }
            if (tok === '\\placeholder') {
                if (vaata() === '[') { while (i < t.length && vota() !== ']'); }
                return argument() ? { s: '', liit: false } : null;
            }
            if (tok === '\\pi') return { s: 'pi', liit: false };
            if (tok === '\\exponentialE') return { s: 'e', liit: false };
            if (['\\sin', '\\cos', '\\tan', '\\ln', '\\exp', '\\log', '\\lg'].includes(tok)) return funktsioon(tok.slice(1));
            if (['\\mathrm', '\\operatorname', '\\text', '\\mathit', '\\textrm'].includes(tok)) {
                let nimi = '';
                if (vaata() === '{') {
                    vota();
                    while (i < t.length && vaata() !== '}') nimi += vota();
                    vota();
                } else {
                    nimi = vota() ?? '';
                }
                nimi = nimi.toLowerCase();
                return FUNKTSIOONID.includes(nimi) ? funktsioon(nimi) : { s: nimi, liit: false };
            }
            if (VAHED.has(tok) || tok === '\\displaystyle') return null;
            if (tok.startsWith('\\')) return { s: tok.slice(1), liit: false };
            return { s: tok, liit: false };
        }

        // Väärtuste ja tehete jada kuni lõputokenini; kõrvuti väärtuste vahele lisatakse korrutusmärk.
        function jada(lopp) {
            const osad = [];
            const viimane = () => osad[osad.length - 1];
            while (i < t.length && !lopp(vaata())) {
                const tok = vota();
                if (tok === '^') {
                    const astendaja = argument();
                    const alus = viimane();
                    if (alus && alus.op === undefined) {
                        alus.s = `${sulgu(alus.s, (x) => onArv(x) || onNimi(x) || onSulgudes(x))}^${sulgu(astendaja, (x) => /^\d+$/.test(x) || onNimi(x))}`;
                        alus.liit = false;
                    }
                    continue;
                }
                if (tok === '_') { argument(); continue; }
                if (tok === '+' || tok === '-') { osad.push({ op: tok }); continue; }
                if (tok === '*' || tok === '\\cdot' || tok === '\\times' || tok === '\\ast') { osad.push({ op: '*' }); continue; }
                if (tok === '/' || tok === '\\div') { osad.push({ op: '/' }); continue; }

                const v = aatom(tok);
                if (!v || v.s === '') continue;
                const eelmine = viimane();
                if (eelmine && eelmine.op === undefined) osad.push({ op: '*' });
                if (v.liit && viimane()?.op === '/') v.s = `(${v.s})`;
                osad.push(v);
            }

            let tulemus = '';
            osad.forEach((o, k) => {
                if (o.op === undefined) { tulemus += o.s; return; }
                const unaarne = k === 0 || osad[k - 1].op !== undefined;
                tulemus += (o.op === '+' || o.op === '-') && !unaarne ? ` ${o.op} ` : o.op;
            });
            return tulemus;
        }

        return jada(() => false).trim();
    }

    // ---------- Tekstivalem → LaTeX ----------

    function valemiTokenid(valem) {
        const tokenid = [];
        const muster = /\s*(\d*\.?\d+|[a-zA-Z]+|\*\*|[-+*/^()])/y;
        let m;
        let viimaneLopp = 0;
        while ((m = muster.exec(valem)) !== null) {
            tokenid.push(m[1] === '**' ? '^' : m[1].toLowerCase());
            viimaneLopp = muster.lastIndex;
        }
        if (valem.slice(viimaneLopp).trim() !== '') throw new Error('Tundmatu märk');
        return tokenid;
    }

    function parsiValem(valem) {
        const t = valemiTokenid(valem);
        let i = 0;
        const vaata = () => t[i];
        const oota = (x) => { if (t[i++] !== x) throw new Error(`Oodati ${x}`); };

        function avaldis() {
            let vasak = liige();
            while (vaata() === '+' || vaata() === '-') {
                const op = t[i++];
                vasak = { tyyp: op, a: vasak, b: liige() };
            }
            return vasak;
        }
        function liige() {
            let vasak = unaarne();
            while (vaata() === '*' || vaata() === '/') {
                const op = t[i++];
                vasak = { tyyp: op, a: vasak, b: unaarne() };
            }
            return vasak;
        }
        function unaarne() {
            if (vaata() === '-') { i++; return { tyyp: 'neg', a: unaarne() }; }
            if (vaata() === '+') { i++; return unaarne(); }
            return aste();
        }
        function aste() {
            const alus = pohi();
            if (vaata() === '^') { i++; return { tyyp: '^', a: alus, b: unaarne() }; }
            return alus;
        }
        function pohi() {
            const tok = t[i++];
            if (tok === undefined) throw new Error('Valem on poolik');
            if (onArv(tok)) return { tyyp: 'arv', s: tok };
            if (tok === '(') { const sisu = avaldis(); oota(')'); return { tyyp: 'sulud', a: sisu }; }
            if (onNimi(tok)) {
                if (FUNKTSIOONID.includes(tok)) { oota('('); const arg = avaldis(); oota(')'); return { tyyp: 'fn', nimi: tok, a: arg }; }
                return { tyyp: 'nimi', s: tok };
            }
            throw new Error(`Ootamatu ${tok}`);
        }

        const puu = avaldis();
        if (i < t.length) throw new Error('Liigne märk');
        return puu;
    }

    const ilmaSulgudeta = (n) => (n.tyyp === 'sulud' ? n.a : n);

    function vasakpoolseim(n) {
        while (['+', '-', '*', '/', '^'].includes(n.tyyp)) n = n.a;
        return n;
    }

    function latex(n) {
        switch (n.tyyp) {
            case 'arv': return n.s;
            case 'nimi': return n.s === 'pi' ? '\\pi' : n.s.length === 1 ? n.s : `\\mathrm{${n.s}}`;
            case 'sulud': return `\\left(${latex(n.a)}\\right)`;
            case 'neg': return `-${latex(n.a)}`;
            case 'fn': return n.nimi === 'sqrt'
                ? `\\sqrt{${latex(ilmaSulgudeta(n.a))}}`
                : `\\${n.nimi}\\left(${latex(n.a)}\\right)`;
            case '^': {
                const alus = ['arv', 'nimi', 'sulud', 'fn'].includes(n.a.tyyp) && !(n.a.tyyp === 'arv' && n.a.s.includes('.'))
                    ? latex(n.a) : `\\left(${latex(n.a)}\\right)`;
                return `${alus}^{${latex(ilmaSulgudeta(n.b))}}`;
            }
            case '/': return `\\frac{${latex(ilmaSulgudeta(n.a))}}{${latex(ilmaSulgudeta(n.b))}}`;
            case '*': {
                const parem = vasakpoolseim(n.b);
                const varjatud = n.b.tyyp !== 'neg' && parem.tyyp !== 'arv' && parem.tyyp !== 'neg';
                return varjatud ? `${latex(n.a)} ${latex(n.b)}` : `${latex(n.a)}\\cdot ${latex(n.b)}`;
            }
            default: return `${latex(n.a)}${n.tyyp}${latex(n.b)}`;
        }
    }

    // Tagastab null, kui tekstivalemit ei õnnestu lugeda (nt poolik valem kirjutamise ajal).
    function valemLatexiks(valem) {
        if (!valem || !valem.trim()) return '';
        try { return latex(parsiValem(valem)); }
        catch { return null; }
    }

    eksport.latexValemiks = latexValemiks;
    eksport.valemLatexiks = valemLatexiks;
})(typeof module !== 'undefined' ? module.exports : (window.Valemiteisendus = {}));
