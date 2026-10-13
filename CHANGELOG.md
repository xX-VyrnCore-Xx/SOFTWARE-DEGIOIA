# Changelog - Vyrn Presence (già PresenzeFlow)

Tutti i cambiamenti significativi di Vyrn Presence sono documentati in questo file.

> Nota: le voci "Cronologia versioni"/"Novità" nel README e le sezioni storiche di questo file descrivono le release precedenti; la distribuzione attuale è solo l'installer Windows Inno Setup.

## [1.22.13] - Corrente

### 🎨 Grafica più curata (la conversione non cambia)
- Nuovo marchio nell'intestazione: quadrato con sfumatura di marca e spunta, al posto dell'icona generica
- Pulsanti con transizione morbida al passaggio del mouse e un filo di luce sul bordo alto dei pulsanti pieni
- Card con un filo d'ombra sul bordo inferiore, e barra colorata in cima alle metriche della dashboard
- Nelle griglie la riga sotto il mouse si evidenzia con un azzurro leggero

## [1.22.12]

### ✅ Presenza: il giustificativo è 01
- Il codice delle presenze nei giorni lavorativi senza assenza non è né 334 né 999: è **01** (ore ordinarie) sia come codice di rilevazione sia come codice ufficiale

## [1.22.11]

### ✅ Presenza nei giorni senza assenza (334)
- Ogni giorno lavorativo (lun-ven, esclusi festivi e chiusure) in cui il dipendente non ha assenze viene registrato come presenza con il giustificativo universale **334** (codice di rilevazione 334, codice ufficiale 01 = ore ordinarie: con 999 Paghe scarterebbe il movimento). Se nel giorno c'è un'assenza parziale, il 334 riceve le ore residue (ore standard − ore di assenza)
- Il periodo è quello del foglio, oppure i mesi interi toccati dalle richieste. Il sabato conta solo se è attivo nelle impostazioni dell'azienda

## [1.22.10]

### 🧩 XML identico a quello accettato da Zucchetti
- Da un XML che Zucchetti importa davvero è emersa la struttura corretta: radice `<Fornitura>` (non `TRRIPW` né `Dipendenti`), `Dipendente` con i codici azienda e dipendente come **attributi**, `Movimenti` con l'attributo `GenerazioneAutomaticaDaTeorico="N"`, e per ogni movimento `CodGiustificativoRilPres`, `CodGiustificativoUfficiale`, `Data`, `NumOre` a 2 cifre (`08`); i minuti compaiono solo se diversi da zero
- Il controllo del file salvato segue la stessa struttura (legge i codici anche come elementi, per i file delle versioni precedenti)
- Il formato di esportazione torna l'XML TRRIPW; l'esportazione TRRIPA a testo è stata tolta

## [1.22.7]

### 🧩 Formato XML configurabile (Zucchetti: «cvc-elt.1»)
- Zucchetti valida il file contro uno schema che non è nel tracciato PDF: né `<TRRIPW>` né `<Dipendenti>` come radice sono stati accettati. Nuova scheda **Impostazioni → Formato XML**: nome dell'elemento radice, spazio dei nomi (xmlns) e disposizione dei movimenti (dentro un nodo «Movimenti» oppure un nodo «Movimenti» per giorno)
- **Leggi da un XML di esempio…**: scegliendo un file XML che Zucchetti accetta (anche prodotto da un altro programma) l'app copia radice, spazio dei nomi e disposizione, senza indovinare
- Il controllo del file salvato segue la forma scelta

## [1.22.6]

### 🐞 Zucchetti respingeva il file: elemento radice
- Il caricamento su Zucchetti falliva con «cvc-elt.1: impossibile trovare la dichiarazione dell'elemento "TRRIPW"»: «TRRIPW» è il nome del tracciato, non dell'elemento. La radice del file ora è `<Dipendenti>` (il contenitore dei nodi `Dipendente`)

### 🐞 Conversione: «nessun movimento» con il file del cliente
- Se il periodo scritto in cima al foglio («From Date / To Date») non contiene **nessuna** richiesta (il file del cliente diceva agosto, le richieste erano di settembre), l'app scartava tutto e mostrava «nessun movimento». Ora il periodo viene ignorato e un avviso lo dice: si usano le date delle richieste. Le richieste fuori dal periodo restano escluse solo quando altre richieste lo rispettano
- Colonne: quando il foglio ha sia «Policy type» («Vacation (hours)») sia «Policy» («IT vacation (hours)») si sceglie «Policy», quella a cui è abbinato il giustificativo

## [1.22.5]

### 💾 Finestra e aggiornamenti
- La posizione e la dimensione della finestra (e lo stato «ingrandita») si salvano mentre si usa l'app, non solo alla chiusura: dopo un aggiornamento la finestra ricompare dov'era
- I dati salvati (aziende, codici, storico, impostazioni) non vengono toccati dall'aggiornamento

