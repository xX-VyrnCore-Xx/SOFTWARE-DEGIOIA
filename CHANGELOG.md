# Changelog - PresenzeFlow

Tutti i cambiamenti significativi di PresenzeFlow sono documentati in questo file.

> Nota: le voci "Cronologia versioni"/"Novità" nel README e le sezioni storiche di questo file descrivono le release precedenti; la distribuzione attuale è solo l'installer Windows Inno Setup.

## [1.7.0] - Corrente

### 💬 Assistente AI conversazionale che agisce sul documento
- Nuova scheda **Assistente AI** nell'area di revisione (e pulsante **Chatta con AI**): conversazione in italiano con il modello locale
- L'AI **non scrive mai l'XML**: propone azioni strutturate (imposta ore/minuti, sposta date, riscala durate, cambia giustificativo, rimuovi) che il programma valida e applica all'anteprima
- **Azioni sicure**: i codici giustificativo vengono accettati solo se già configurati; i filtri selezionano i movimenti per dipendente, giustificativo, giorno o intervallo; ogni azione riporta l'esito e l'anteprima/XML si rigenerano subito
- **Cronologia conversazione persistente** nel database locale, con pulsante **Nuova chat**

### 🔁 Conversioni migliorate
- Le durate espresse in **giorni** (intestazioni `Giorni`, `Durata (giorni)`, `Days`, `gg`) vengono convertite in ore con la giornata lavorativa standard di 8h: prima "2 giorni" sarebbe diventato "2 ore"
- Nuova classe pura `DocumentEditor`/`DocumentEditParser`, separata dalla UI e coperta da test

### 💾 Preferenze e codici sempre salvati
- Codice azienda salvato automaticamente poco dopo la digitazione (debounce), non solo quando il campo perde il focus
- Posizione e dimensione della finestra ricordate (con controllo che ricadano in un monitor collegato)
- Ultima cartella del file Excel ricordata e riproposta all'importazione
- I codici dipendente/giustificativo corretti nell'anteprima vengono riportati nel database locale

### 🎨 Grafica
- Striscia sfumata (`AccentBar`) in cima all'header, nuova tinta di accento ciano
- Pannello di chat temizzato (bolla messaggi, stato, compositore) coerente con il resto dell'interfaccia

### 🚀 Rilasci automatici
- Nuovo workflow `.github/workflows/release.yml`: a ogni push su `main` (o tag `vX.Y.Z`) compila app e installer e crea la Release GitHub con `PresenzeFlow-Setup-X.Y.Z.exe` allegato
- Il workflow verifica che `Version` del `.csproj` e `AppVersion` di `Installer.iss` coincidano e non duplica Release già esistenti
- Nuovo script `tools/New-Version.ps1` che aggiorna la versione in tutti i punti (csproj + Installer.iss) in un solo comando

### ✅ Verifiche
- **10 nuovi test** su `DocumentEditor`/`DocumentEditParser` e sulla conversione giorni→ore (46 test di logica, tutti verdi)
- Nota: i test per la UI WinForms richiedono Windows; su questo ambiente di build Linux sono stati verificati compilazione dell'app e dei test e l'esecuzione della logica

---

## [1.6.0] - Precedente

### 🎨 Interfaccia: palette unica e bug grafici risolti
- **Tema unico** (`Theme.cs`): tutti i colori e i font sono definiti una sola volta e nominati per ruolo; finestra principale e i tre dialoghi usavano quattro blu diversi per lo stesso pulsante e bordi di tono differente
- **Pulsanti con tutti gli stati**: hover, premuto **e disabilitato**; prima un pulsante disabilitato restava colorato e sembrava ancora premibile
- **Niente più grigio di sistema**: la striscia delle schede del `TabControl` nativo veniva dipinta da Windows ignorando il tema (misurato: 2,5% dei pixel grigi non temizzati, ora 0,00%). Le schede sono disegnate dall'app e sono usate anche nel dialogo "Codici dipendenti e giustificativi", dove il TabControl compariva del colore di sistema
- **Campo codice azienda e ricerca**: bordo arrotondato, bordo blu al focus e testo guida (placeholder) che sparisce alla digitazione; il tasto di ricerca era largo 100 px e il testo non si leggeva
- **Bug di scalatura DPI rimossi**: `AutoScaleMode.Dpi` scala già tutta la geometria, quindi la seconda scalatura manuale gonfiava finestra e scatole a 125-150% lasciando invariati i font
- **Allineamento campi**: testo e placeholder condividono la stessa scatola (prima lo scarto era di qualche pixel)
- **Testo non più tagliato**: etichette dei passi, delle sezioni e del riepilogo in griglia; numeri di riga con la stessa famiglia monospaziata del resto
- **Famiglia monospaziata automatica**: Cascadia Mono se presente, altrimenti Consolas (Cascadia non esiste su tutti i sistemi)
- **Componenti condivisi**: `ThemedGrid` (doppio buffering, niente sfarfallio), `SurfacePanel` arrotondato configurabile, badge di stato e cerchini numerati disegnati in casa
- **Pulizia**: rimossi colori e font duplicati in sei file, i font creati a ogni ridisegnamento della barra schede e la doppia gestione del DPI

