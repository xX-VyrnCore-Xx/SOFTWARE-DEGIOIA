# PresenzeFlow

PresenzeFlow è un'applicazione Windows 11 x64 per convertire un foglio Excel di richieste di assenza nel tracciato XML TRRIPW per Paghe Infinity. Con interfaccia moderna, analisi dati avanzata e validazione completa.

## Installazione

L'unica modalità di installazione supportata è l'installer Windows basato su Inno Setup: compila `Installer.iss` con Inno Setup 6 per generare `dist/PresenzeFlow-Setup-1.10.0.exe`. L'installer copia l'applicazione e la libreria nativa SQLite (`e_sqlite3.dll`) in `%LOCALAPPDATA%\Programs\PresenzeFlow` e crea i collegamenti nel menu Start e sul desktop. Non richiede privilegi amministrativi.

Durante l'installazione il wizard permette di aggiungere l'**AI locale** (Ollama + un modello, opzionale e deselezionabile): la conversione funziona comunque senza. Nella pagina dedicata scegli la dimensione del modello — *Veloce* `qwen2.5:0.5b` (~400 MB), *Consigliato* `qwen2.5:1.5b` (~1 GB), *Qualità* `qwen2.5:3b` (~2 GB) — oppure *nessun modello adesso*. Se **Ollama è già presente** (anche in una cartella personalizzata) non viene reinstallato e il modello viene scaricato con una **barra di avanzamento annullabile**. L'eseguibile e l'installer vengono **firmati** quando il certificato è configurato (vedi "Firma digitale"): un file firmato viene controllato più in fretta al primo avvio.

**Primo avvio**: al primo lancio subito dopo l'installazione Windows Defender analizza l'eseguibile (circa 160 MB non firmato digitalmente) e la finestra può impare anche 15-20 secondi ad apparire; dai lanci successivi l'avvio è immediato (misurato: ~0,5 s). La firma digitale dell'eseguibile riduce ulteriormente questa attesa.

Durante la disinstallazione puoi scegliere se rimuovere il programma mantenendo le impostazioni, se cancellare anche il database locale di codici azienda, dipendenti e giustificativi e, se presente, se eliminare il modello AI scaricato (Ollama stesso non viene mai rimosso). In modalità silenziosa l'installazione non cancella mai i dati utente.

## Utilizzo

1. **Azienda Zucchetti**: Inserisci il codice azienda ufficiale PAGHE (fino a 6 caratteri).
2. **File sorgente**: Importa un file `.xlsx` con un foglio di richieste. Il controllo del file limita:
   - Dimensione archivio ZIP: 50 MB
   - Dimensione espansa: 150 MB
   - Righe foglio: 100.000
   - Colonne: 256
   - Celle utilizzate: 2.000.000
   
   L'app riconosce intestazioni italiane e inglesi comuni e numeri decimali con virgola o punto; se il layout non è riconosciuto, mostra la schermata per associare le colonne.

3. **Codici dipendente**: Se il foglio contiene una colonna `Codice PAGHE` o `CodDipendenteUfficiale`, il codice ufficiale PAGHE (fino a 7 caratteri) viene letto e memorizzato localmente. Per i nominativi senza codice, inseriscilo una volta nel programma: verrà riutilizzato nelle conversioni successive.

4. **Giustificativi**: Associa i valori presenti nella colonna `Policy` ai codici giustificativo ufficiali PAGHE (fino a 3 caratteri). Questi codici non sono dedotti dall'AI.

5. **Anteprima**: Genera e revisiona l'anteprima:
   - Ogni durata viene ripartita in modo uniforme sui giorni lunedì-venerdì dell'intervallo
   - Esclude le festività nazionali italiane
   - Gli eventuali minuti residui sono assegnati in ordine cronologico
   - Utilizza `Duration in range` quando presente
   - Limita i movimenti al periodo `From Date`/`To Date` del foglio quando disponibile
   - Le richieste dello stesso dipendente che generano movimenti sullo stesso giorno sono segnalate per la revisione (non vengono scartate o modificate automaticamente)

