# Vyrn Presence

> Vyrn Presence (già «PresenzeFlow») — i nomi dei file e dei dati sul PC restano quelli di prima, così gli aggiornamenti continuano a funzionare.

Vyrn Presence è un'applicazione Windows 11 x64 per convertire un foglio Excel di richieste di assenza nel tracciato XML TRRIPW per Paghe Infinity. Con interfaccia moderna, analisi dati avanzata e validazione completa.

## Installazione

L'unica modalità di installazione supportata è l'installer Windows basato su Inno Setup: compila `Installer.iss` con Inno Setup 6.3 o successivo per generare `dist/PresenzeFlow-Setup-1.31.0.exe`. L'installer copia l'applicazione e la libreria nativa SQLite (`e_sqlite3.dll`) in `%LOCALAPPDATA%\Programs\PresenzeFlow` e crea i collegamenti nel menu Start e sul desktop. Non richiede privilegi amministrativi.

L'installer non scarica nulla oltre al programma. L'eseguibile e l'installer sono **firmati** (vedi "Firma digitale"): il workflow non pubblica una Release senza firma. Un file firmato viene controllato più in fretta al primo avvio.

Se la distribuzione è gestita a distanza puoi consegnare **anche il token delle Release**: l'installer lo prende dal parametro `/GITHUBTOKEN=...` oppure da un file `github.token` lasciato accanto al setup e lo salva nel profilo dell'utente (anche in installazione silenziosa, senza mai sovrascrivere un token già presente). In questo modo il PC è collegato agli aggiornamenti fin dal primo avvio.

**Primo avvio**: al primo lancio subito dopo l'installazione Windows Defender analizza l'eseguibile (circa 160 MB non firmato digitalmente) e la finestra può impare anche 15-20 secondi ad apparire; dai lanci successivi l'avvio è immediato (misurato: ~0,5 s). La firma digitale dell'eseguibile riduce ulteriormente questa attesa.

Durante la disinstallazione puoi scegliere se rimuovere il programma mantenendo le impostazioni, se cancellare anche il database locale di codici azienda, dipendenti e giustificativi. In modalità silenziosa l'installazione non cancella mai i dati utente.

## Utilizzo

La finestra è divisa in quattro sezioni, con la barra di navigazione a sinistra: **Dashboard** (riepilogo e ultime esportazioni), **Nuova conversione** (il lavoro di conversione), **Gestione codici** (abbinamenti dipendenti e giustificativi) e **Storico esportazioni**. Nella Dashboard il campo di ricerca dell'intestazione filtra i movimenti da qualunque sezione.

1. **Azienda Zucchetti**: Inserisci il codice azienda ufficiale PAGHE (fino a 6 caratteri).
2. **File sorgente**: Importa un file `.xlsx` con un foglio di richieste — con il pulsante *Apri*, con `Ctrl+O` oppure **trascinandolo sulla finestra**. Il controllo del file limita:
   - Dimensione archivio ZIP: 50 MB
   - Dimensione espansa: 150 MB
   - Righe foglio: 100.000
   - Colonne: 256
   - Celle utilizzate: 2.000.000
   
   L'app riconosce intestazioni italiane e inglesi comuni e numeri decimali con virgola o punto; se il layout non è riconosciuto, mostra la schermata per associare le colonne.

3. **Codici dipendente**: Se il foglio contiene una colonna `Codice PAGHE` o `CodDipendenteUfficiale`, il codice ufficiale PAGHE (fino a 7 caratteri) viene letto e memorizzato localmente. Per i nominativi senza codice, inseriscilo una volta nel programma: verrà riutilizzato nelle conversioni successive.

4. **Giustificativi**: Associa i valori presenti nella colonna `Policy` ai codici giustificativo ufficiali PAGHE (fino a 3 caratteri). Questi codici non vengono mai dedotti: li decidi tu.

5. **Anteprima**: Genera e revisiona l'anteprima:
   - Ogni durata viene ripartita in modo uniforme sui giorni lunedì-venerdì dell'intervallo (o anche il sabato, se attivato nelle impostazioni)
   - Esclude le festività nazionali italiane e le chiusure aziendali aggiunte nelle impostazioni
   - La giornata standard è configurabile (8 h per impostazione predefinita) e le richieste che la superano vengono **segnalate**, mai modificate o scartate
   - Gli eventuali minuti residui sono assegnati in ordine cronologico
   - Utilizza `Duration in range` quando presente
   - Limita i movimenti al periodo `From Date`/`To Date` del foglio quando disponibile
   - Le richieste dello stesso dipendente che generano movimenti sullo stesso giorno sono segnalate per la revisione (non vengono scartate o modificate automaticamente)

