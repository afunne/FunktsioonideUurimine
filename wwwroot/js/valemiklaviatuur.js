// Visuaalne valemiväli (MathLive) ja valemi klaviatuur loomise ja muutmise vormis.
// Tekstiväli jääb vormi tegelikuks väärtuseks: visuaalse välja muudatus kirjutatakse tekstiks ja vastupidi,
// nii et graafik ja vastused (graafik.js kuulab tekstivälja 'input' sündmust) uuenevad mõlemal juhul.
(() => {
    const redaktor = document.querySelector('[data-valemiredaktor]');
    if (!redaktor || !window.customElements) return;

    const sisend = document.getElementById(redaktor.dataset.sisend);
    const { latexValemiks, valemLatexiks } = window.Valemiteisendus;
    let vali;

    customElements.whenDefined('math-field').then(() => {
        // Fondid ja helid tuleb määrata enne esimese välja loomist (vaikimisi otsib MathLive fonte valest kohast).
        window.MathfieldElement.fontsDirectory = redaktor.dataset.fondid;
        window.MathfieldElement.soundsDirectory = null;

        vali = new window.MathfieldElement();
        vali.className = 'valemivali';
        vali.setAttribute('aria-label', 'Valem f(x)');
        redaktor.querySelector('[data-lylitid]').after(vali);

        vali.mathVirtualKeyboardPolicy = 'manual';   // MathLive enda klaviatuuri asemel on oma klaviatuur
        vali.smartFence = true;
        vali.placeholder = '\\text{nt } x^3-3x';

        redaktor.hidden = false;
        seadistaLylitid();
        naitaTeksti();

        // Visuaalne väli → tekst
        vali.addEventListener('input', () => {
            sisend.value = latexValemiks(vali.getValue('latex'));
            sisend.dispatchEvent(new CustomEvent('input', { bubbles: true, detail: { valemivaljast: true } }));
        });

        // Tekst → visuaalne väli (poolikut tekstivalemit ei näidata, viimane õige kuju jääb alles)
        sisend.addEventListener('input', (e) => {
            if (e.detail?.valemivaljast) return;
            naitaTeksti();
        });

        redaktor.querySelectorAll('.klahv').forEach((klahv) => {
            klahv.addEventListener('pointerdown', (e) => e.preventDefault());   // fookus jääb valemiväljale
            klahv.addEventListener('click', () => vajuta(klahv));
        });
    });

    // Lülitid: visuaalne väli ja klaviatuur. Ilma visuaalse väljata pole klaviatuurist kasu (see kirjutab välja sisse).
    function seadistaLylitid() {
        const lylitid = redaktor.querySelectorAll('[data-lylitid] [data-seade]');
        const klaviatuur = redaktor.querySelector('.klaviatuur');
        const vihje = redaktor.querySelector('[data-vihje]');
        const tekstisilt = document.querySelector('[data-tekstisilt]');
        const seade = (nimi) => redaktor.querySelector(`[data-seade="${nimi}"]`);

        function rakenda() {
            const visuaalne = seade('visuaalne').checked;
            const klahvid = seade('klaviatuur').checked;
            vali.hidden = !visuaalne;
            klaviatuur.hidden = !(visuaalne && klahvid);
            vihje.hidden = !visuaalne;
            tekstisilt.hidden = !visuaalne;
            seade('klaviatuur').disabled = !visuaalne;
        }

        lylitid.forEach((lyliti) => {
            try {
                const salvestatud = localStorage.getItem(`valemivali.${lyliti.dataset.seade}`);
                if (salvestatud !== null) lyliti.checked = salvestatud === '1';
            } catch { /* privaatne aken – vaikimisi sees */ }
            lyliti.addEventListener('change', () => {
                try { localStorage.setItem(`valemivali.${lyliti.dataset.seade}`, lyliti.checked ? '1' : '0'); } catch { /* ei salvestu */ }
                rakenda();
            });
        });
        rakenda();
    }

    function naitaTeksti() {
        const latex = valemLatexiks(sisend.value);
        if (latex !== null) vali.setValue(latex, { silenceNotifications: true });
    }

    function vajuta(klahv) {
        vali.focus();
        const kask = klahv.dataset.kask;
        if (kask === 'tyhjenda') {
            vali.setValue('');
        } else if (kask) {
            vali.executeCommand(kask);
        } else {
            vali.insert(klahv.dataset.sisesta, { selectionMode: 'placeholder', focus: true });
        }
        vali.dispatchEvent(new Event('input', { bubbles: true }));
    }
})();
