# Changelog - PresenzeFlow

Tutti i cambiamenti significativi di PresenzeFlow sono documentati in questo file.

> Nota: le voci "Cronologia versioni"/"Novità" nel README e le sezioni storiche di questo file descrivono le release precedenti; la distribuzione attuale è solo l'installer Windows Inno Setup.

## [1.11.0] - Corrente

### 📊 Analisi dei file Excel
- Righe problematiche **segnalate e non bloccanti**: ogni riga scartata riporta numero e motivo (data illeggibile, durata non riconosciuta, codice troppo lungo, date invertite) e l'elenco compare nei Controlli
- **Scelta del foglio**: se il file ha più fogli simili, il pulsante *Foglio: …* permette di cambiarlo senza reimportare il file, riutilizzando l'abbinamento delle colonne
- Intestazioni fuzzy più numerose, date e durate riconosciute in più formati (celle con formato orario, durate in giorni), nomi normalizzati e codici PAGHE letti quando presenti
- Nuove API pure (`WorkbookCell`, `WorkbookInspection.Read`) con i problemi separati dalle richieste valide

### 🧹 Chat AI rimossa, nuova scheda Controlli
- Eliminati la scheda **Assistente AI**, il pulsante *Chatta con AI*, la cronologia in database e i file `DocumentEdits`/`LocalAiAssistant.Chat`
- Nuova scheda **Controlli**: elenco unico ordinato per gravità (ERRORE/ATTENZIONE/NOTA) con righe non importate, codici non configurati o mancanti, codici PAGHE duplicati, sovrapposizioni nello stesso giorno, giornate oltre la standard, festivi e fuori periodo, dipendenti senza movimenti, differenze fra ore dichiarate e generate
- Doppio clic su una segnalazione per raggiungere il movimento, **Copia elenco** per condividerlo; i controlli si aggiornano a ogni modifica della griglia e l'app apre la scheda da sola quando restano errori