### 🎨 Aspetto più curato
- Campo di ricerca dell'intestazione scuro, in tono con la fascia, con la lente

## [1.22.4]

### 🎨 Aspetto più curato
- Pulsanti dipinti dall'app: angoli arrotondati con bordo morbido come card e campi, stato al passaggio e alla pressione, anello di focus da tastiera, testo sempre su una riga (mai più etichette spezzate su due righe)
- Griglie con intestazione chiara e senza le linee scure di sistema attorno a ogni cella
- Testo guida dei campi nativo di Windows: non si sovrappone più al campo

### 🐞 Campi di testo
- Il cursore lampeggiante ora si vede: il testo guida («ABC123», «Cerca nei movimenti») copriva il campo anche quando era attivo e nascondeva il cursore. Ora sparisce appena clicchi nel campo
- Campo attivo con alone morbido attorno al bordo, così si capisce subito dove si sta scrivendo

### ✨ Note di aggiornamento più belle
- Le novità di una versione non sono più testo grezzo: i titoli («What's Changed» diventa «Novità») hanno la barretta d'accento, le voci un punto colorato, e spariscono autore, link alla pull request e «Full Changelog»
- Tolta la riga d'apertura che ripeteva il titolo («Vyrn Presence 1.22.2 — PresenzeFlow 1.22.2 (v1.22.2)»)
- Testo più grande e leggibile; «Copia note» copia sempre il testo originale

## [1.22.3]

### 🐞 Correzioni grafiche (verificate sulle schermate rese in CI)
- Novità di una versione: la barra di scorrimento stava sopra il testo, a sinistra; ora è a destra. I segni Markdown (`##`, `**`) non compaiono più
- Dashboard: l'intestazione dell'elenco «Ultime esportazioni» non viene più tagliata a metà con la finestra bassa
- Aziende: le etichette dei pulsanti («Salva azienda», «Usa in conversione») stanno su una riga; tolto il testo guida che rubava spazio alla barra
- Tutti i pulsanti hanno un margine in più attorno al testo, così nessuna etichetta va a capo
- Casella del file: «oppure fai clic per scegliere un file» non è più troncata

### 🧹 Release
- Il workflow di Release elimina le Release vecchie (con il loro tag) e tiene solo la nuova e una precedente, per non esaurire lo spazio del repository

## [1.22.2]

### 🐞 Correzioni
- Aziende: **Salva azienda** è il pulsante più a destra della barra, sempre visibile; stessa cosa per **Apri cartella** nello Storico

## [1.22.1]

### 🐞 Correzioni
- Finestra delle novità: il pulsante **Più tardi** accanto ad **Aggiorna ora** non viene più tagliato fuori dal bordo
- Stesso difetto corretto nei piè di pagina di Aziende, Codici, Storico e della barra di revisione: i pulsanti più a sinistra non vengono più tagliati

## [1.22.0]

### 🏢 Regole di conversione per azienda
- Nella sezione **Aziende** il pulsante **Regole…** apre le regole proprie dell'azienda: giornata standard, sabato sempre lavorativo, rilevamento automatico di sabati e ore, chiusure aziendali. Senza regole proprie vale quanto impostato in Impostazioni (che restano le regole generali)
- Scegliendo l'azienda in conversione le regole si applicano da sole: l'anteprima già generata si invalida e le richieste del foglio si rileggono con la nuova giornata standard; la colonna **Regole** dell'elenco mostra per ogni azienda «generali» oppure il riepilogo delle proprie
- Salvate nel database (schema 5, incluse nei backup); rinominare l'azienda non le cancella

---

## [1.21.0] - Precedente

### ✨ Nuovo nome: Vyrn Presence
- L'app si chiama **Vyrn Presence** (prima PresenzeFlow): finestra, tray, notifiche, impostazioni, aggiornamenti, installer, collegamenti e proprietà del file. Restano invariati i nomi interni (eseguibile, installer, cartella dei dati, identificatori), così gli aggiornamenti dalle versioni già installate continuano a funzionare e i dati si ritrovano
- L'installer toglie i vecchi collegamenti «PresenzeFlow» e crea quelli nuovi

### 📦 Installer e procedura guidata
- Immagini della procedura guidata ridisegnate con il nuovo marchio, a **tre scale** (100%, 150%, 200%): nitide anche sugli schermi ad alta risoluzione (`assets/Create-BrandIcon.py`)
- Testi di benvenuto e di fine installazione riscritti; cartella di installazione per le nuove installazioni «Vyrn Presence» (gli aggiornamenti usano quella già scelta)

### ⚡ Velocità e fluidità
- **Controlli dei movimenti ~25 volte più rapidi** (717 → 30 ms su 9.000 movimenti): il confronto dipendenti × movimenti era un ciclo dentro un ciclo
- **Niente più blocco dopo l'importazione**: i nomi dei fogli arrivano dalla lettura già fatta, mentre prima il file veniva riaperto e riletto da capo sul thread dell'interfaccia (secondi di finestra ferma su fogli grandi)
- **Anteprima XML senza attese**: i colori della sintassi si compongono in un solo documento RTF (`XmlRtf`) invece di una selezione per ogni tag; la colorazione ora regge fino a 2 MB
- **Griglia dei movimenti**: le righe si aggiungono in blocco invece che una per volta
- Guardie di velocità nei test (`PerformanceGuardTests`)

### 🏢 Aziende
- Nuova voce **Aziende** nella barra laterale: anagrafica con codice PAGHE, ragione sociale e note, salvata nel database (schema 4, inclusa nei backup). Ogni codice usato in conversione entra da solo nell'elenco; suggerimenti nel campo Azienda e ragione sociale sotto al campo e nel piede della barra
- La barra laterale mostra i contatori di Aziende e Storico e l'azienda in uso; scorciatoie `Ctrl+4/5/6`

### 🪟 Applicazione e icona
- **Un solo PresenzeFlow per utente**: un secondo avvio riporta in primo piano la finestra già aperta (anche dal tray) invece di aprirne una seconda sullo stesso database
- **Nuova icona** con disegni dedicati per ogni dimensione (16–256 px): resta leggibile anche nella barra delle applicazioni; generata da `assets/Create-BrandIcon.py`

### 🔐 Certificato, aggiornamento e disinstallazione
- L'installer rende attendibile solo il certificato con l'impronta calcolata alla compilazione e non ripropone l'avviso di Windows se è già attendibile
- L'aggiornamento rifiuta un installer **non firmato** quando l'app installata è firmata (un certificato rinnovato resta ammesso)
- La disinstallazione toglie anche la cartella del programma se resta vuota

### 📄 Lettura e XML
- Accettati anche i fogli **.xlsm** (le macro non vengono eseguite)
- **Controllo del file salvato**: l'XML si rilegge e si verifica contro il tracciato TRRIPW (BOM, radice, codici, date, ore/minuti, duplicati) prima di darlo per consegnato

---

## [1.20.1] - Precedente

### 📄 XML riconosciuto da Zucchetti
- **Attributo al posto giusto**: `GenerazioneAutomaticaDaTeorico="N"` stava sul nodo `Dipendente`; il tracciato TRRIPW (Paghe Infinity) lo prevede sul nodo `Movimenti`. Messo sul nodo sbagliato PAGHE può ignorarlo e applicare l'orario teorico al posto dei movimenti comunicati
- **Un movimento per giorno e giustificativo**: righe dello stesso dipendente con stessa data e stesso giustificativo (es. due ferie sovrapposte) sono unite in un solo `Movimento` con le ore sommate

### 🔘 Pulsanti non più tagliati
- I pulsanti avevano larghezza fissa (110 px): «Salva abbinamenti», «Copia percorso» e simili venivano tagliati. Ora ogni pulsante si allarga al proprio testo; il test grafico controlla anche i pulsanti, oltre alle etichette

---

## [1.20.0] - Precedente

### 🗓️ Motore di conversione
- **Sabato lavorativo rilevato dalle date**: se le ore richieste non entrano nei giorni feriali ma entrano includendo i sabati (es. 48 h dal lunedì al sabato) il sabato riceve ore; se bastano i feriali resta libero; una richiesta del solo sabato non viene più scartata
- **Ore effettive dalle date**: una durata pari al numero dei giorni del periodo (es. «5» dal lunedì al venerdì) è letta come giorni e convertita in ore; ogni correzione automatica è segnalata nei Controlli
- Nuova opzione **Rileva in automatico sabati e ore dalle date** (attiva di default) e **Il sabato è sempre lavorativo** nelle impostazioni; i controlli non segnalano più i sabati rilevati come anomalia

### 🧹 AI locale e Ollama rimossi
- Eliminati dall'app e dall'installer: nessun download di Ollama o di modelli, nessuna pagina del wizard dedicata, nessuna domanda in disinstallazione, nessuna revisione AI. Mappatura delle colonne e dei codici restano manuali e sotto il tuo controllo
- Un Ollama già installato sul PC **non viene toccato né disinstallato**: si rimuove da Windows (Impostazioni → App) se non serve più
- La dashboard mostra le regole di conversione in uso al posto dello stato dell'AI; il pulsante **Regole** apre le impostazioni

### 🧪 Build e dettagli
- **Test indipendenti dall'Excel del cliente**: il progetto di test copiava `file presenze cliente.xlsx`; cancellato quel file dal repository, la build dei test (e quindi la Release 1.19.0) si fermava con «file non trovato». Ora il foglio di esempio viene generato dai test (`SampleWorkbook`)
- Menu del tray: voce **Notifiche di fine conversione** per attivarle o spegnerle

---

## [1.19.0] - Precedente

### 🎨 Interfaccia rifatta
- **Intestazione**: marchio e titolo, ricerca centrale con icona e testo guida visibile (il segnaposto restava nascosto dietro il campo), stato dell'anteprima e due pulsanti a icona (aggiornamenti con pallino quando c'è una versione nuova, impostazioni)
- **Dashboard**: quattro schede con icona, area di rilascio del foglio Excel (clic o trascinamento), avanzamento Importa → Verifica → Esporta, stato del sistema (file, database, aggiornamenti, AI) e ultime consegne con stato vuoto illustrato
- **Conversione**: fascia di comandi in alto (azienda, file, riepilogo, azioni) e area di revisione **a tutta larghezza** al posto della colonna di 300 px che tagliava i testi
- Nuovi controlli disegnati a mano (`IconButton`, `DropZone`, `MetricCard`, `StatusRow`, `StepperBar`, icone vettoriali) che misurano il testo: le etichette non vengono più tagliate

