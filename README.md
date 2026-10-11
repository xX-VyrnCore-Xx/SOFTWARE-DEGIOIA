# PresenzeFlow

PresenzeFlow è un'applicazione Windows 11 x64 per convertire un foglio Excel di richieste di assenza nel tracciato XML TRRIPW per Paghe Infinity. Con interfaccia moderna, analisi dati avanzata e validazione completa.

## Installazione

L'unica modalità di installazione supportata è l'installer Windows basato su Inno Setup: compila `Installer.iss` con Inno Setup 6 per generare `dist/PresenzeFlow-Setup-1.7.0.exe`. L'installer copia l'applicazione e la libreria nativa SQLite (`e_sqlite3.dll`) in `%LOCALAPPDATA%\Programs\PresenzeFlow` e crea i collegamenti nel menu Start e sul desktop. Non richiede privilegi amministrativi.

Durante l'installazione il wizard chiede se installare anche l'AI locale (Ollama + modello `qwen2.5:1.5b`, circa 2 GB, opzionale e deselezionabile): la conversione funziona comunque senza.

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

Il modello può essere installato dal wizard di installazione oppure da **Configura AI locale**, che scarica l'installer ufficiale di Ollama e poi il modello. Se è già presente un altro modello compatibile (es. `qwen2.5:0.5b` o `3b`) viene usato automaticamente. Il runtime supera 1 GB e il modello richiede altro spazio; l'AI è opzionale.

### Assistente AI: cosa può fare sul documento

L'AI **non scrive mai direttamente l'XML**. Descrivi a parole la correzione e l'applicazione la traduce in un insieme di azioni validate da codice:

- impostare ore e minuti sui movimenti che corrispondono a un filtro (dipendente, giustificativo, giorno o intervallo);
- spostare le date di N giorni (avanti o indietro);
- riscalare le durate (es. dimezzare);
- cambiare il **codice giustificativo**, ma solo riusando codici già configurati;
- rimuovere movimenti.

Un filtro senza campi vale "tutti i movimenti". Esempi di richieste: *"metti le ferie di Rossi a 4 ore il 17/08"*, *"sposta di un giorno tutti i movimenti del 20 agosto"*, *"dimezza le ore di Mario Bianchi"*, *"rimuovi i permessi non retribuiti di luglio"*.

Ogni modifica viene mostrata nel riquadro della conversazione con l'esito (quanti movimenti aggiornati, rimossi o ignorati) e l'anteprima/XML si rigenerano subito. Se l'AI propone un codice non configurato, la modifica viene scartata e viene segnalato. La conversazione è salvata nel database locale e viene ripristinata al riavvio (puoi azzerarla con **Nuova chat**).

**Comunicazioni e privacy**: Il programma comunica solo con `127.0.0.1:11434` (modello locale). Per le attività 1 e 2 invia esclusivamente intestazioni, etichette `Policy` e mapping già configurati, mai nominativi o righe del foglio. Per l'assistente conversazionale invia al modello locale il **riepilogo dell'anteprima corrente** (dipendenti, codici, date e giustificativi): serve a capire cosa correggere e **non esce mai dal tuo PC**. Nessun dato viene inviato a servizi esterni. L'AI non può introdurre codici ufficiali nuovi: i suggerimenti vanno verificati e confermati.

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
# iscc .\Installer.iss  ->  dist\PresenzeFlow-Setup-1.6.0.exe
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

## Cronologia versioni

**v1.4.0** (corrente)
- UI/UX completamente ridisegnato con palette moderna
- Miglioramenti ai componenti visivi e alle icone

**v1.3.0**
- Modello di analisi dati avanzato
- Validazione e sicurezza del database migliorata
- Controlli di integrità file ZIP e XML

**v1.2.0**
- Redesign completo della grafica
- Layout responsive
- Tema colori coordinato

**v1.1.0**
- Setup wizard personalizzato
- Icona multi-risoluzione
- Disinstallazione intelligente

**v1.0**
- Versione iniziale con funzionalità core