### ✅ Verifiche
- **Test di regressione grafica**: la finestra e i dialoghi vengono renderizzati e verificati pixel per pixel (nessun colore di sistema non temizzato, nessun testo tagliato); 34 test totali, tutti verdi
- Versione incrementata a 1.6.0

---

## [1.5.1] - Precedente

### 🚨 Correzione critica: l'app installata non si avviava
- **Causa**: l'installer copiava nella cartella di destinazione **solo** `PresenzeFlow.exe`, ma il `PublishSingleFile` non include le librerie native: mancava `e_sqlite3.dll`. All'avvio `Microsoft.Data.Sqlite` falliva con `DllNotFoundException` e l'app terminava **istantaneamente, senza alcuna finestra**
- **Sintomo**: sia il doppio clic sull'icona sia la spunta "Avvia PresenzeFlow" a fine installazione sembravano non funzionare
- **Correzione**: `e_sqlite3.dll` viene ora installato accanto all'exe (verificato: l'app si avvia con la stessa composizione di file prodotta dall'installer)
- L'avvio post-installazione usa inoltre `{app}` come cartella di lavoro

### 🛡️ Robustezza dell'avvio
- Errori non gestiti (avvio o interfaccia) non chiudono più l'app in silenzio: mostano un dialog con il messaggio completo e scrivono il dettaglio in `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\error.log`
- Versione incrementata a 1.5.1

### 🎨 Interfaccia: niente più sovrapposizioni
- **Scalatura DPI reale**: a 125-150% di scala i caratteri crescevano ma le posizioni no; ora tutta la geometria (righe delle tabelle, colonne, badge, griglia) viene riscalata sul DPI del monitor e la finestra resta entro lo schermo. Verificato con UI Automation: da 2 sovrapposizioni + 6 elementi fuori bordo a **0 e 0**
- **Header**: titolo, sottotitolo e badge su righe/colonne separate: non si più finiscono sopra l'uno all'altra e il badge resta a destra ad ogni resize (rimosso il riposizionamento manuale)
- **Barra strumenti anteprima**: contatore, ricerca e pulsanti in righe con altezza sufficiente; prima i pulsanti uscivano dal rigo e finivano sull'area di griglia
- **Riepilogo in basso**: stato e indicazione AI su due colonne distinte con ellissi: il testo lungo non passa più sotto l'etichetta AI
- **Tile metriche** e **passi numerati**: contenuto contenuto nelle proprie caselle a qualsiasi scala
- **Numeri di riga duplicati**: la griglia disegnava "1" sopra "01"; ora il numero custom cancella prima quello di default
- **Dialoghi**: scaling DPI su tutti e tre + pulsante "Annulla" (ed ESC) in "Associa colonne", che prima non aveva modo visibile di essere chiuso
- **Codice morto rimosso**: ~300 righe di vecchio layout ormai inutilizzato (`BuildLegacyLayout`/`BuildLegacyPreview`) e il campo etichetta non più mostrato

---

## [1.5.0] - Precedente

### 🧙 Wizard di installazione e disinstallazione
- Il wizard di installazione propone l'opzione "Installa l'AI locale" (Ollama + modello `qwen2.5:1.5b`, circa 2 GB): deselezionabile, la conversione funziona comunque senza
- La disinstallazione chiede se rimuovere anche il modello AI scaricato (solo se Ollama è presente); Ollama stesso non viene mai toccato
- Testo del wizard di revisione aggiornato

### 🤖 AI locale più adatta al compito
- Modello predefinito `qwen2.5:1.5b` (era `qwen2.5:0.5b`): migliore a classificare intestazioni e giustificativi pur restando leggero
- Rilevamento automatico dei modelli compatibili già installati (`qwen2.5` qualsiasi variante)
- Prompt strutturati con esempi few-shot e temperature 0 per suggerimenti riproducibili
- Tolleranza al formato della risposta e fallback normalizzato sull'abbinamento delle intestazioni

### ⚡ Motore di analisi e conversione
- Riconoscimento fuzzy delle intestazioni: maiuscole, accenti, punteggiatura e sinonimi comuni (es. "NOME DIPENDENTE", "Data Inizio (dal)")
- Le colonne con "codice" non vengono più assegnate per errore al nome dipendente
- Parsing durate esteso: `8h30`, `8h`, `8,5h`, `8:30` e celle Excel con formato orario (frazione di giorno convertita in ore)
- Selezione automatica del foglio migliore tra quelli del workbook
- Movimenti duplicati (stesso dipendente, giorno e giustificativo) uniti sommando le ore, con avviso se superano le 24
- Controllo di plausibilità sulla durata importata

### 🚀 Velocità e risparmio di tempo
- Importazione del file Excel e generazione dei movimenti eseguite in background: l'interfaccia non si blocca mai
- Ricalcolo XML in griglia posticipato di 350 ms dopo l'ultima modifica (digitazione fluida)
- Evidenziazione syntax XML disattivata automaticamente oltre 400k di caratteri
- Salvataggio XML in streaming su disco (niente duplicazione in memoria per file grandi)
- L'export ricorda l'ultima cartella di destinazione