---

## [1.18.0] - Precedente

### 🚪 «Esci definitivamente» funziona
- Nel dialogo di chiusura il pulsante **Esci definitivamente** restituisce l'esito *Abort*, ma la finestra principale considerava valida solo la risposta *OK*: l'*Abort* veniva letto come «annulla» e il programma **non si chiudeva mai**. Ora solo la X e Esc annullano la chiusura
- Un'unica routine di uscita (`ExitApplication`), usata anche dal menu del tray, ferma comunque il ciclo dei messaggi; salvataggi e backup alla chiusura non possono più bloccarla
- «Non chiedere più» non è più preselezionato (un primo clic su «Riduci in tray» nascondeva per sempre il programma) e il menu del tray ha *Alla chiusura chiedi di nuovo*

### 🔔 Notifiche
- Una notifica arrivava **solo con la finestra nascosta**: con la finestra ridotta a icona o coperta da altre il fine conversione non veniva mai segnalato. Ora conta se l'utente sta davvero usando la finestra (visibile, non ridotta, attiva)
- La prima notifica dopo la comparsa dell'icona si perdeva (Windows non l'aveva ancora registrata): ora parte dopo un istante
- Un clic sulla notifica riporta la finestra davanti; con la finestra aperta l'icona non resta nel tray; una sola notifica di aggiornamento per versione
- I collegamenti del menu Start portano l'**AppUserModelID** dell'app: senza, Windows non attribuisce le notifiche al programma e può sopprimerle. Si disattivano con l'impostazione `notifications = false`