6. **Revisione ed export**: nella scheda **Movimenti** puoi correggere codice dipendente, giustificativo, data, ore e minuti: l'XML viene aggiornato automaticamente (con una breve pausa dopo le modifiche, per restare fluido). La scheda **Controlli** elenca ciò che va verificato prima di esportare, la scheda **Anteprima XML** mostra il documento finale. La ricerca filtra i movimenti con più parole in qualsiasi ordine e **ignora accenti e maiuscole**: `mario ferie 12/05` trova sia "Rossi Mario" sia la data, scritta come `12/05/2026` o `2026-05-12`. Il file si salva dove vuoi e il programma ricorda l'ultima cartella usata. Le correzioni ai codici fatte in griglia vengono salvate nel database locale.

7. **Impostazioni** (pulsante nell'intestazione o `Ctrl+,`): ore della giornata standard, se considerare lavorativo il sabato, chiusure aziendali aggiuntive, rilevamento automatico di sabati e ore, token delle Release. Il foglio di un file con più fogli si cambia dal pulsante **Foglio: …** nella colonna di sinistra, senza reimportare il file; i **Codici PAGHE** e i giustificativi delle assenze si modificano dal pulsante omonimo, sempre nella colonna di sinistra.

8. **Chiusura**: alla prima chiusura puoi scegliere una sola volta (o ricordare la scelta) se uscire definitivamente o restare in tray: in background ricevi una notifica quando una conversione termina.

Il file generato contiene solo i movimenti di assenza presenti nell'Excel; non inventa ore ordinarie o presenze. Prima dell'esportazione vengono verificati codici, limiti del tracciato, associazioni dipendente/codice e caratteri XML; l'output è ordinato in modo deterministico, scritto in UTF-8 senza BOM e sostituito atomicamente per evitare file parziali.

## Aziende

La sezione **Aziende** della barra laterale è l'anagrafica delle aziende per cui prepari i file: **codice ufficiale PAGHE** (fino a 6 caratteri), **ragione sociale** e note. Si crea, si modifica e si elimina da lì; **Usa in conversione** (o doppio clic) imposta l'azienda del file XML.

- Tutto è salvato nel database locale (schema 4) e quindi incluso nei backup automatici e nel ripristino.
- Ogni codice digitato nel campo Azienda della conversione entra da solo nell'elenco, senza toccare ragione sociale e note di un'azienda già presente; il codice usato nelle versioni precedenti viene importato al primo avvio.
- Nel campo Azienda compaiono i suggerimenti scrivendo le prime lettere; sotto al campo e nel piede della barra laterale si vede la ragione sociale dell'azienda in uso.

### Regole per azienda

Il pulsante **Regole…** della sezione Aziende imposta, per l'azienda selezionata, giornata standard, sabato lavorativo, rilevamento automatico e chiusure aziendali. Quando scegli quell'azienda in conversione le regole si applicano da sole; senza regole proprie valgono quelle generali delle Impostazioni.

## Sabato lavorativo e ore effettive

Il motore di conversione rileva da solo, in base alle date, come distribuire le ore (impostazione **Rileva in automatico sabati e ore dalle date**, attiva di default; si può spegnere dalle impostazioni):

- **Sabato lavorativo solo quando serve**: se le ore richieste non entrano nei giorni feriali del periodo ma entrano includendo i sabati (es. 48 h dal lunedì al sabato), il sabato viene usato; se bastano i feriali (es. 40 h nella stessa settimana) il sabato resta libero. Una richiesta del solo sabato si mantiene. «Il sabato è sempre lavorativo» forza l'uso dei sabati.
- **Ore effettive dalle date**: se la durata coincide con il numero dei giorni del periodo (es. «5» dal lunedì al venerdì) viene letta come **giorni** e convertita in ore (5 × giornata standard). Ogni correzione compare fra le segnalazioni della scheda **Controlli**.
- Festivi nazionali, domeniche e chiusure aziendali restano sempre esclusi.

### Controlli prima dell'esportazione

La scheda **Controlli** raccoglie in un unico elenco tutto ciò che va verificato, ordinato per gravità (**ERRORE**, **ATTENZIONE**, **NOTA**):

- righe del foglio che non sono state importate, con il numero di riga e il motivo (data illeggibile, durata non riconosciuta, codice troppo lungo…);
- codici giustificativo non configurati, codici PAGHE mancanti o assegnati a due dipendenti diversi;
- più richieste che si sovrappongono nello stesso giorno e giornate oltre la giornata standard configurata;
- date fuori dal periodo dichiarato nel foglio, sabati, domeniche e giorni festivi;
- dipendenti presenti nel foglio che non hanno prodotto alcun movimento;
- differenze fra le ore dichiarate nel foglio e quelle effettivamente generate.

I controlli automatici girano **sempre**, in locale e senza rete, e si aggiornano a ogni modifica della griglia. **Copia elenco** esporta l'elenco per condividerlo con chi gestisce le paghe, e un doppio clic su una segnalazione porta al movimento corrispondente. Se restano errori, l'app li riassume in una finestra e apre direttamente la scheda Controlli.

**Comunicazioni e privacy**: il programma comunica con `127.0.0.1:11434` (modello locale) e, separatamente, con `api.github.com` solo per il controllo degli aggiornamenti (vedi la sezione "Aggiornamenti automatici"). Per le attività 1 e 2 invia esclusivamente intestazioni, etichette `Policy` e mapping già configurati, mai nominativi o righe del foglio. Per la revisione invia al modello locale il **riepilogo dell'anteprima corrente** (dipendenti, codici, date, giustificativi e ore): **non esce mai dal tuo PC**. Nessun dato viene inviato a servizi esterni.

## Dati e tracciato

**Database**: `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\settings.db` (schema 2: preferenze, abbinamenti e storico delle esportazioni nella tabella `Exports`, limitato alle ultime 200 consegne)

SQLite è configurato con:
- WAL journal mode per migliori prestazioni e concorrenza
- Attese controllate per accessi concorrenti
- Sincronizzazione completa delle scritture (`PRAGMA synchronous FULL`)
- `secure_delete` su tutte le pagine
- Controllo d'integrità rapido (`PRAGMA quick_check`) al primo avvio e poi al massimo una volta a settimana
- Tutti i valori passati tramite query parametrizzate

⚠️ **Nota importante**: Il database non è cifrato. Proteggi l'account Windows e il dispositivo secondo le policy aziendali. La disinstallazione consente di mantenere o rimuovere il database.

**Formato XML**: Il documento Zucchetti fornito (pagine 14-16) indica i campi del nodo `Dipendente` e dei movimenti: 
- Codice azienda ufficiale
- Codice dipendente ufficiale
- Codice giustificativo ufficiale
- Data in formato `aaaa-mm-gg`
- Ore e minuti

Il giustificativo `999` è riservato dal tracciato: **PAGHE scarta** il movimento comunicato con quel codice (l'app lo segnala nella scheda **Controlli**).

Prima del primo utilizzo produttivo, verifica un XML di prova nell'ambiente Zucchetti: i codici, le abilitazioni e le configurazioni di importazione dipendono dall'installazione PAGHE aziendale.

## Scorciatoie da tastiera

| Tasto | Azione |
| --- | --- |
| `Ctrl+O` | Apri un file Excel |
| `Ctrl+G` | Genera anteprima e XML |
| `Ctrl+S` | Salva l'XML su file |
| `Ctrl+F` | Cerca nei movimenti |
| `Ctrl+0` | Sezione Dashboard |
| `Ctrl+1` / `Ctrl+2` / `Ctrl+3` | Scheda Movimenti / Anteprima XML / Controlli |
| `Ctrl+4` / `Ctrl+5` / `Ctrl+6` | Sezione Aziende / Gestione codici / Storico esportazioni |
| `Ctrl+,` | Impostazioni |
| `F1` | Elenco delle scorciatoie |

## Build e test

Richiede .NET 8 SDK e Windows x64:

```powershell
# Esegui i test di validazione
dotnet test .\PresenzeXmlConverter.Tests\PresenzeXmlConverter.Tests.csproj

# Compila e pubblica l'app (input dell'installer)
dotnet publish .\PresenzeXmlConverter\PresenzeXmlConverter.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\dist\app

# Compila l'installer Windows (richiede Inno Setup 6.3 o successivo, lanciare dalla root del progetto)
# iscc .\Installer.iss  ->  dist\PresenzeFlow-Setup-1.31.0.exe
```

La compilazione elimina automaticamente i vecchi `dist\PresenzeFlow-Setup-*.exe`: nella cartella `dist` resta quindi **sempre e solo l'ultima build** (mentre `dist\app` e le cartelle `bin`/`obj` sono ignorate da git, vedi `.gitignore`).

⚠️ **Nota per chi cura la build**: il `PublishSingleFile` non include le librerie native, quindi `dist/app/e_sqlite3.dll` **deve** essere copiato dall'installer accanto a `PresenzeFlow.exe`. Senza quel file l'app non può aprire il database locale e termina all'avvio (ora con un messaggio d'errore esplicito invece di un silenzioso crash).

Per rigenerare l'icona e le immagini della procedura guidata (tre scale per lo schermo ad alta risoluzione), esegui `python3 assets/Create-BrandIcon.py` (richiede Pillow).

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
2. Rendilo disponibile in uno dei modi seguenti:
   - **dall'app** (consigliato): **Impostazioni → Aggiornamenti**, incolla il token e premi **Verifica e salva token**. L'app interroga subito GitHub, dice esattamente cosa risponde (`200` collegato, `401` token non valido, `403` permessi mancanti, `404` repository non visibile al token), salva il token e **lo attiva immediatamente**: nessun riavvio e nessun file da cercare a mano;
   - **variabile d'ambiente** `PRESENZEFLOW_GITHUB_TOKEN`;
   - oppure file **`%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\github.token`** contenente solo il token;
   - oppure, per una distribuzione aziendale, al momento dell'installazione con `/GITHUBTOKEN=...` o con un file `github.token` accanto al setup.

Il token **non è mai incluso nell'eseguibile** e non viene mai scritto nel database: resta nella configurazione del PC. Senza token il controllo automatico è inattivo: la barra in basso lo dice (`Aggiornamenti · token GitHub da configurare`) e al primo avvio l'app apre una sola volta le impostazioni nel punto giusto.

Con **Rimuovi token** il token viene cancellato dal profilo dell'utente.

### Come funziona

- **All'avvio**, dopo qualche secondo, l'app controlla in silenzio l'ultima Release; **mentre l'app resta aperta il controllo si ripete ogni ora**, così un aggiornamento pubblicato nel pomeriggio non aspetta il giorno dopo.
- La barra inferiore mostra sempre lo stato del collegamento (`Aggiornamenti · aggiornato alla 1.11.0 · 09:41`) e il pulsante **Aggiornamenti** nell'intestazione avvia un controllo immediato.
- Se esiste una versione più recente, mostra una finestra con **le novità** e i pulsanti **Aggiorna ora** / **Più tardi** (un aggiornamento rimandato non viene riproposto a ogni avvio, ma resta disponibile dal tray).
- **Aggiorna ora**: l'installer viene scaricato con barra di avanzamento e **verificato con il checksum SHA-256** pubblicato nella Release. Poi l'app si chiude, l'installer aggiorna e **PresenzeFlow si riapre da solo**.
- **Dopo l'aggiornamento** viene mostrata automaticamente la finestra **Novità** con il riepilogo delle modifiche (le note sono salvate in locale, quindi funzionano anche senza rete).
- Dal menu dell'icona in tray: **Cerca aggiornamenti…** e **Novità di questa versione**.

Per disattivare il controllo automatico (mantenendo quello manuale) imposta la preferenza `auto-update-check` a `false` nel database locale.

Per usare un feed diverso (es. un mirror interno) imposta la preferenza `update-endpoint` con l'URL completo dell'API della Release.

**Rete**: l'unico traffico verso Internet è il controllo delle Release su `api.github.com`, autenticato con il token locale.

## Firma digitale

L'eseguibile e l'installer **sono firmati**: la firma identifica l'editore e riduce l'attesa del primo avvio (Windows Defender controlla con più fiducia un eseguibile firmato). Il workflow di Release firma l'eseguibile, l'installer e il disinstallatore, poi **verifica** ogni firma: se il certificato non è configurato la Release si ferma e non pubblica nulla di non firmato (vedi *Release non firmate* più sotto).

### Attivare la firma (una volta sola, su Windows)

```powershell
.\tools\New-CodeSigningCertificate.ps1
```

Lo script crea un **certificato di firma autofirmato** per l'azienda e prepara la cartella `codesign/` (esclusa da git, non committarla) con:

- `PresenzeFlow-codesign.pfx.base64.txt` → da incollare nel secret GitHub **CODE_SIGNING_PFX_BASE64**
- `PASSWORD.txt` → la password del `.pfx`, da copiare nel secret **CODE_SIGNING_PFX_PASSWORD**
- `PresenzeFlow-codesign.cer` → certificato pubblico da rendere attendibile sui PC

I due secret devono venire dallo **stesso** certificato e vanno **copiati dal file**, non trascritti: la password di un altro certificato (o con uno spazio in più) fa fallire la firma con *"The specified network password is not correct"*, che sembra una password sbagliata anche quando è solo un a capo di troppo. Il workflow ripulisce da sé spazi e a capo e, se il `.pfx` non si apre, scrive nel log le **impronte** dei due secret (lunghezza e SHA-256, mai i valori): così si capisce subito quale dei due non corrisponde.

Il workflow firma eseguibile e installer **solo se i due secret esistono**, e li verifica entrambi (che la firma ci sia, che l'hash corrisponda e che il certificato sia quello atteso). Senza certificato il workflow **si ferma** e dice quale dei due secret manca: nessuna Release esce non firmata per distrazione.

Oltre all'installer e al suo checksum, la Release allega anche il **certificato pubblico** `PresenzeFlow-codesign.cer`: è quello da installare sui PC (paragrafo successivo). Non contiene la chiave privata.

### Release non firmate

Se serve pubblicare comunque una Release non firmata (per esempio per una prova), si imposta la variabile di repository **`ALLOW_UNSIGNED_RELEASE=true`** (Settings → Secrets and variables → Actions → Variables): il workflow avvisa, scrive nel riepilogo che la Release non è firmata e prosegue. Togliendola, la firma torna obbligatoria.

### Rendere attendibile il certificato sui PC

Su ogni PC con l'app installata (per-utente, **senza privilegi di amministratore**):

```bat
certutil -addstore -user Root PresenzeFlow-codesign.cer
certutil -addstore -user TrustedPublisher PresenzeFlow-codesign.cer
```

In azienda la stessa operazione si distribuisce con Criteri di gruppo; per rendere il certificato valido per **tutto il PC** si usano gli stessi comandi da prompt elevato **senza** `-user`.

### Attendibilità automatica con l'installer (v1.18.0)

Nella procedura guidata compare la voce **«Considera attendibile il certificato di firma di PresenzeFlow»**: esegue per l'utente corrente gli stessi due comandi `certutil` descritti sopra e li annulla alla disinstallazione. Windows mostra un solo avviso di sicurezza da confermare. Negli aggiornamenti silenziosi non viene toccato nulla.

### Come ottenere una firma che nessun PC consideri sospetta

Un certificato autofirmato e il suo inserimento fra le autorità attendibili funzionano **solo sui PC dove è installato**. Per un file scaricato da Internet, SmartScreen valuta la **reputazione** dell'editore: la ottiene solo un certificato emesso da una CA pubblica (OV o EV) o dal servizio **Azure Trusted Signing**, con cui il workflow si può estendere al posto di `signtool` con il PFX. Finché non c'è, se Defender segnala un file firmato dal proprio certificato, si può inviarlo al portale di Microsoft per l'analisi dei falsi positivi (https://www.microsoft.com/wdsi/filesubmission).

### Nota importante

Un certificato **autofirmato non elimina** l'avviso SmartScreen per chi scarica l'installer da Internet: diventa attendibile solo sui PC dove il certificato pubblico è installato. Per una firma riconosciuta da tutti serve un certificato emesso da una CA pubblica (a pagamento) o un servizio di firma gestito. Senza firma l'app funziona comunque: Windows mostra solo l'avviso "editore sconosciuto" al primo avvio. La verifica nel workflow non pretende una catena attendibile sul runner (il certificato è autofirmato): controlla firma, hash e impronta del certificato.

## Aggiornamenti, token e dati (v1.16.0)

**Token incluso nella build.** Crea un token fine-grained con repository *solo questo* e permesso **Contents → Read-only**, scadenza la più lunga possibile, e salvalo come secret `PRESENZEFLOW_GITHUB_TOKEN` (Settings → Secrets and variables → Actions). Il workflow di rilascio lo verifica e lo inietta, offuscato, in ogni installer: le app installate si collegano da sole alle Release. Se il secret manca il rilascio si ferma; per pubblicare comunque imposta la variabile `ALLOW_RELEASE_WITHOUT_UPDATE_TOKEN=true`. Il token è offuscato, non cifrato: non dargli permessi diversi da quello di lettura.

**Dati locali.** Database, backup automatici (ultimi 10), copie di file danneggiati e token personale stanno in `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter`. All'avvio il database si ripara da solo (ripristino dall'ultimo backup valido). Dal menu dell'icona in tray: *Stato del database…*, *Backup dei dati ora*, *Apri cartella dei dati*.

**Disinstallazione.** Il disinstallatore chiede se conservare o eliminare i dati (eliminare richiede una seconda conferma). In silenzioso i dati si conservano: usa `/DELETEDATA` per eliminarli.

## Novità v1.15.0

- **Barra di navigazione laterale** con cinque sezioni: **Dashboard**, **Nuova conversione**, **Aziende**, **Gestione codici** e **Storico esportazioni**, con ricerca nell'intestazione che filtra i movimenti da qualunque sezione (scorciatoie `Ctrl+0…5`)
- **Dashboard**: card di riepilogo (richieste, dipendenti, movimenti, ore), stato del file sorgente, azioni rapide e ultime esportazioni con doppio clic per aprire la cartella del file
- **Gestione codici** come sezione della finestra, con lo **stesso** pannello del dialogo "Codici PAGHE" (una sola logica di validazione)
- **Storico esportazioni** (nuovo): ogni XML salvato è registrato con data, file, cartella, dipendenti e ore; le voci si aprono in Esplora file o si copiano; lo storico tiene le ultime 200 consegne e i file su disco non vengono mai toccati
- **Apertura più rapida**: `PublishReadyToRun` nel publish, preferenze di avvio lette in una sola query, storico e stato del database letti **dopo** che la finestra è visibile; con `PRESENZEFLOW_TRACE_STARTUP=1` l'app scrive in `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\startup.log` la durata di ogni fase di avvio
- **15 nuovi test di logica** (dashboard, storico, persistenza e schema del database) e **test grafico su ogni sezione** della finestra

## Novità v1.14.0

- **Struttura Windows**: il manifesto dichiara le versioni di Windows supportate (senza, `Environment.OSVersion` risponde 6.2 e gli **angoli arrotondati di Windows 11** non venivano mai richiesti) e abilita i **percorsi lunghi**; l'app comunica a Windows la propria **identità** (`AppUserModelID`) prima di creare la finestra, così barra delle applicazioni, notifiche e collegamento bloccato sono la stessa applicazione; l'app tiene aperto un **mutex condiviso con l'installer** (`[Setup] AppMutex`), quindi un aggiornamento chiude l'app in modo ordinato invece di fallire sui file in uso
- **Tracciato Zucchetti**: le lunghezze documentate dei campi stanno in un unico posto e vengono applicate al valore (codice azienda 6, dipendente 7, giustificativo 3, ore 0-99, minuti 0-59); gli errori di validazione **nominano il dipendente e la richiesta** da correggere; il **giustificativo 999** (che PAGHE scarta) è segnalato nella scheda **Controlli** come NOTA
- **UI/UX e accessibilità**: nomi e descrizioni accessibili su pulsanti, campi, griglia, anteprima e schede (un lettore di schermo annuncia "Seleziona file Excel", non "pulsante"), con gli stessi testi usati come suggerimenti
- **5 nuovi test di logica** su limiti del tracciato, messaggi di validazione, giustificativo 999, mutex app/installer e manifesto Windows

## Novità v1.13.0

- **Impostazioni a schede**: *Conversione*, *AI locale* e *Aggiornamenti* sono tre schede che entrano intere nella finestra, quindi il dialogo non ha più la barra di scorrimento grigia di sistema; l'altezza di ogni scheda è la somma delle sue righe (`SettingsPageLayout`), così nessun campo viene schiacciato e nessun testo tagliato (prima la scheda *AI locale* perdeva l'ultima riga)
- **Novità di una Release più leggibili**: il testo scorre con una barra disegnata con il tema dell'app e un pulsante **Copia note** lo copia negli appunti
- **Versione in esecuzione** anche nell'intestazione delle Impostazioni e **firma dell'eseguibile** spostata nella scheda *Aggiornamenti*
- **Stato vuoto nelle griglie**: quando non c'è nulla da mostrare l'area di revisione lo dice, invece di restare bianca
- **Correzioni**: i pulsanti *Revisione AI* e *Copia elenco* erano tagliati di 3 px sul bordo inferiore; il campo delle chiusure aziendali non mostra più la barra grigia di sistema
- **184 test di logica** (17 nuovi) e test grafico che ora disegna **tutte le schede** della finestra e dei dialoghi (Impostazioni e Novità compresi)