6. **Revisione, assistente AI ed export**: Nella griglia puoi correggere codice dipendente, giustificativo, data, ore e minuti: l'XML viene aggiornato automaticamente (con una breve pausa dopo le modifiche, per restare fluido). Nella scheda **Assistente AI** puoi invece descrivere a parole la correzione e l'AI la applica all'anteprima. Il file si salva dove vuoi e il programma ricorda l'ultima cartella usata. Le correzioni ai codici fatte in griglia vengono salvate nel database locale.

7. **Chiusura**: alla prima chiusura puoi scegliere una sola volta (o ricordare la scelta) se uscire definitivamente o restare in tray: in background ricevi una notifica quando una conversione termina.

Il file generato contiene solo i movimenti di assenza presenti nell'Excel; non inventa ore ordinarie o presenze. Prima dell'esportazione vengono verificati codici, limiti del tracciato, associazioni dipendente/codice e caratteri XML; l'output è ordinato in modo deterministico, scritto in UTF-8 senza BOM e sostituito atomicamente per evitare file parziali.

## AI locale opzionale

L'app usa un modello locale (`qwen2.5:1.5b` per impostazione predefinita) per tre attività:

1. **Riconoscimento assistito delle colonne** quando il foglio non ha intestazioni standard.
2. **Proposta di abbinamenti** tra le etichette `Policy` e i giustificativi già configurati.
3. **Assistente conversazionale** (scheda **Assistente AI** nell'area di revisione, oppure pulsante **Chatta con AI**): scrivi in italiano cosa vuoi correggere e l'app lo trasforma in modifiche controllate sui movimenti.

Il modello può essere installato dal wizard di installazione oppure da **Configura AI locale**, che scarica l'installer ufficiale di Ollama e poi il modello. Se **Ollama è già installato** (per esempio perché usato da altri programmi) né l'installer né il programma lo reinstallano: viene scaricato **solo il modello**; se il servizio non è attivo, l'app prova ad avviarlo e attende che risponda. Se è già presente un altro modello compatibile (es. `qwen2.5:0.5b` o `3b`) viene usato automaticamente. Il runtime supera 1 GB e il modello richiede altro spazio; l'AI è opzionale.

### Assistente AI: cosa può fare sul documento

L'AI **non scrive mai direttamente l'XML**. Descrivi a parole la correzione e l'applicazione la traduce in un insieme di azioni validate da codice:

- impostare ore e minuti sui movimenti che corrispondono a un filtro (dipendente, giustificativo, giorno o intervallo);
- spostare le date di N giorni (avanti o indietro);
- riscalare le durate (es. dimezzare);
- cambiare il **codice giustificativo**, ma solo riusando codici già configurati;
- rimuovere movimenti.

Un filtro senza campi vale "tutti i movimenti". Esempi di richieste: *"metti le ferie di Rossi a 4 ore il 17/08"*, *"sposta di un giorno tutti i movimenti del 20 agosto"*, *"dimezza le ore di Mario Bianchi"*, *"rimuovi i permessi non retribuiti di luglio"*.

Ogni modifica viene mostrata nel riquadro della conversazione con l'esito (quanti movimenti aggiornati, rimossi o ignorati) e l'anteprima/XML si rigenerano subito. Se l'AI propone un codice non configurato, la modifica viene scartata e viene segnalato. La conversazione è salvata nel database locale e viene ripristinata al riavvio (puoi azzerarla con **Nuova chat**).

**Comunicazioni e privacy**: Il programma comunica con `127.0.0.1:11434` (modello locale) e, separatamente, con `api.github.com` solo per il controllo degli aggiornamenti (vedi la sezione "Aggiornamenti automatici"). Per le attività 1 e 2 invia esclusivamente intestazioni, etichette `Policy` e mapping già configurati, mai nominativi o righe del foglio. Per l'assistente conversazionale invia al modello locale il **riepilogo dell'anteprima corrente** (dipendenti, codici, date e giustificativi): serve a capire cosa correggere e **non esce mai dal tuo PC**. Nessun dato viene inviato a servizi esterni. L'AI non può introdurre codici ufficiali nuovi: i suggerimenti vanno verificati e confermati.

## Dati e tracciato

**Database**: `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\settings.db`

SQLite è configurato con:
- WAL journal mode per migliori prestazioni e concorrenza
- Attese controllate per accessi concorrenti
- Sincronizzazione completa delle scritture (`PRAGMA synchronous FULL`)
- `secure_delete` su tutte le pagine
- Controllo d'integrità all'avvio (`INTEGRITY_CHECK`)
- Tutti i valori passati tramite query parametrizzate

⚠️ **Nota importante**: Il database non è cifrato. Proteggi l'account Windows e il dispositivo secondo le policy aziendali. La disinstallazione consente di mantenere o rimuovere il database.

**Formato XML**: Il documento Zucchetti fornito (pagine 14-16) indica i campi del nodo `Dipendente` e dei movimenti: 
- Codice azienda ufficiale
- Codice dipendente ufficiale
- Codice giustificativo ufficiale
- Data in formato `aaaa-mm-gg`
- Ore e minuti

Prima del primo utilizzo produttivo, verifica un XML di prova nell'ambiente Zucchetti: i codici, le abilitazioni e le configurazioni di importazione dipendono dall'installazione PAGHE aziendale.

## Build e test

Richiede .NET 8 SDK e Windows x64:

```powershell
# Esegui i test di validazione
dotnet test .\PresenzeXmlConverter.Tests\PresenzeXmlConverter.Tests.csproj

# Compila e pubblica l'app (input dell'installer)
dotnet publish .\PresenzeXmlConverter\PresenzeXmlConverter.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\dist\app

# Compila l'installer Windows (richiede Inno Setup 6, lanciare dalla root del progetto)
# iscc .\Installer.iss  ->  dist\PresenzeFlow-Setup-1.10.0.exe
```

La compilazione elimina automaticamente i vecchi `dist\PresenzeFlow-Setup-*.exe`: nella cartella `dist` resta quindi **sempre e solo l'ultima build** (mentre `dist\app` e le cartelle `bin`/`obj` sono ignorate da git, vedi `.gitignore`).

⚠️ **Nota per chi cura la build**: il `PublishSingleFile` non include le librerie native, quindi `dist/app/e_sqlite3.dll` **deve** essere copiato dall'installer accanto a `PresenzeFlow.exe`. Senza quel file l'app non può aprire il database locale e termina all'avvio (ora con un messaggio d'errore esplicito invece di un silenzioso crash).