### 🔏 Firma e attendibilità
- L'installer propone **«Considera attendibile il certificato di firma»**: aggiunge il certificato agli archivi *dell'utente corrente* (editori attendibili e autorità attendibili; nessun privilegio di amministratore, Windows chiede conferma una volta) e lo toglie alla disinstallazione. Mai in installazione silenziosa, quindi nessun avviso durante gli aggiornamenti
- Il rilascio passa all'installer il certificato e la sua impronta; la **CI compila lo stesso ramo con un certificato usa e getta**, così un errore non emerge a rilascio partito
- Informazioni dell'editore nell'eseguibile (Company, Copyright)
- Resta valido il limite del certificato autofirmato: sui PC dove non è installato SmartScreen continua a diffidare. Per una firma riconosciuta da tutti serve un certificato di una CA pubblica o un servizio di firma gestito (vedi README)

---

## [1.17.0] - Precedente

### 🧭 Intestazione e barra laterale
- **Intestazione riordinata**: nella 1.16 la tabella dichiarava 3 colonne ma ne usava 4, quindi *Aggiornamenti*, *Impostazioni* e lo stato dell'anteprima **non comparivano**. Ora sono quattro colonne (marchio · titolo · ricerca · azioni), il marchio è centrato, la ricerca sta al centro dello spazio libero (al più 460 px) e i pulsanti hanno uno stile adatto al fondo scuro
- **Barra laterale che si adatta**: con la finestra larga mostra le etichette intere («Nuova conversione», «Storico esportazioni», 224 px); con quella stretta passa a 176 px con le etichette brevi («Conversione», «Codici», «Storico») invece di tagliarle con i puntini
- **Piede della barra**: stato dei dati locali («Database collegato», numero e età dei backup) e versione, sempre visibili

### 🔁 Conversione
- **Nome del file proposto**: `TRRIPW_<azienda>_<periodo>.xml` (es. `TRRIPW_AZ001_2026-08.xml`) invece del nome del foglio
- **Protezione dal doppio invio**: ogni consegna ha un'*impronta* dei movimenti (azienda, dipendente, giorno, giustificativo, ore, indipendente dall'ordine e dal formato dell'XML). Se si esportano di nuovo gli stessi movimenti l'app avvisa — mandare due volte lo stesso file a PAGHE duplica le assenze — e chiede conferma