### 🔔 Tray icon e notifiche
- Alla chiusura puoi scegliere (una sola volta, con opzione "ricorda") se uscire o restare in tray
- In background arriva una notifica quando una conversione termina
- Menu tray con apertura rapida ed escape definitivo

---

## [1.4.0] - 2024

### 🎨 Miglioramenti UI/UX (Redesign Completo)

**Palette Colori Modernizzata**
- Blu primario: #3B82F6 (da #246DB7)
- Verde primario: #22C55E (da #158E70)
- Aggiunto accento viola: #A855F7
- 16 colori semantici per superficie, stati hover, divider e testo

**Header Redesignato**
- Altezza aumentata da 76px a 80px
- Titolo: Segoe UI 24px bold (da 18px)
- Icona: 52x52px (da 48x48px)
- Sfondo elegante #0F1A28 con migliore gerarchia visiva

**Componenti UI Migliorati**
- **Metric Tiles**: Aggiunti bordi dinamici, spaziatura 14px, font numero 22px
- **Tabs**: Underline styling più pulito con colori moderni
- **Pulsanti**: Minimo 120x38px, cursor feedback migliorato, stati hover/down rinnovati
- **Data Grid**: Header #F2F4F8, righe alternate #F9FAFE, gridline #EDEFF4
- **Toolbar**: Sfondo CanvasDark #F1F5FA più raffinato
- **Footer**: CanvasDark con border styling

**XML Syntax Highlighting**
- Tag: #81C6FF (da #67BEE8) - blu più luminoso
- Attributi: #FFA500 (da #E8D64B) - arancione definito
- Valori: #A7C161 (da #E8BE75) - giallo-verde moderno
- Testo: #A7F3D0 (da #B4DBB9) - verde menta più soft
- Sfondo editor: #0F172A (dark mode ottimizzato)

**Spacing e Layout**
- Padding standardizzato su 16px
- Spaziatura verticale coerente tra sezioni
- Migliore allineamento orizzontale con 8px grid
- Effetto depth con ombre sottili

### 🔧 Miglioramenti Tecnici

**Build e Distribuzione**
- Version incrementato a 1.4.0
- Release build completamente self-contained
- Unica modalità di distribuzione: installer Windows con Inno Setup (`Installer.iss`)

**Qualità Codice**
- Tutti i 17 test di validazione passano ✅
- Nessun breaking change nelle API
- Syntax error corretto (duplicate bracket in CreateStepDot)
- Nullable reference warnings documentati (non critici)

### 📦 Distribuzione

**Opzioni di installazione**
- `dist/PresenzeFlow-Setup-1.4.0.exe`: installer Windows generato da `Installer.iss` (Inno Setup 6)
- Installer batch e archivio portatile rimossi: resta solo l'installabile Windows

### ✅ Test Coverage

- 17 unit tests in Release configuration (100% passing)
- Test categories: XML generation, validation, data processing
- Regressione azzerata: nessun impatto su logica core da UI redesign

### 📋 Note di Rilascio

**Breaking Changes**: Nessuno - completamente backward compatible

**Deprecazioni**: Nessuna

**Improvements Futuri Considerati**
- [ ] Implementazione di 7z per ulteriore compressione installer
- [ ] Possibile certificazione installer per Windows Defender
- [ ] Aggiunta di tema scuro completo per editor XML
- [ ] Preview PDF integrata per tracciato Zucchetti

---

## [1.3.0] - Precedente

### Aggiunte
- Modello di analisi dati avanzato
- Validazione automatica del database SQLite
- Controlli di integrità file ZIP
- Validazione XML completa pre-export
- Sicurezza migliorata (query parametrizzate, secure_delete)

### Correzioni
- Gestione eccezioni file corrotti
- Robustezza accessi concorrenti DB
- Sincronizzazione scritture garantita

---

## [1.2.0] - Precedente

### Aggiunte
- Redesign grafica interfaccia principale
- Layout responsive Windows 11
- Tema colori coordinato
- Miglioramento leggibilità dati

---

## [1.1.0] - Precedente

### Aggiunte
- Setup wizard personalizzato
- Icona multi-risoluzione
- Disinstallazione intelligente con opzioni

---

## [1.0] - Precedente

### Feature Iniziali
- Conversione Excel → XML TRRIPW
- Importazione file .xlsx
- Mapping colonne intelligente
- Associazione giustificativi
- AI locale (Ollama qwen2.5:0.5b)
- Database locale SQLite
- Validazione completa
- Export XML deterministico

---

## Come Interpretare le Versioni

- **MAJOR (1.x.0)**: Breaking changes, rework architettura
- **MINOR (1.4.0)**: Nuove feature, UI redesign, miglioramenti significativi
- **PATCH**: Bug fix, correzioni di sicurezza (futura versione 1.4.1, etc.)

## Supporto

Per problemi, segnalazioni o suggerimenti, contatta il team di sviluppo.