Per rigenerare l'icona e le immagini della procedura guidata, esegui `.\assets\Create-BrandAssets.ps1`.

## Rilasci automatici (GitHub Actions)

Ogni nuova versione viene pubblicata come **Release GitHub** con l'installer Windows allegato, tramite il workflow `.github/workflows/release.yml`. Non serve compilare a mano.

Il workflow parte:

- a ogni push su `main`: se la versione del progetto non ha ancora una Release, la crea;
- a ogni tag `vX.Y.Z`;
- manualmente da **Actions → Release → Run workflow**.

Prima di pubblicare, il workflow **verifica che `Version` nel `.csproj` e `AppVersion` in `Installer.iss` coincidano** (altrimenti si ferma senza pubblicare nulla), esegue i test, compila l'app single-file `win-x64`, genera l'installer con Inno Setup e allega `PresenzeFlow-Setup-X.Y.Z.exe` alla Release.

Per preparare una nuova versione:

```powershell
.\tools\New-Version.ps1 -Version 1.7.1   # aggiorna csproj e Installer.iss in un colpo
git commit -am "Rilascio 1.7.1"
git tag v1.7.1
git push origin main --follow-tags
```

Il tag è facoltativo: basta il push su `main` e il workflow crea da sé il tag `vX.Y.Z` e la Release corrispondente. Se la Release esiste già, il workflow non la duplica.

## Aggiornamenti automatici (Release GitHub)

PresenzeFlow controlla le Release pubblicate sul proprio repository GitHub e propone di aggiornarsi. **Il repository è privato**, quindi ogni richiesta va autenticata con un token di sola lettura.

### Attivare il controllo (una volta per PC)

1. Su GitHub crea un **fine-grained personal access token** con un solo permesso: **Contents → Read-only**, limitato al repository `xX-VyrnCore-Xx/SOFTWARE-DEGIOIA`.
2. Rendilo disponibile in uno dei due modi:
   - **variabile d'ambiente** `PRESENZEFLOW_GITHUB_TOKEN`;
   - oppure file **`%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\github.token`** contenente solo il token.