### 🗄️ Database (schema 3)
- Ogni consegna conserva ora il **file di origine**, l'**impronta** e una **copia compressa dell'XML effettivamente scritto** (fino a 20 MB). Nello *Storico esportazioni* il nuovo pulsante **Salva copia…** lo risalva anche se l'originale è stato spostato o cancellato
- La copia esce dal database insieme alla voce dello storico (ultime 200 consegne), senza righe orfane
- **Migrazione sicura**: un database dello schema 2 viene aggiornato aggiungendo le colonne (lo storico esistente resta intatto) dopo un backup automatico `prima-di-aggiornare`

---

## [1.16.0] - Precedente

### 🔄 Aggiornamenti sempre collegati
- **Token iniettato in ogni build**: il workflow di rilascio legge il secret `PRESENZEFLOW_GITHUB_TOKEN`, lo **verifica** contro l'API (un token scaduto non viene mai distribuito) e lo scrive, offuscato, nel codice prima della compilazione (`tools/Inject-UpdateToken.ps1` → `EmbeddedUpdateToken`). Ogni installer esce con gli aggiornamenti già collegati, senza configurare nulla sul PC e senza token nel repository. Senza secret il rilascio si ferma (salvo la variabile `ALLOW_RELEASE_WITHOUT_UPDATE_TOKEN=true`)
- **Più token, nessun punto di rottura**: si prova prima l'ultimo che ha funzionato, poi ambiente, Impostazioni, file locale e infine quello incluso; se uno è scaduto o revocato (401/403/404) si passa al successivo
- **Rete instabile**: ogni richiesta ripete da sola i tentativi per i problemi transitori (rete, errori 5xx), lo scaricamento dell'installer riparte fino a 3 volte e dopo un controllo fallito per la rete si riprova dopo 5 minuti invece che all'ora piena
- **Anche con la finestra nascosta in tray**: il controllo periodico non si ferma più quando il programma è ridotto a icona; se c'è una versione nuova arriva una notifica
- **Proxy aziendale**: il proxy di Windows (con le credenziali dell'utente) non è più escluso
- **Limite di richieste di GitHub** riconosciuto e distinto da un token sbagliato
- **Checksum obbligatorio quando la Release lo dichiara**: se non si riesce a leggerlo l'installazione viene annullata invece di procedere con un file non verificato
- In Impostazioni il token incluso nell'app non viene mai mostrato né modificato: il campo resta per un token personale facoltativo

### 🗄️ Database sempre disponibile
- **Si ripara da solo all'avvio**: un file danneggiato viene messo da parte (cartella `corrupt`) e sostituito dall'ultimo backup valido; senza backup se ne crea uno nuovo; se la cartella dei dati non è utilizzabile si lavora su un database temporaneo e lo si dice. Un database di una versione più recente **non** viene mai sovrascritto
- **Backup automatici** (VACUUM INTO, consistenti anche con l'app in uso): all'avvio se l'ultimo ha più di 20 ore, alla chiusura se ne ha più di 6 e prima di ogni aggiornamento dello schema; si tengono gli ultimi 10
- **Checkpoint alla chiusura**: copiare `settings.db` non lascia indietro dati recenti
- Dal menu dell'icona in tray: *Stato del database…*, *Backup dei dati ora*, *Apri cartella dei dati*

### 🧹 Disinstallazione: conservare o eliminare i dati
- Domanda esplicita **«Vuoi conservare i dati?»** con tre esiti (conserva / elimina / annulla); l'eliminazione chiede una **seconda conferma** con «No» preselezionato
- Eliminando si rimuove l'intera cartella dei dati (database, backup, copie danneggiate, token, log), ma solo se il nome della cartella è quello atteso
- Disinstallazione silenziosa: i dati si **conservano** salvo `/DELETEDATA`

### ✅ Verifiche
- Nuovi test: codifica/decodifica del token incorporato, sorgente committato senza token, workflow (iniezione prima della compilazione e verifica del token), ordine dei token, limite di richieste, backup e potatura, ripristino da file danneggiato (con e senza backup), database più nuovo mai sovrascritto, riepilogo dello stato, domanda di disinstallazione

### ⚠️ Sicurezza del token incluso
Il token è offuscato, **non cifrato**: chi possiede l'eseguibile può estrarlo. Deve avere il solo permesso *Contents: Read-only* e vedere il solo repository delle Release; con un repository privato che contiene anche il codice sorgente, il token permetterebbe di leggerlo. Se il codice non va condiviso, pubblica le Release in un repository separato.

---

## [1.15.1] - Precedente

### 🩹 Correzioni
- **Finestra vuota e Excel non importabile**: nella 1.15.0 la costruzione dell'interfaccia passava da `Trace?.Measure(...)`; con la diagnostica di avvio spenta (il caso normale) l'operatore `?.` saltava l'intera chiamata e l'interfaccia non veniva mai costruita — nessun pulsante, nessuna dashboard, nessuna importazione. Ora le fasi di avvio passano da `MeasureStartup`, che esegue **sempre** l'azione e la misura solo se richiesto. Stessa correzione per l'inizializzazione degli aggiornamenti
- Nuovo test di regressione che vieta `Trace?.Measure(...)` nei sorgenti della finestra

---

## [1.15.0] - Precedente

### 🧭 Nuova struttura della finestra
- **Barra di navigazione laterale** con quattro sezioni — *Dashboard*, *Nuova conversione*, *Gestione codici*, *Storico esportazioni* — disegnata con la palette dell'app (voci alte 46 px, icona, barretta laterale e fondo chiaro sulla voce attiva), con navigazione da tastiera (frecce, Home/End) e nome accessibile per i lettori di schermo
- **Campo di ricerca nell'intestazione**: filtra i movimenti da qualunque sezione e porta l'utente dove i risultati si vedono
- Le schede dell'area di revisione restano dov'erano, dentro la sezione di conversione; le scorciatoie `Ctrl+0…5` aprono la sezione giusta invece di cambiare scheda in una sezione nascosta
- Larghezze dei comandi dell'area di revisione ritarate (ricerca 170 px, *Copia XML* 92, *Scarica XML* 100) perché le tre sezioni nuove non possono comprimere l'area di lavoro nemmeno nella finestra minima di 1024 px

### 📊 Dashboard
- **Card di riepilogo** con richieste lette, dipendenti, movimenti generati e ore assegnate, tutte calcolate da un unico posto (`DashboardSummary`): nessun conto duplicato nel codice dell'interfaccia
- **File sorgente**: nome del foglio e stato della conversione in una frase; **azioni rapide** (apri file, genera anteprima, gestione codici, storico) e **ultime esportazioni** con doppio clic per aprire la cartella del file
- Importando un file l'app passa da sola alla sezione di conversione: i codici da verificare sono cambiati

### 🗂️ Gestione codici e 🕘 Storico esportazioni
- **Gestione codici** è ora una sezione della finestra: le due griglie (dipendenti e giustificativi) sono estratte in un pannello riusabile (`MappingEditorPanel`) usato **sia** dalla sezione **sia** dal dialogo "Codici PAGHE": una sola logica di validazione, non due copie
- **Storico esportazioni** (nuovo): ogni XML salvato viene registrato nel database locale con data, file, cartella, dipendenti, movimenti e ore; la sezione li elenca con *Apri cartella*, *Copia percorso* e *Svuota storico* (i file su disco non vengono mai toccati). Lo storico è limitato alle **200 esportazioni** più recenti
- Database locale portato allo **schema 2** con la tabella `Exports`; preferenze e abbinamenti già presenti restano intatti, e un database di versione superiore viene rifiutato invece di essere aperto a metà

### ⚡ Apertura più rapida
- **`PublishReadyToRun`** nel publish: il codice gestito dell'interfaccia viene compilato in anticipo, così la finestra non paga la compilazione JIT al primo utilizzo (vale per la Release; le risorse localizzate delle dipendenze non vengono più incluse nel pacchetto)
- **Nessuna attesa per dati che non servono a disegnare**: lo storico delle esportazioni e lo stato del motore AI locale vengono letti **dopo** che la finestra è visibile
- **Preferenze di avvio lette in una sola query** (prima ogni impostazione apriva la propria connessione a SQLite)
- **Diagnostica su richiesta**: con la variabile d'ambiente `PRESENZEFLOW_TRACE_STARTUP=1` l'app scrive in `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\startup.log` la durata di ogni fase di avvio, ordinata dalla più lenta: senza numeri la velocità resta un'impressione

### ✅ Verifiche
- **15 test in più**: dashboard (conteggi e descrizioni), storico (data leggibile "oggi/ieri", formattazione, riepilogo), persistenza e ordinamento dello storico nel database, rifiuto di uno schema più recente
- **Il test grafico controlla ogni sezione**: dashboard, conversione (con tutte le sue schede), gestione codici e storico vengono disegnati e verificati (nessun testo tagliato, nessun controllo sovrapposto, nessun colore di sistema) — prima veniva controllata solo la schermata aperta all'avvio

---

## [1.14.0] - Precedente

### 🪟 Struttura Windows
- **Il manifesto dichiara le versioni di Windows supportate**: mancava il blocco `supportedOS`, quindi Windows rispondeva a `Environment.OSVersion` con la versione "compatibile" 6.2. Il titolo scuro funzionava per tentativi, ma gli **angoli arrotondati di Windows 11** (concessi solo da build 22000) non venivano mai richiesti: la finestra restava a spigoli vivi pur essendo su Windows 11. Ora la build riportata dal sistema è quella reale
- **Percorsi lunghi abilitati** (`longPathAware`): un foglio Excel in cartelle profondamente annidate si apre anche oltre il limite di 260 caratteri
- **Identità Windows unica** (`AppIdentity`): l'AppUserModelID `DeGioia.PresenzeFlow` viene comunicato al processo *prima* che nasca la finestra, così finestra, notifiche nell'area di notifica e collegamento bloccato sono la stessa applicazione nella barra delle applicazioni
- **Mutex condiviso con l'installer**: l'app tiene aperto il mutex `DeGioia.PresenzeFlow.Setup` e `Installer.iss` lo dichiara in `[Setup] AppMutex`; un aggiornamento riconosce così l'esecuzione in corso e chiude il programma in modo ordinato, invece di fallire sui file in uso. Un test impedisce che i due nomi divergano

### 🧾 Tracciato Zucchetti
- **Limiti del tracciato in un unico posto**: `TrriPwXmlWriter` dichiara le lunghezze documentate (codice azienda 6, codice dipendente 7, giustificativo 3, ore 0-99, minuti 0-59, massimo 250.000 movimenti per file) e le applica al valore. Generazione e scrittura non possono più usare limiti diversi da quelli del documento Zucchetti
- **Errori che dicono chi correggere**: un codice fuori misura ora nomina il dipendente e la richiesta — *"Il codice PAGHE di “Rossi Mario” deve contenere da 1 a 7 caratteri"* — invece di parlare di "un codice"; anche ore e minuti fuori limite indicano persona e data del movimento
- **Giustificativo 999 segnalato**: il tracciato riserva il codice 999 ai movimenti che PAGHE deve scartare. Usarlo è legittimo (neutralizza una riga senza toglierla dall'Excel), ma la scheda **Controlli** ora lo dice come NOTA — "quel giorno non riceverà ore" — invece di lasciarlo passare in silenzio

### ♿ UI/UX e accessibilità
- **Nomi accessibili sui comandi**: pulsanti, campi, griglia, anteprima e schede hanno `AccessibleName`/`AccessibleDescription`: un lettore di schermo annuncia "Seleziona file Excel" invece di "pulsante". Le tre schede delle Impostazioni e le schede dell'area di revisione si presentano con il loro titolo
- **Suggerimenti coerenti col nome**: gli stessi testi diventano il tooltip dei comandi dell'intestazione, del campo di ricerca, di *Copia XML*, *Salva XML*, *Genera anteprima* e *Seleziona file Excel*

### ✅ Verifiche
- **5 nuovi test di logica**: limiti del tracciato al confine esatto di ogni campo, messaggi di validazione che nominano il dipendente, nota sul giustificativo 999, mutex condiviso fra app e installer, manifesto con `supportedOS` e `longPathAware`

---

## [1.13.0] - Precedente

### 🖼️ Grafica: fuori i colori di sistema che restavano
- **Il dialogo Impostazioni è diviso in tre schede** (**Conversione**, **AI locale**, **Aggiornamenti**): ogni scheda entra intera nella finestra, quindi non compare più la barra di scorrimento grigia di sistema che prima accompagnava sempre il dialogo (il contenuto era alto 802 px in uno spazio di 632)
- **Le altezze delle schede sono calcolate dalle righe** (`SettingsPageLayout`): la scheda *AI locale* era alta 150 px per righe che ne chiedevano 142 solo per i campi usati, quindi l'ultima riga di testo (modello installato) finiva fuori dal bordo della card; ora l'altezza è la somma delle righe, in un unico posto e verificata da un test
- **Le novità di una Release** non usano più la casella di testo con la barra nativa: il testo viene impaginato alla larghezza disponibile e scorre con una **barra disegnata con il tema dell'app** (rotellina, trascinamento del pollice, clic sulla pista); un pulsante **Copia note** copia il testo negli appunti e conferma per qualche secondo
- **Il campo delle chiusure aziendali** non mostra più la barra grigia di sistema: si scorre con la rotellina o con i tasti, come negli altri campi
- **La versione in esecuzione** compare anche nell'intestazione delle Impostazioni (`PresenzeFlow 1.13.0 · regole di conversione, aggiornamenti e modello AI locale`)
- **I pulsanti del piè di pagina non sono più tagliati**: *Salva impostazioni* e *Annulla* erano alti 40 px in una riga che ne lasciava 30, quindi il bordo inferiore finiva fuori dalla finestra; ora l'altezza del piè di pagina è la somma del messaggio di errore e dei pulsanti (`SettingsPageLayout`)
- **La firma digitale dell'eseguibile** è passata nella scheda **Aggiornamenti** (riguarda l'aggiornamento, non la conversione) e la finestra è più compatta: 760x640 invece di 760x780

### 📋 Area di revisione
- **Stato vuoto nelle griglie**: quando non c'è nulla da mostrare la griglia lo dice ("Importa un foglio Excel…" oppure "Nessun movimento corrisponde alla ricerca." per un filtro senza risultati) invece di restare un rettangolo bianco che sembra un caricamento non riuscito
- **Pulsanti non più tagliati**: nella scheda **Controlli** la fascia dei comandi era più bassa dei pulsanti (28 px utili per pulsanti alti 31) e il loro bordo inferiore veniva tagliato di 3 px; nella fascia dell'area di revisione un pixel di spaziatura interna tagliava *Copia XML* e *Scarica XML*

### ✅ Verifiche
- **184 test di logica** (17 nuovi): geometria della barra temizzata (pollice sempre dentro la pista, scorrimento sempre dentro i limiti, rotellina, trascinamento, clic sulla pista, contenuto che entra senza barra) e conti delle schede Impostazioni (ogni scheda entra nella finestra minima e in quella predefinita, ogni riga lascia spazio al controllo che ospita, le tre schede si costruiscono dalle righe condivise e il dialogo non usa barre di scorrimento di sistema)
- **Il test grafico copre tutte le schede**: oltre alla finestra principale (che ora viene disegnata anche su *Anteprima XML* e *Controlli*, prima solo sulla scheda aperta all'avvio) sono controllati i due dialoghi che ne restavano fuori, **Impostazioni** (tutte e tre le schede) e **Novità** (con note lunghe, quindi con la barra da disegnare): nessun colore di sistema, nessun testo tagliato, nessun controllo sovrapposto

---

## [1.12.0] - Precedente

### 🪟 Grafica Windows
- **La barra del titolo segue il tema dell'app**: era chiara sopra l'intestazione scura, e la finestra sembrava composta da due programmi. Ora il titolo è scuro anche a livello di sistema, con gli **angoli arrotondati** su Windows 11. L'attributo si adatta alla versione di Windows (20 da Windows 10 1903, 19 sulle precedenti) e si prova anche l'alternativa, perché la versione riportata dal sistema può essere quella dichiarata nel manifesto dell'eseguibile; se il sistema non lo conosce la finestra resta standard, senza errori
- Il chrome nativo non tocca il disegno dei contenuti: nessun colore di sistema entra nella verifica grafica
- Il **titolo della finestra** e il tooltip dell'icona nell'area di notifica mostrano la **versione in esecuzione** (`PresenzeFlow 1.12.0`), letta dall'eseguibile: senza aprire le Impostazioni si capisce subito quale build è installata, utile ora che le Release automatiche si succedono anche più volte al giorno

### 📥 Trascinamento del foglio Excel
- Il file `.xlsx` si può **trascinare direttamente sulla finestra**, senza aprire il dialogo di apertura: vengono accettati solo fogli Excel esistenti (cartelle, file di altro tipo, file temporanei di Excel e trascinamenti multipli vengono ignorati: si prende il primo foglio valido) e durante una conversione il trascinamento non è accettato
- Dialogo di apertura e trascinamento passano dallo **stesso percorso** (`LoadWorkbookAsync`), quindi si comportano allo stesso modo

### 🔏 Firma digitale visibile nell'app
- In **Impostazioni → Aggiornamenti** compare lo stato dell'eseguibile in esecuzione: *"Eseguibile firmato digitalmente da DeGioia (valido fino al 11/10/2029)"* oppure *"Eseguibile non firmato digitalmente"*: si vede subito se il file installato è quello pubblicato
- La lettura della firma non solleva mai eccezioni (eseguibile non firmato, file illeggibile, sistema che non sa leggerla)

### 🔐 Diagnostica della firma in Release
- I due secret del certificato vengono **ripuliti da spazi e a capo** prima dell'uso: un copia-incolla li aggiunge facilmente e il `.pfx` non si apriva più con l'errore di Windows *"The specified network password is not correct"*, che sembra una password sbagliata anche quando è solo un a capo di troppo
- Se il `.pfx` non si apre, il log mostra **lunghezza e impronta SHA-256** dei due secret (mai i valori): con quelle si riconosce subito un `.pfx` troncato o una password presa da un altro certificato
- `tools/New-CodeSigningCertificate.ps1` salva la password anche in `codesign\PASSWORD.txt`: trascriverla a mano era il modo più facile di sbagliarla

### ✅ Verifiche
- **167 test di logica, tutti verdi** (15 nuovi): scelta dell'attributo del titolo scuro e degli angoli arrotondati per versione di Windows, applicazione del chrome che non solleva errori, regole del trascinamento (estensioni accettate, primo file valido, cartelle e file inesistenti ignorati), editore letto dal soggetto del certificato e riga della firma
- Il titolo della finestra è **assegnato in un solo posto e con la versione**: la guardia impedisce che una seconda assegnazione lo sovrascriva (era proprio il difetto trovato dal test grafico in CI: il costruttore lo metteva con la versione e la costruzione del layout lo rimetteva senza)
- Verifica grafica (solo Windows/CI) che il titolo della finestra porti davvero la versione dell'eseguibile, così titolo e versione non possono separarsi: è la verifica che ha trovato l'assegnazione doppia del titolo

---

## [1.11.0] - Precedente

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