## Novità v1.12.0

- **Barra del titolo in tema**: il titolo della finestra è scuro come l'intestazione dell'app (prima era chiaro e la finestra sembrava composta da due programmi) e su Windows 11 la finestra ha gli **angoli arrotondati**; la scelta si adatta da sola alla versione di Windows. Nel titolo della finestra (e nel tooltip dell'icona vicino all'orologio) compare la **versione in esecuzione**, così si vede subito quale build è installata
- **Trascina il foglio Excel sulla finestra**: si importa senza aprire il dialogo; vengono accettati solo fogli `.xlsx` esistenti e durante una conversione il trascinamento non è accettato
- **Firma digitale visibile nell'app**: in *Impostazioni → Aggiornamenti* è indicato se l'eseguibile in esecuzione è firmato, da chi e fino a quando

## Novità v1.11.0

- **Analisi Excel più solida**: intestazioni fuzzy più numerose, date e durate riconosciute in più formati (comprese le celle con formato orario e le durate espresse in giorni), nominativi normalizzati (spazi doppi e caratteri invisibili), righe problematiche **segnalate con il numero di riga e il motivo** invece di bloccare l'importazione, e possibilità di **scegliere un altro foglio** del file senza reimportarlo
- **Chat AI rimossa**: non serviva. Al suo posto la scheda **Controlli**, che elenca per gravità errori, attenzioni e note di ogni conversione (righe non importate, codici non configurati, sovrapposizioni, giorni oltre la giornata standard, festivi, differenze di ore), con doppio clic per raggiungere il movimento e **Copia elenco** per condividerlo
- **AI usata meglio nella generazione dell'XML**: **structured outputs** (JSON Schema passato a Ollama) per l'abbinamento delle colonne e dei codici, prompt dedicati con temperature 0 e seed fisso, e una nuova **revisione dei movimenti** che aggiunge solo osservazioni verificate (i codici inventati vengono scartati)
- **Modello AI**: predefinito e consigliato `qwen2.5:3b`, il più affidabile per l'estrazione strutturata fra i modelli piccoli; la lista dei modelli accettati è allineata fra app e installer
- **Aggiornamenti sempre collegati**: token GitHub inserito e **verificato direttamente nell'app** (Impostazioni → Aggiornamenti, con l'esito reale di GitHub: 200/401/403/404), attivo **subito** senza riavvio, controllo ripetuto ogni ora, stato sempre visibile nella barra inferiore, e possibilità di fornire il token all'installazione (`/GITHUBTOKEN=...` o file `github.token`)
- **Motore di calcolo e ricerca**: ore della giornata standard configurabili, sabato lavorativo opzionale, chiusure aziendali aggiuntive, avvisi sulle richieste che superano la giornata; ricerca nei movimenti **multi-termine, senza accenti e senza maiuscole**, che riconosce codici, date (italiane e ISO) e durate (`7:30`, `7.30`, `450` minuti)
- **UI/UX**: pulsanti **Impostazioni** e **Aggiornamenti** nell'intestazione, nuovo dialogo **Impostazioni** unico per conversione, aggiornamenti e modello AI, pulsante **Foglio: …** per cambiare foglio, riepilogo dell'anteprima con totali, **scorciatoie da tastiera** (`Ctrl+O/G/S/F`, `Ctrl+1/2/3`, `Ctrl+,`, `F1`) e stato degli aggiornamenti nel piè di pagina
- **140 test di logica** (49 nuovi) su ricerca, impostazioni, controlli, generazione con le nuove opzioni e installer

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
- **Firma digitale obbligatoria** (autofirmata aziendale) di eseguibile, installer e disinstallatore, con verifica della firma a ogni Release; senza certificato la Release si ferma (esenzione esplicita con `ALLOW_UNSIGNED_RELEASE`)
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