Il token **non è mai incluso nell'eseguibile** e non viene mai scritto nel database: resta nella configurazione del PC. Senza token il controllo automatico è inattivo e compare un messaggio che spiega come configurarlo.

### Come funziona

- **All'avvio**, dopo qualche secondo, l'app controlla in silenzio l'ultima Release.
- Se esiste una versione più recente, mostra una finestra con **le novità** e i pulsanti **Aggiorna ora** / **Più tardi** (un aggiornamento rimandato non viene riproposto a ogni avvio, ma resta disponibile dal tray).
- **Aggiorna ora**: l'installer viene scaricato con barra di avanzamento e **verificato con il checksum SHA-256** pubblicato nella Release. Poi l'app si chiude, l'installer aggiorna e **PresenzeFlow si riapre da solo**.
- **Dopo l'aggiornamento** viene mostrata automaticamente la finestra **Novità** con il riepilogo delle modifiche (le note sono salvate in locale, quindi funzionano anche senza rete).
- Dal menu dell'icona in tray: **Cerca aggiornamenti…** e **Novità di questa versione**.

Per disattivare il controllo automatico (mantenendo quello manuale) imposta la preferenza `auto-update-check` a `false` nel database locale.

Per usare un feed diverso (es. un mirror interno) imposta la preferenza `update-endpoint` con l'URL completo dell'API della Release.

**Rete**: l'unico traffico verso Internet è il controllo delle Release su `api.github.com`, autenticato con il token locale. L'AI resta rigidamente in locale su `127.0.0.1:11434`.

## AI locale (Ollama)

L'assistente AI è **opzionale** e gira interamente sul PC (Ollama su `127.0.0.1:11434`); la conversione Excel → XML non ne ha bisogno.

### In fase di installazione
- l'installer scarica l'installer ufficiale di Ollama **solo se il motore non è già presente**; il download mostra una barra di avanzamento reale, si può annullare e viene ritentato fino a tre volte se la rete è instabile;
- Ollama viene riconosciuto anche nelle **installazioni con cartella personalizzata** (`OllamaSetup.exe /DIR=…`), cercando anche nel registro delle installazioni e nel `PATH`;
- il modello scelto viene scaricato **solo se non è già presente**; la scelta è ricordata in `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\ai-model.txt` e riproposta agli aggiornamenti successivi;
- in disinstallazione viene rimosso **solo il modello scelto per PresenzeFlow**: Ollama e gli altri modelli restano intatti.