### 🤖 AI impiegata meglio sulla generazione dell'XML
- **Structured outputs**: gli schemi JSON vengono passati a Ollama nel campo `format`, quindi l'abbinamento di colonne e codici non dipende più dal testo libero del modello
- Prompt riscritti (regole esplicite, temperature 0, seed fisso, `keep_alive`, contesto e `num_predict` limitati) e gestione dei modelli "reasoning" con `think: false`
- Nuova **revisione dei movimenti**: l'AI rilegge il riepilogo dell'anteprima e aggiunge solo osservazioni verificate; un'osservazione che cita codici non configurati viene scartata
- Modello predefinito e **consigliato `qwen2.5:3b`** (il più affidabile per l'estrazione strutturata fra i modelli piccoli); lista dei modelli accettati allineata fra app e installer

### ⬇️ Aggiornamenti collegati subito e sempre
- **Token GitHub verificato e attivato dall'app**: Impostazioni → Aggiornamenti con esito reale di GitHub (200 collegato, 401 token non valido, 403 permessi mancanti, 404 non visibile, oppure errore di rete); il token è attivo **nella stessa sessione**, senza riavvio, e viene salvato con `Rimuovi token` per cancellarlo
- **Controllo periodico ogni ora** oltre a quello all'avvio, **stato degli aggiornamenti sempre visibile** nel piè di pagina e pulsante **Aggiornamenti** nell'intestazione
- **Token fornibile all'installazione**: parametro `/GITHUBTOKEN=...` o file `github.token` accanto al setup, salvato anche in installazione silenziosa e mai sovrascritto con un valore vuoto
- Al primo avvio, se la repo privata non è raggiungibile per mancanza di token, le impostazioni si aprono una sola volta nel punto giusto

### 🔢 Motore di calcolo e ricerca
- **Ore della giornata standard configurabili** (1-24 h, virgola o punto): usate per convertire le durate in giorni e per segnalare le richieste che superano la giornata, che vengono **mai scartate**
- **Sabato lavorativo opzionale** e **chiusure aziendali aggiuntive** (gg/mm o gg/mm/aaaa), con le voci non riconosciute riportate all'utente invece di essere ignorate in silenzio
- Avvisi più chiari quando una richiesta non trova giorni lavorativi (fine settimana, festivi, sabati)
- **Ricerca multi-termine** in qualsiasi ordine, **senza accenti e senza maiuscole**, che riconosce codici, date (italiane e ISO), nomi dei mesi e durate (`7:30`, `7.30`, `7h30`, 450 minuti)

### 🎨 UI/UX
- Pulsanti **Impostazioni** e **Aggiornamenti** nell'intestazione, nuovo dialogo **Impostazioni** per conversione, aggiornamenti e modello AI
- Pulsante **Foglio: …** per scegliere il foglio, riepilogo dell'anteprima con dipendenti, giornate e ore/minuti totali
- **Scorciatoie da tastiera** (`Ctrl+O`, `Ctrl+G`, `Ctrl+S`, `Ctrl+F`, `Ctrl+1/2/3`, `Ctrl+,`, `F1`) e voce *Scorciatoie da tastiera* nel menu dell'icona in tray
- Scritte e messaggi di stato riscritti (stato degli aggiornamenti e dell'AI sempre leggibili nel piè di pagina)
- **Card di sinistra che entra sempre nella finestra**: le altezze delle righe stanno in un unico posto (`SetupCardLayout`) e la guida ai passi è l'unica riga elastica, quindi non compare la barra di scorrimento grigia di sistema e nessun testo viene schiacciato; *Codici PAGHE* e *AI locale* sono sulla stessa riga
- **Campo di ricerca dell'area di revisione non più tagliato**: la fascia dei comandi ha la larghezza che le serve, il riepilogo prende lo spazio rimanente ed è lui a troncare con i puntini

### 🔏 Firma digitale obbligatoria
- **Il workflow di Release non pubblica più eseguibili non firmati**: prima, senza i secret del certificato, lo step della firma veniva **saltato in silenzio** (il log diceva solo "l'installer verrà prodotto senza firma") e usciva una Release non firmata. Ora se il certificato non è configurato il workflow **si ferma** e dice **quale** dei due secret manca (`CODE_SIGNING_PFX_BASE64`, `CODE_SIGNING_PFX_PASSWORD`), con i passi per sistemarlo; per una release non firmata serve un consenso esplicito (`ALLOW_UNSIGNED_RELEASE=true`, variabile di repository)
- **Firma e verifica** di eseguibile, installer e disinstallatore: dopo ogni firma il file viene controllato (`tools/Test-CodeSignature.ps1`), perché `signtool` può uscire con codice 0 senza aver firmato nulla e Inno Setup firma l'installer per una strada diversa (`-dEnableSigning=1 -sci=...`). Si controlla anche l'esito di `signtool` e di `ISCC` (`$LASTEXITCODE`)
- **Il certificato pubblico segue la Release**: viene esportato dal `.pfx` ed allegato come `PresenzeFlow-codesign.cer`, così i PC possono renderlo attendibile (Trusted Root/Trusted Publisher) senza passarselo a mano
- Il caricamento del certificato usa **.NET diretto** invece di `Import-PfxCertificate` (nessuna dipendenza dal modulo PKI) e distingue i casi: Base64 non valido, contenuto che non è un `.pfx`, password errata, certificato senza chiave privata
- Riepilogo dell'esecuzione (Actions → Summary) con l'impronta del certificato usato, o l'avviso esplicito di Release non firmata

### ✅ Verifiche
- **152 test di logica, tutti verdi** (55 nuovi) su ricerca, impostazioni, controlli, generazione con le nuove opzioni, aiuti dell'AI e installer
- Nuovo test sul bilancio delle altezze della card (`SetupCardLayout`): a ogni dimensione di finestra consentita il contenuto deve entrare, altrimenti comparirebbe la barra di scorrimento di sistema
- Nuova guardia sui **tipi del Pascal Script** della sezione `[Code]`: ogni tipo dichiarato deve esistere fra quelli registrati dal compilatore Inno Setup 6 (elenco nella versione 6.7), così un tipo inventato si vede nei test e non alla prima release. Il caso reale: `RootKey: HKEY` — in Inno Setup 6 le chiavi di registro sono costanti `Integer` e `HKEY` come tipo esiste solo da Inno Setup 7, quindi l'installer non compilava e la release non veniva pubblicata; i parametri sono ora `Integer`
- **Tipi `AnsiString` dei parametri `var`**: in Inno Setup 6 `String` (Unicode) e `AnsiString` sono tipi distinti e per un parametro `var` non esiste conversione implicita, quindi `LoadStringFromFile(FileName, var S: AnsiString)` rifiutava una variabile `String` con *Type mismatch* e la compilazione dell'installer si fermava lì (la Release non veniva pubblicata). La lettura del token GitHub e del modello preferito usa ora una variabile `AnsiString` con `UTF8Decode`, mentre le scritture usano `UTF8Encode` esplicito; la nuova guardia `AnsiStringFileFunctionsReceiveAnsiStringArguments` controlla i tipi di ogni chiamata con parametri `AnsiString`, risolvendo le dichiarazioni per ambito
- **Commenti non annidabili nel `[Code]`**: i commenti del Pascal Script finiscono alla prima graffa chiusa, quindi un commento che citava la cartella temporanea fra graffe diventava codice e la compilazione si fermava con *'BEGIN' expected*. I due commenti colpevoli (della 1.10.0) sono stati riscritti e la nuova guardia `InstallerCodeCommentsDoNotNest` percorre la sezione segnalando graffe e `(*` annidati, con il numero di riga del file
- Guardia sul workflow di release: la versione di Inno Setup deve arrivare dal **registro delle installazioni**, perché `ISCC.exe` dichiara `0.0.0.0` fra le proprie risorse e il controllo sulla versione bloccava la pubblicazione senza compilare nulla; se la versione non è determinabile si prova a compilare lo stesso
- **Versione minima di Inno Setup corretta da 6.1 a 6.3**: lo script usa `CreateDownloadPage` (dalla 6.1) ma anche `ShowBaseNameInsteadOfUrl` e `ArchitecturesAllowed=x64compatible`, introdotti nella 6.3 — su una 6.1/6.2 la compilazione si fermerebbe a metà con un errore oscuro. Un test tiene ora allineate la versione minima dichiarata dal workflow e le funzionalità realmente usate dallo script
- **Audit completo della sezione `[Code]`** contro le 359 firme reali di Inno Setup 6.7.1 ricavate dai sorgenti (nomi, numero di argomenti, tipi dei parametri `var`) e contro le classi esposte dal compilatore (proprietà e metodi, comprese le catene a più livelli come `WizardForm.TasksList.OnClickCheck`): nessuna chiamata o membro inesistente
- Guardie sul percorso di firma: il workflow deve rifiutare la pubblicazione senza certificato e dichiarare quale secret manca, deve firmare e verificare sia l'eseguibile sia l'installer, e lo script di verifica deve controllare hash, impronta del certificato e scadenza
- Nuove guardie statiche sull'installer: token fornibile all'installazione senza sovrascrivere quello esistente e modello consigliato coincidente con il predefinito dell'app

---

## [1.10.0] - Precedente

### ⬇️ Installazione dell'AI locale riscritta
- **Barra di avanzamento reale nel download di Ollama**: l'installer ufficiale viene scaricato con la pagina di download di Inno Setup (`CreateDownloadPage`), quindi con percentuale visibile, possibilità di **annullare** e **fino a 3 tentativi automatici** su rete instabile; prima era un comando PowerShell silenzioso che sembrava bloccato
- **Riconoscimento robusto di Ollama**: oltre alle cartelle documentate vengono controllati il **registro delle installazioni** (`InstallLocation`, anche in `WOW6432Node`) e il `PATH`, quindi un'installazione in una cartella personalizzata non viene più scambiata per "da installare"
- **Il modello viene scaricato davvero, ovunque sia Ollama**: il passo `ollama pull` usava sempre `%LOCALAPPDATA%\Programs\Ollama\ollama.exe`; con Ollama installato altrove il download veniva saltato in silenzio. Ora il percorso viene risolto come nel riconoscimento
- **Scelta della dimensione del modello** in una pagina dedicata: *Veloce* `qwen2.5:0.5b` (~400 MB), *Consigliato* `qwen2.5:1.5b` (~1 GB), *Qualità* `qwen2.5:3b` (~2 GB) oppure *nessun modello adesso*; la scelta viene riproposta agli aggiornamenti (file `ai-model.txt`)
- **Niente download inutili**: se il modello è già presente (`manifests`, `ollama.com` o `manifests-v2`, rispettando `OLLAMA_MODELS`) il passo viene saltato
- **Disinstallazione mirata**: viene rimosso solo il modello scelto per PresenzeFlow, mai un modello usato da altri programmi

### 🖥️ Installazione più rapida e diagnosticabile
- `MinVersion` (Windows 10 1607): su un sistema non supportato l'installer lo dice subito, invece di installare un'app che non parte
- `LZMANumBlockThreads=4` e `LZMAUseSeparateProcess=yes`: compressione più rapida in CI e minor memoria durante l'installazione
- `SetupLogging=yes`: ogni installazione scrive un log in `%TEMP%`, così un problema segnalato dall'utente è ricostruibile
- Il file `github.token` viene rimosso insieme ai dati locali durante la disinstallazione
- Nuova opzione "nessun collegamento nel menu Start" (`AllowNoIcons`)

### 🤖 Nell'app: Ollama senza reinstallazioni
- Nuova logica pura `OllamaSetup` (percorsi candidati, cartelle dei manifest, comandi winget, interpretazione dell'avanzamento) interamente coperta da test
- **Installazione tramite winget** quando disponibile, con **ritorno automatico all'installer ufficiale** se winget manca o fallisce; se l'installazione silenziosa non produce risultati viene riproposto l'installer visibile
- **Download del modello con percentuale reale**: lo stream NDJSON di `/api/pull` viene tradotto in avanzamento (byte di tutti i blocchi) in una finestra dedicata e annullabile; prima un download da circa un gigabyte avveniva senza alcun riscontro
- **Controllo dello spazio su disco** (2,5 GB) prima di avviare il download
- **Modello preferito coerente con l'installer**, letto da `ai-model.txt`; il confronto avviene sul **nome completo con il tag**, quindi non viene più richiesto `qwen2.5:1.5b` quando l'unico modello installato è il `3b`
- La finestra di avanzamento è ora una sola e condivisa (`ProgressDialog`) tra aggiornamenti, Ollama e modelli, con il conteggio delle righe del layout allineato agli stili dichiarati

### ✅ Verifiche
- **91 test di logica, tutti verdi** (31 nuovi: 24 su Ollama e modelli, 7 sull'installer), compresi i controlli automatici che ogni costante `{#…}` usata sia definita, che i blocchi Pascal siano bilanciati e che la versione dell'installer coincida con quella del progetto
- Le due finestre di avanzamento sono entrate nel test di regressione grafica (eseguito su Windows in CI)

---

## [1.9.0] - Precedente

### ⚡ Installazione e avvio più veloci
- **Ollama già installato**: l'installer salta download e installazione del motore (`Check: OllamaMissing`) e scarica **solo il modello**; anche l'app, se trova Ollama installato ma non attivo, prova ad avviarlo e attende che risponda, senza reinstallare nulla
- **Compressione `lzma2/normal`** al posto del default `max`: decompressione più rapida e minor memoria durante l'installazione
- **Apertura più rapida**: il font monospaziato viene risolto provando le famiglie disponibili, senza enumerare tutti i font di sistema (`InstalledFontCollection`); il controllo d'integrità del database viene eseguito al primo avvio e poi al massimo una volta a settimana

### 🔐 Firma digitale (opzionale)
- Nuovo `tools/New-CodeSigningCertificate.ps1`: crea il certificato di firma autofirmato e prepara i secret per il workflow
- Il workflow firma eseguibile e installer con `signtool` + `SignTool` di Inno (`SignedUninstaller=yes`) **solo se** i secret `CODE_SIGNING_PFX_BASE64` / `CODE_SIGNING_PFX_PASSWORD` sono configurati; altrimenti la build resta valida e non firmata
- README con le istruzioni per rendere attendibile il certificato sui PC (`certutil -addstore -user Root/TrustedPublisher`) e nota onesta: un'autofirma non elimina l'avviso SmartScreen per chi scarica da Internet

### 🎨 Grafica
- Nuova barra di avanzamento temizzata (`ProgressStrip`): niente più grigio di sistema nel download dell'aggiornamento
- Intestazione sfumata (`AccentBar`) anche nella finestra "Novità"

### 🧹 Repository più pulita
- Rimossi i file generati e non usati dal build (`assets/PresenzeXmlConverter.png`, `assets/WizardImage.png`, `assets/Branding.svg`) e la sezione "Cronologia versioni" duplicata nel README
- `.gitignore` aggiornato: output di `Create-BrandAssets.ps1` e cartella `codesign/` (mai commitare il certificato)

---

## [1.8.0] - Precedente

### 🔄 Aggiornamenti dalle Release GitHub (repository privata)
- **Controllo automatico all'avvio** (ritardato di qualche secondo, silenzioso) dell'ultima Release; se c'è una versione più recente compare una finestra con le novità e **Aggiorna ora** / **Più tardi**
- **Download verificato**: l'installer viene scaricato con barra di avanzamento e controllato con il **checksum SHA-256** pubblicato nella Release
- **Installazione guidata**: avvio dell'installer in modalità silenziosa, chiusura dell'app e **riavvio automatico** a fine installazione
- **Finestra "Novità"** mostrata automaticamente dopo l'aggiornamento, con le note salvate in locale (funziona anche senza rete); voci **Cerca aggiornamenti…** e **Novità di questa versione** nel menu dell'icona in tray
- **Adattamento alla repo privata**: le chiamate all'API usano un token di sola lettura letto da `PRESENZEFLOW_GITHUB_TOKEN` o dal file `github.token` (mai incorporato nell'eseguibile); gli asset vengono scaricati tramite l'API con `Accept: application/octet-stream`
- **Messaggi chiari** per token mancante/non valido, nessuna Release e versione non riconoscibile
- Nuova preferenza `auto-update-check` (controllo automatico on/off) e `update-endpoint` (feed alternativo)

### 🚀 Rilasci automatici
- Il workflow ora pubblica anche il **checksum** `PresenzeFlow-Setup-X.Y.Z.exe.sha256` accanto all'installer
- L'installer riapre PresenzeFlow anche dopo un'installazione silenziosa (aggiornamento in-app)

### ✅ Verifiche
- **13 nuovi test** su interpretazione dei tag, confronto versioni (componenti mancanti normalizzati) e verifica SHA-256: 59 test di logica totali, tutti verdi

---

## [1.7.0] - Precedente

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