### Dall'app (menu "Configura AI locale")
- se Ollama è installato ma non attivo, l'app prova ad avviarlo e attende che risponda (fino a 15 secondi): nessuna reinstallazione;
- se Ollama manca, l'app prova prima con **winget** (`Ollama.Ollama`, modalità silenziosa) e, se winget non è disponibile o fallisce, scarica l'installer ufficiale con barra di avanzamento; se l'installazione silenziosa non produce risultati viene riproposto l'installer visibile;
- il download del modello avviene in streaming con **percentuale reale** (byte di tutti i blocchi dello stream `/api/pull`), annullabile; prima di partire l'app verifica che ci siano **almeno 2,5 GB liberi**;
- il modello in uso è quello **preferito** (scelta in installazione, poi quella salvata dall'utente) e il confronto avviene sul nome completo con il tag, quindi non viene richiesto un modello diverso da quello installato.

## Firma digitale (opzionale)

L'eseguibile e l'installer possono essere **firmati**. La firma identifica l'editore e riduce l'attesa del primo avvio (Windows Defender controlla con più fiducia un eseguibile firmato).

### Attivare la firma (una volta sola, su Windows)

```powershell
.\tools\New-CodeSigningCertificate.ps1
```

Lo script crea un **certificato di firma autofirmato** per l'azienda e prepara la cartella `codesign/` (esclusa da git, non committarla) con:

- `PresenzeFlow-codesign.pfx.base64.txt` → da incollare nel secret GitHub **CODE_SIGNING_PFX_BASE64**
- la **password** stampata dallo script → secret **CODE_SIGNING_PFX_PASSWORD**
- `PresenzeFlow-codesign.cer` → certificato pubblico da rendere attendibile sui PC

Il workflow di Release firma eseguibile e installer **solo se i due secret esistono**: senza certificato la build procede normalmente, non firmata.

### Rendere attendibile il certificato sui PC

Su ogni PC con l'app installata (per-utente, **senza privilegi di amministratore**):

```bat
certutil -addstore -user Root PresenzeFlow-codesign.cer
certutil -addstore -user TrustedPublisher PresenzeFlow-codesign.cer
```

In azienda la stessa operazione si distribuisce con Criteri di gruppo; per rendere il certificato valido per **tutto il PC** si usano gli stessi comandi da prompt elevato **senza** `-user`.

### Nota importante

Un certificato **autofirmato non elimina** l'avviso SmartScreen per chi scarica l'installer da Internet: diventa attendibile solo sui PC dove il certificato pubblico è installato. Per una firma riconosciuta da tutti serve un certificato emesso da una CA pubblica (a pagamento) o un servizio di firma gestito. Senza firma l'app funziona comunque: Windows mostra solo l'avviso "editore sconosciuto" al primo avvio.

## Novità v1.10.0

- **Installazione dell'AI locale rifatta**: download di Ollama con barra di avanzamento, annullamento e fino a 3 tentativi automatici (prima era un comando silenzioso che sembrava bloccato)
- **Scelta del modello in fase di installazione**: Veloce (~400 MB), Consigliato (~1 GB), Qualità (~2 GB) o nessun modello; la scelta viene riproposta agli aggiornamenti successivi
- **Niente download inutili**: se il modello è già presente il passo viene saltato; Ollama viene riconosciuto anche se installato in una cartella personalizzata (registro + `PATH`) e il `pull` usa il percorso corretto (prima con Ollama in un'altra cartella il modello non veniva mai scaricato)
- **Installazione più rapida e diagnosticabile**: `MinVersion` (Windows 10 1607) con messaggio chiaro sui sistemi non supportati, compressione parallela, log di ogni installazione in `%TEMP%`, token di GitHub rimosso in disinstallazione
- **Nell'app**: installazione di Ollama tramite winget quando disponibile (con ritorno automatico all'installer ufficiale), download del modello con percentuale reale e annullabile, controllo dello spazio su disco, modello preferito coerente con l'installer
- **91 test di logica** (31 nuovi) e controlli automatici sullo script dell'installer: costanti `{#…}` definite, blocchi Pascal bilanciati, versione allineata al `.csproj`

## Novità v1.9.0

- **Ollama già installato? Niente reinstallazione**: l'installer scarica solo il modello e l'app, se trova Ollama presente ma non attivo, prova ad avviarlo e attende che risponda
- **Installazione più rapida**: compressione a livello "normal" (decompressione più veloce e meno memoria), passi di Ollama saltati quando non servono
- **Apertura più rapida**: il font monospaziato viene scelto senza enumerare tutti i font di sistema e il controllo d'integrità del database viene eseguito una volta a settimana invece che a ogni avvio
- **Firma digitale opzionale** (autofirmata aziendale) di eseguibile e installer, attivabile con due secret GitHub
- **Grafica**: barra di avanzamento del download disegnata con il tema dell'app (niente grigio di sistema) e intestazione sfumata anche nella finestra "Novità"
- **README aggiornato** all'ultima versione + rimosse la cronologia duplicata e i file generati non usati dal build

## Novità v1.8.0

- **Aggiornamenti dalla Release GitHub** (repo privata): controllo silenzioso all'avvio, finestra con le novità e pulsanti Aggiorna ora / Più tardi
- **Download verificato**: installer scaricato con barra di avanzamento e **checksum SHA-256** (pubblicato nella Release e verificato prima di installare)
- **Aggiornamento guidato**: installazione silenziosa, riavvio automatico dell'app e finestra **Novità** con il riepilogo delle modifiche dopo l'aggiornamento
- **Token di sola lettura** letto da `PRESENZEFLOW_GITHUB_TOKEN` o dal file `github.token`: mai incorporato nell'eseguibile
- **Voci nel menu tray**: "Cerca aggiornamenti…" e "Novità di questa versione"
- **13 nuovi test** per l'interpretazione dei tag, il confronto versioni e il checksum

## Novità v1.7.0

- **Assistente AI conversazionale**: nuova scheda **Assistente AI** in cui chatti in italiano e l'AI **agisce sull'anteprima** (ore, date, durate, codici, rimozioni) tramite azioni validate; l'XML non viene mai scritto direttamente dall'AI
- **Conversione più fedele**: se il foglio esprime la durata in **giorni** (intestazioni `Giorni`, `Days`, `gg`) viene convertita in ore usando la giornata lavorativa standard (8h), evitando di importare "2 giorni" come "2 ore"
- **Preferenze e codici sempre salvati**: il codice azienda si salva da solo poco dopo la digitazione, la posizione/dimensione della finestra e l'ultima cartella usata sono ricordate, la cronologia della chat persiste e i codici corretti nella griglia vengono riportati nel database locale
- **Grafica**: striscia sfumata in cima all'header, nuova palette con accento ciano e pannello di chat temizzato coerente con il resto dell'interfaccia
- **10 nuovi test di logica** per le modifiche dell'assistente e la conversione giorni→ore (46 test di logica totali, tutti verdi)

## Novità v1.6.0

- **Palette colori unica**: tutti i colori e i font di finestra e dialoghi sono definiti una sola volta (`Theme.cs`); finiti i blu diversi per lo stesso pulsante e i bordi di tono differente
- **Pulsanti professionali**: stati hover, premuto e disabilitato coerenti; un pulsante disabilitato ora si vede disabilitato
- **Niente più grigio di sistema**: la striscia delle schede (e quella del dialogo abbinamenti) è disegnata dall'app e segue il tema
- **Campi di testo arrotondati** con bordo blu al focus e testo guida per codice azienda e ricerca
- **Bug grafici corretti**: scalatura DPI doppia rimossa, testo non più tagliato, allineamento di placeholder e testo nei campi, ricerca larga e leggibile
- **Test di regressione grafica**: la UI viene renderizzata e controllata pixel per pixel a ogni build (34 test)

## Novità v1.5.1

- **Ripristinato l'avvio dell'app installata**: l'installer copiava soltanto `PresenzeFlow.exe`, omettendo `e_sqlite3.dll`; l'app quindi terminava all'istante senza mostrare finestre. Ora la libreria nativa viene installata e l'app si avvia correttamente anche subito dopo l'installazione (spunta "Avvia PresenzeFlow")
- **Niente più crash silenziosi**: eventuali errori di avvio o dell'interfaccia mostrano un messaggio leggibile e vengono registrati in `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\error.log`

## Novità v1.5.0

- **Wizard di installazione e disinstallazione migliorati**: durante l'installazione puoi scegliere se installare subito l'AI locale (Ollama + modello); la disinstallazione può rimuovere anche il modello AI scaricato
- **Motore di analisi più intelligente**: riconoscimento fuzzy delle intestazioni (maiuscole, punteggiatura, sinonimi) e supporto a durate come `8h30`, `8:30` e celle Excel con formato orario
- **Conversione più veloce**: importazione e generazione avvengono in background senza bloccare l'interfaccia; le modifiche in griglia non ricalcolano l'XML a ogni tasto
- **Movimenti duplicati uniti**: richieste ripetute per lo stesso dipendente, giorno e giustificativo vengono sommate anziché duplicate nell'XML
- **Salvataggio streaming**: l'XML viene scritto direttamente su disco senza duplicarlo in memoria; si salva nella cartella scelta dall'utente, che viene ricordata
- **Tray icon e notifiche**: puoi chiudere una sola volta la scelta tra uscire o restare in background, con notifica al termine delle conversioni
- **AI più precisa**: modello predefinito `qwen2.5:1.5b` con prompt strutturati, temperature 0 e rilevamento automatico dei modelli compatibili già installati

## Novità v1.4.0

- **Interfaccia completamente ridisegnata**: Palette colori moderna e sofisticata
- **Tipografia migliorata**: Migliore gerarchia visiva e leggibilità
- **Componenti UI potenziati**: Tab, pulsanti, metriche e griglia con design contemporaneo
- **Syntax highlighting XML**: Colori ottimizzati per la leggibilità su dark mode
- **Spacing e layout**: Spaziatura più elegante e professionale
