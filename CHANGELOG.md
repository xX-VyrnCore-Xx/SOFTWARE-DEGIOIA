# Changelog - Vyrn Presence (già PresenzeFlow)

Tutti i cambiamenti significativi di Vyrn Presence sono documentati in questo file.

> Nota: le voci "Cronologia versioni"/"Novità" nel README e le sezioni storiche di questo file descrivono le release precedenti; la distribuzione attuale è solo l'installer Windows Inno Setup.

## [1.75.0] - Corrente

### ✨ Avvio a schermo intero
- Il programma si apre con la finestra massimizzata (a tutto schermo, barra delle applicazioni compresa); l'avvio automatico con Windows continua a restare nel tray

## [1.74.0]

### ✨ Una sola interfaccia e il calendario in anteprima
- **Vyrn Presence usa solo la nuova interfaccia**: non esiste più il ritorno alla classica (tolti il pulsante nella barra laterale, la voce nel menu del tray e l'avvio `--classic`); se la finestra non riesce ad aprirsi il programma lo dice con il percorso del registro degli errori
- **Calendario nell'anteprima** (scheda «Calendario»): una riga per dipendente e una colonna per giorno, ogni giorno colorato per giustificativo (presenza in verde, mezza giornata divisa a metà, ogni assenza con il suo colore); i giorni lavorativi senza movimenti sono tratteggiati in rosso, weekend e festivi in tono spento
- Ogni riga riporta il totale del mese e, in rosso, i giorni scoperti o la differenza dalle ore teoriche; il piè conta i presenti di ogni giorno
- Si sfoglia mese per mese, si cerca per nome, si guardano solo i dipendenti da controllare; **passando sopra un giorno** compaiono movimenti e anomalie, con un clic il giorno si evidenzia
- Barra delle anomalie in alto (giorni oltre la giornata, giorni insoliti, totali diversi dal teorico) con «Vai al primo / al prossimo» che porta al giorno giusto
- La legenda dei colori è sotto il calendario; la larghezza dei giorni si adatta alla finestra

### 🐛 Correzioni grafiche e aggiornamenti in tempo reale
- **Riga selezionata in magenta** (per esempio il codice nelle tabelle dei Codici): mancava un colore del tema, ora c'è; un controllo automatico verifica che ogni colore usato nel codice esista
- **Il tema cambia subito**: scegliendo Chiaro, Scuro, Come Windows o Vetro nelle Impostazioni la finestra si ridisegna senza riavviare, restando nella stessa pagina e con il lavoro in corso (file importato, anteprima) intatto; con «Come Windows» segue anche il cambio del tema di sistema a programma aperto
- **Icona degli aggiornamenti in alto a destra**: mostra la versione installata e lo stato in tempo reale (aggiornato, controllo in corso con icona che gira, nuova versione, download con percentuale e barra); il pulsante cerca subito o, se c'è una versione pronta, la installa
- Il download dell'aggiornamento avanza anche nella barra delle applicazioni di Windows, e lo stato nelle Impostazioni → Aggiornamenti si aggiorna da solo

### ✨ Menù e icona nel tray più curati
- **Icona nel tray**: con un aggiornamento pronto compare un pallino verde sull'icona (oltre al suggerimento), alla misura giusta per lo schermo
- **Menù del tray** con testata (nome, versione e stato degli aggiornamenti), accesso rapido a **Nuova conversione** e **Storico esportazioni**, voci raggruppate e senza più il ritorno alla classica
- **Menù del tasto destro** nello stile dell'app (angoli tondi, ombra, icone, voce evidenziata) su movimenti e segnalazioni (copia riga, copia nome, mostra nel calendario), Storico (apri cartella, verifica, salva copia, copia percorso), Aziende (usa, regole, controlli, ferie e permessi, elimina) e calendario (copia i dettagli del giorno, vai alla prossima anomalia)

## [1.72.0]

### ✨ Ferie e permessi residui
- Nella sezione **Aziende** (nuova interfaccia) il pulsante **Ferie e permessi…** raccoglie quanto spetta in un anno di ogni giustificativo, una riga ciascuno: «Ferie = 26 giorni», «Permessi = 104 ore» (il giustificativo si indica col nome o col codice; i giorni contano con la giornata standard dell'azienda)
- **Storico → Residui**: per l'anno scelto, per ogni dipendente quanto è stato usato contro quanto spetta e quanto resta, con chi è oltre la spettanza in cima
- **Avviso già in anteprima**: se con le ore del file qualcuno supera le ferie o i permessi dell'anno (contando quanto già consegnato) compare una segnalazione nella scheda Controlli; chi è vicino all'esaurimento ha una nota
- Le ore usate per giustificativo si conservano a ogni esportazione (anche dalla classica) a partire da questa versione; esportare di nuovo un mese lo corregge
- Nuovo passaggio del database (versione 8)

## [1.71.0]

### ✨ Andamento delle ore mese su mese
- **Storico → Andamento** (nuova interfaccia): grafico a colonne delle ore consegnate mese per mese per ogni azienda (ultimi 12 mesi) e, sotto, il confronto per dipendente fra l'ultimo mese e quello prima: ore, differenza, percentuale e ore di assenza, con chi è cambiato di più in cima
- Le ore mensili si conservano a ogni esportazione (anche dalla versione classica); esportare di nuovo lo stesso mese lo corregge, gli altri mesi restano
- Con più aziende si sceglie quale guardare; un dipendente nuovo nell'ultimo mese è indicato come «nuovo»
- Nuovo passaggio del database (versione 7), applicato in automatico e in una transazione

## [1.70.0]

### ✨ Controlli su misura per ogni azienda
- Nella sezione **Aziende** (nuova interfaccia) il pulsante **Controlli…** fissa le soglie con cui il foglio di quell'azienda viene controllato: malattia di fila oltre N giorni, altre assenze di fila oltre N giorni, variazione rispetto all'ultima consegna, tetto di ore al mese per dipendente e **dipendenti attesi** (avviso se mancano nel foglio)
- Si può anche spegnere il controllo delle richieste sovrapposte
- Ogni azienda ha le sue soglie; senza soglie proprie valgono quelle di serie (le stesse di prima). Le segnalazioni compaiono nella scheda Controlli, anche nell'interfaccia classica
- Salvare le soglie finisce nel registro attività; cambiare ragione sociale, note o regole di conversione non le cancella

## [1.69.0]

### ✨ Importazione da file CSV
- **Si importano anche file CSV e TSV** (oltre a .xlsx e .xlsm): dal pulsante «Scegli il foglio» o trascinandoli sulla finestra, nella nuova interfaccia e nella classica
- Separatore (`;` di Excel italiano, `,`, tabulazione o `|`) e codifica (UTF-8 o Windows) si riconoscono dal contenuto; virgolette, a capo dentro le celle, date `gg/mm/aaaa` e numeri con la virgola si leggono come nel foglio Excel
- Stesso riconoscimento delle colonne e stessi controlli del foglio Excel (malattia in giorni compresa); limiti di 50 MB, 100.000 righe e 256 colonne

## [1.68.0]

### ✨ Riepilogo mensile e PDF nella nuova interfaccia
- Nuova scheda **Riepilogo** nella conversione: ore del mese di ogni dipendente (presenza, assenza, totale), con ore teoriche, differenza e giorni scoperti quando le presenze automatiche sono accese
- **Salva PDF** (report del mese con totali, assenze e giorni da controllare) e **Salva CSV** (per Excel): gli stessi file della versione classica, con gli stessi numeri
- Il nome del file propone il mese dei movimenti («Report ore 2026-03.pdf»)

## [1.67.0]

### ✨ Controlli più intelligenti sul foglio
- **Richieste sovrapposte**: due richieste dello stesso dipendente che si sovrappongono (ad esempio ferie e malattia negli stessi giorni) o la stessa richiesta scritta due volte si segnalano con le righe del foglio e i giorni in comune
- **Malattie molto lunghe**: una malattia (anche a pezzi attaccati) oltre 30 giorni di fila si segnala per un controllo
- **Confronto con l'ultima consegna**: se dipendenti od ore cambiano di oltre il 30% rispetto all'ultima consegna della stessa azienda, compare una nota (di solito un foglio incompleto o un mese sbagliato)
- Festivi, sabati e domeniche, ore oltre la giornata e codici mancanti erano già controllati e restano

## [1.66.0]

### ✨ La nuova interfaccia diventa quella di serie
- **Vyrn Presence si apre con la nuova interfaccia**; chi aveva già scelto la classica la mantiene
- **Ripiego automatico**: se la nuova non riesce ad aprirsi, parte la classica; se non compare la finestra per due avvii di fila, la classica diventa la scelta salvata (l'errore è nel registro)
- **Tornare alla classica** è sempre possibile: pulsante in fondo alla barra laterale, voce nel menu del tray della classica per riprovare la nuova, oppure l'avvio con `--classic` (e `--wpf` per la nuova)

## [1.65.1]

### 🐛 Correzioni
- **Malattia e permessi retribuiti (Milestone) in giorni**: il valore nel file Excel è letto in giorni (1 = una giornata standard, ad esempio 8 ore), anche se la colonna è «ore»; se la cella indica l'unità (ad esempio «4 h») vale quella. Le altre causali restano invariate
- **Impostazioni classiche**: l'interruttore riempie sempre uno sfondo opaco, così non restano righe scure ai bordi a certe scale dello schermo

## [1.65.0]

### ✨ Nuova interfaccia (anteprima): aggiornamenti
- **Controllo automatico** all'avvio (ritardato), ogni dieci minuti anche con la finestra nel tray, al ritorno della rete e alla fine della sospensione, e quando si torna alla finestra dopo un po'; «Cerca aggiornamenti ora» e lo stato nelle Impostazioni → Aggiornamenti
- **Una nuova versione si annuncia con un avviso** (cartellino, fumetto di Windows, lampeggio), mai con una finestra a sorpresa; un riquadro in fondo alla barra laterale e la voce in cima al menu del tray la installano con un clic; una versione rimandata non si ripropone a ogni avvio
- **Novità e avanzamento** in finestre della nuova interfaccia: note della Release impaginate, i quattro passi (scaricamento, controllo del file, verifica della firma, installazione) con barra e «Annulla» fino all'installazione
- **Dopo l'aggiornamento** si mostrano le novità salvate prima del riavvio (senza rete)
- Il download è lo stesso percorso sicuro della versione classica, ora in `UpdateInstaller` senza finestre e con i suoi test: cartella privata e nome casuale, checksum SHA-256 obbligatorio se la Release lo dichiara, firma Authenticode, certificato diverso solo con conferma

## [1.64.0]

### ✨ Nuova interfaccia (anteprima): notifiche e tray
- **Avvisi dentro la finestra** (conversione completata, file salvato) come cartellini in basso a destra: si ritirano da soli, si fermano col mouse, un clic apre il file
- **Icona nel tray** con il menu della versione classica (Apri, Cerca aggiornamenti, Impostazioni, Notifiche di Windows, Avvia con Windows, «Alla chiusura chiedi di nuovo», Torna all'interfaccia classica, Esci) e **fumetti di Windows** quando la finestra non è in uso; un clic sul fumetto riapre la finestra
- **La X della finestra** chiede se restare in background o uscire (si può ricordare la scelta, come nella classica); un secondo avvio del programma riporta la finestra davanti; l'avvio con Windows (`--tray`) parte nel tray senza finestra
- **«Prova le notifiche»** nelle Impostazioni; all'uscita copie di sicurezza e manutenzione dei dati come nella finestra classica
- Le regole (quando basta il cartellino, quando servono lampeggio e fumetto, cosa fa la X) stanno in `NotificationPolicy`, con i loro test

## [1.63.0]

### ✨ Nuova interfaccia (anteprima): finestre di dialogo in WPF
- **Associa le colonne**, **Regole di conversione** dell'azienda e **Completa i codici mancanti** sono ora finestre della nuova interfaccia (stesso stile, elenchi a discesa e interruttori nuovi), non più le finestre classiche ospitate
- I controlli sono gli stessi di prima (cinque colonne diverse; ore, date e chiusure valide) e stanno in `DialogLogic`, con i loro test
- Chiudere i codici con «Annulla» non butta via quelli già scritti: si salvano da soli anche a metà

## [1.62.0]

### ✨ Nuova interfaccia (anteprima): Impostazioni
- Sezione **Impostazioni** con cinque schede: **Conversione** (ore della giornata, sabato, rilevamento automatico, presenza 01, chiusure aziendali), **Aggiornamenti** (ricerca automatica, avvio con Windows, token GitHub da verificare e salvare subito), **Formato XML** (radice, spazio dei nomi, un nodo per movimento, lettura da un XML di esempio), **Notifiche** e **Generale** (cosa fa la X, tema, cartella dei dati, backup ora, copie dei codici)
- Gli stessi controlli e lo stesso salvataggio della finestra classica (`SettingsService` senza finestre, con i suoi test): se cambi le regole o la forma dell'XML l'anteprima già generata va rifatta
- La barra laterale della nuova interfaccia ha ora tutte le sezioni; l'interfaccia classica non cambia e resta quella di partenza

## [1.61.0]

### ✨ Nuova interfaccia (anteprima): Storico esportazioni e Registro attività
- **Storico esportazioni**: ogni XML salvato con data, file, cartella, movimenti e ore; **Apri cartella** (anche con doppio clic), **Verifica file** (identico alla copia consegnata? rispetta ancora il tracciato?), **Salva copia…** dalla copia conservata nel database, **Copia percorso** e **Svuota storico** (i file su disco restano)
- **Registro attività**: cosa è stato fatto, con ricerca per testo, filtro per tipo, **Esporta CSV…** delle voci mostrate e **Svuota registro**
- Con queste due sezioni la barra laterale della nuova interfaccia è completa; restano le Impostazioni. L'interfaccia classica non cambia
- Dietro le sezioni ci sono `HistoryService` e `ActivityService` senza finestre, con i loro test

## [1.60.0]

### ✨ Nuova interfaccia (anteprima): Aziende e Gestione codici
- **Aziende**: elenco con l'azienda in uso, modulo per codice (1-6 caratteri), ragione sociale e note; pulsanti per **Regole…** (le stesse della finestra classica), **Usa per la conversione** (la schermata di Conversione si aggiorna subito) ed **Elimina**
- **Gestione codici**: tabelle **Dipendenti** (codice PAGHE, ore al giorno, assunzione e fine rapporto) e **Giustificativi**, con celle modificabili; i nomi del foglio importato senza codice sono segnati con *. Si salva con «Salva abbinamenti» o da sé poco dopo l'ultima modifica, **anche a metà**: lunghezze del tracciato, ore, date e codici assegnati a più persone si controllano come nella versione classica
- Dietro le due sezioni ci sono servizi senza finestre con i loro test; l'interfaccia classica non cambia e resta quella di partenza

## [1.59.0]

### 📄 «Rinomina PDF» nella barra laterale e Impostazioni più coerenti
- **Rinomina PDF è una sezione della barra laterale** (con la sua pagina nella finestra, non più un dialogo a parte): scegli o trascini lo ZIP, controlli la tabella e salvi. La scorciatoia in Dashboard e il comando Ctrl+K aprono la stessa sezione
- **Impostazioni**: tutti i campi di testo hanno lo stesso carattere e la stessa altezza (prima alcuni erano in grassetto grande e altri piccoli, e il testo guida sembrava testo vero) e sono spariti i doppi spazi nelle etichette

## [1.58.0]

### ✨ Nuova interfaccia (anteprima), opzionale e con ripiego automatico
- **Interfaccia nuova in WPF**: tavolozza chiara e arioso stile macOS, animazioni fluide (barra laterale con pillola che scorre, dissolvenza fra le pagine, riquadri che si sollevano al passaggio del mouse, interruttori animati), barre di scorrimento sottili. In questa versione sono pronte la **barra laterale** e la **Dashboard** (foglio da trascinare, tre passi, stato del sistema, scorciatoie); le altre sezioni arrivano nelle prossime versioni
- **Non cambia nulla se non la scegli**: l'interfaccia classica resta quella di partenza e funziona come prima. Si prova dal menù dell'icona nel tray («Prova la nuova interfaccia (anteprima)») o con il parametro `--wpf`; si torna indietro dal pulsante in fondo alla barra laterale o con `--classic`
- **Ripiego automatico**: se la nuova interfaccia non riesce ad aprirsi, l'errore va nel registro errori e il programma apre da solo quella classica
- L'installer porta con sé le librerie native di WPF; la CI avvia l'eseguibile pubblicato con `--wpf` per verificare che parta
- La conversione XML non è toccata

### 📄 Rinomina PDF per codice cliente
- Nuova funzione, dalla Dashboard («Rinomina PDF»), dalla ricerca comandi (Ctrl+K) o trascinando il file: si sceglie uno **ZIP** (o dei PDF) generati da Zucchetti, dove **ogni pagina è un cliente**. Il programma legge il **codice interno in fondo alla pagina** (cifre separate da trattini, es. 123-456-7)
- **Anteprima con controlli prima di scrivere**: tabella pagina per pagina con lo stato (giusto, da controllare, codice mancante). Segnala **codice mancante**, **più codici sulla stessa riga**, **codice di forma diversa dagli altri**, **codice ripetuto** (il secondo diventa «codice (2)»); i codici si correggono a mano nella tabella e i controlli si rifanno subito
- Alla conferma crea un **nuovo ZIP** con un PDF per cliente chiamato `codice.pdf` (l'originale non si tocca; le pagine senza codice restano fuori) e poi **lo riapre e verifica ogni file**: una pagina, stesso testo dell'originale, nessun file mancante

## [1.57.0]

### 🍎 Grafica rifatta in stile macOS, senza testi tagliati né sovrapposti
- **Perché i testi si sovrapponevano**: con lo schermo al 125 % o 150 % i caratteri crescevano ma molte altezze scritte in pixel (righe, card, voci della barra) restavano ferme, quindi titoli, righe di testo e pulsanti si tagliavano o si sovrapponevano. Ora **ogni riga di testo è alta quanto il suo testo** (nessuna altezza fissa nei titoli, nelle Impostazioni e nei dialoghi) e le misure disegnate a mano seguono la scala dello schermo. In più una **rete di sicurezza** scala la finestra se Windows non l'ha fatto
- **Barra laterale in stile macOS**: pannello arrotondato più scuro della tela, voce attiva a **pillola blu piena** con testo e icona bianchi che scorre da una voce all'altra
- **Tavolozza chiara macOS**: blu di sistema (#007AFF), grigi morbidi, bordi sottili; angoli delle card lisci (prima a scalini) e senza il doppio filo sul bordo inferiore
- **Interruttori** (verde acceso, grigio spento, con animazione) al posto delle caselle di spunta, e **controllo segmentato** con pillola che scorre al posto dei pulsanti radio (chiusura con la X, aspetto dell'app)
- **Impostazioni e dialoghi** (Impostazioni, «Come vuoi chiudere il programma?», Regole azienda): righe che seguono il testo, titolo non più tagliato, schede che non hanno bisogno di scorrere
- **Dashboard a foglio vuoto**: la scheda di conversione riempie la pagina e sotto compaiono **scorciatoie** verso Aziende, Codici, Storico e Registro (con animazione al passaggio del mouse), così si vede dove andare
- Intestazioni delle tabelle su una riga (niente più «Codice PAGHE» spezzato in due)
- La conversione XML non è toccata

## [1.56.0]

### 🏢 Aspetto professionale, chiaro, in stile Windows 11 / Office
- **Tema chiaro rifatto** con grigi neutri (fondo #F3F3F3, schede bianche), testo quasi nero, il blu di sistema di Windows 11 (#0067C0), verde, ambra e rosso sobri e **bordi netti** (più scuri, ben visibili) su pulsanti, caselle e griglie. Tutti i contrasti restano verificati dai test
- **È il tema predefinito**: chi aveva «Come Windows» o «Vetro» salvato (valori scritti dalle versioni precedenti) passa **una volta sola** a «Chiaro»; «Scuro», «Vetro» e «Come Windows» restano nelle Impostazioni → Generale e, se scelti di nuovo, vengono rispettati
- **Testi più grandi in tutto il programma** (circa +13 %): testo base 10,5 pt (prima 10), secondario 9,8 pt (8,7), piccolo 9,2 pt (8,4), etichette e pillole 9 pt (8,2), tabelle 10 pt (8,9) con intestazioni 9,5 pt, voci della barra laterale e schede 10,5 pt, pulsanti 10,5 pt
- **Il testo disegnato dal programma non viene più tagliato dal bordo del suo riquadro**: con caratteri più grandi o con lo scaling di Windows al 125–150 % una riga che sborda di un pixel resta leggibile (prima perdeva la metà inferiore delle lettere, come «DATI LOCALI»). Vale per tutti i controlli fatti a mano (indicatori, barra di stato, calendario, ricerca, menù, avvisi…)
- Le Impostazioni mostrano i temi nell'ordine Chiaro, Scuro, Come Windows, Vetro
- La conversione XML non è toccata

## [1.55.0]

### 🪟 Finestre «Apri» e «Salva» spostabili, Dashboard più ordinata
- **Le finestre per scegliere o salvare un file non si aprono più a tutto schermo**: Windows ricorda la dimensione di ogni finestra di questo tipo per applicazione, e una volta ingrandita si riapriva sempre così, senza poterla spostare. Ora hanno un identificativo proprio: riaprono con la dimensione normale, si spostano e si ridimensionano a piacere, e ricordano la nuova misura. Vale per tutte le dieci finestre dell'app (importa Excel, salva XML, esporta e importa codici, report e immagini del calendario, registro attività, certificato…). Un test controlla che nessuna sia rimasta indietro
- **Dashboard a foglio vuoto**: la scheda «Nuova conversione» non è più enorme; ha un'altezza ragionevole e l'area di rilascio ha **icona, titolo e invito centrati** invece di una riga isolata a sinistra
- **Stato**: le tre righe stanno insieme in alto con la stessa altezza, invece di sparpagliarsi su tutta la pagina
- **Card del tema «Vetro»**: sparito il riquadro interno più scuro che si vedeva dentro le card; l'interno è uniforme e il vetro si vede sul bordo luminoso e sul velo di luce in alto
- La conversione XML non è toccata

## [1.54.0]

### ✂️ Meno rumore, più sostanza
- **Dashboard essenziale**: a foglio vuoto resta solo la scheda «Nuova conversione» (grande, con l'area di rilascio) e lo stato. Gli **indicatori** compaiono solo quando c'è un foglio letto (prima erano tre o quattro zeri), il riquadro **«Ultime esportazioni»** solo se esiste una consegna o un'anteprima, **«Analisi del mese»** solo con un'anteprima pronta. Niente più riquadri vuoti
- **Indicatori da quattro a tre**: tolta «Richieste» (il numero è già nell'area di rilascio, con dipendenti e foglio)
- **Stato** con tre righe che servono (Database, Aggiornamenti, Regole): tolti «File Excel» (già nella scheda di conversione) e «Certificato» (dice già se scade con una notifica e nelle Impostazioni)
- **Barra di stato in fondo**: solo il messaggio; gli aggiornamenti compaiono solo se c'è una versione nuova o un errore. Tolti la versione e l'ora, ripetuti altrove
- **Barra laterale**: sparito il riquadro «Dati locali» (database, backup, azienda e versione, tutte cose già mostrate altrove). Restano la versione, piccola, e una riga di avviso solo se il database ha un problema
- Tolto il suggerimento «puoi trascinare un file Excel», ripetuto dall'area di rilascio
- Un test controlla quando compare ciascun riquadro
- La conversione XML non è toccata

## [1.53.0]

### 🛠️ Correzioni grafiche (dallo screenshot della 1.52.0)
- **Il tema «Vetro» ora si applica davvero**: chi aveva «Come Windows» salvato (il valore che le Impostazioni scrivono da sole) restava con il vecchio tema scuro blu. Una volta sola «Come Windows» diventa «Vetro»; scegliere di nuovo «Come Windows» o «Chiaro» / «Scuro» viene rispettato
- **«DATI LOCALI» non è più tagliato** nel riquadro in basso della barra laterale (la riga era più bassa del testo); la card cresce di quattro pixel
- **Titolo delle sezioni più staccato dalle card**: più aria fra sottotitolo e schede, e il testo parte esattamente dal bordo sinistro delle card (prima era spostato di 4–8 px)
- **«Analisi del mese» senza dati**: icona e testo centrati, come nell'elenco delle esportazioni, invece di una riga isolata a sinistra
- Un test controlla il passaggio al tema «Vetro» (una sola volta, scelte esplicite mai toccate)
- La conversione XML non è toccata

## [1.52.0]

### 🔮 Nuovo tema «Vetro» (predefinito)
- **Tema «Vetro»**, ispirato alla dashboard di riferimento: fondo viola-nero profondo, **card di vetro** con sfumatura leggera, bordo luminoso che si schiarisce verso l'angolo in alto a sinistra e un filo di luce sotto il bordo alto, **colori vivaci** (viola elettrico, ciano, verde menta, ambra, rosa)
- **Pulsanti più definiti**: i pulsanti pieni sono sfumati (più chiari in alto) con un riflesso sul bordo; quelli neutri sono appena velati con bordo viola
- **Barra laterale**: la voce attiva è una pillola viola vivace sfumata con testo e icona bianchi
- **Indicatori con andamento**: «Dipendenti», «Movimenti» e «Ore assegnate» mostrano una linea con area sfumata dell'andamento delle ultime otto esportazioni
- Si sceglie in Impostazioni → Generale → «Aspetto dell'app» (Vetro, Come Windows, Chiaro, Scuro); chi aveva già scelto un tema lo mantiene, gli altri partono da «Vetro». Si applica al prossimo avvio
- Tutti i contrasti del nuovo tema sono verificati dai test, come per gli altri
- La conversione XML non è toccata

## [1.51.0]

### 📊 Dashboard: confronto con l'ultima esportazione
- La scheda **«Ore assegnate»** mostra una pillola colorata con la variazione sull'ultima esportazione, per esempio «+3,1% sull'ultima esportazione» (verde se aumentano, arancio se diminuiscono, azzurra se uguali). Se l'anteprima è già stata esportata si confronta con quella prima. Senza esportazioni precedenti resta la didascalia di sempre
- Il calcolo è separato dal disegno e coperto da test (arrotondamento, segno, casi senza confronto)
- La conversione XML non è toccata

## [1.50.0]

### 🎨 Nuova grafica in stile Windows 11 (prima parte)
- **Tavolozza rifatta** in tema chiaro e scuro: blu Windows 11 al posto del blu «iOS», grigi con una leggera tinta azzurra, verde, arancio e rosso più sobri. Tutti i contrasti restano verificati dai test (testi, pillole, pulsanti, bordi)
- **Barra laterale**: la voce attiva è una pillola tenue del colore di marca con una barretta sul bordo sinistro, al posto della pillola piena; scorre fra le voci come prima
- **Card più morbide**: angoli di 14 px e un rilievo a tre strati sul bordo inferiore (nel tema scuro un filo di luce in alto)
- Pulsanti di pericolo e barre delle assenze seguono i colori del tema invece di colori fissi
- La conversione XML non è toccata

## [1.49.0]

### 🗄️ Database più veloce, più sicuro e ordinato
- **Passaggi di schema ordinati** (versione 6): ogni modifica allo schema è un passaggio numerato, applicato in una transazione insieme al nuovo numero di versione. Se qualcosa si interrompe il database resta com'era. Un test controlla che i numeri siano consecutivi. Prima di aggiornare un database con dati si continua a tenere una copia di sicurezza
- **Indici per le liste** (storico delle esportazioni, registro attività, ricerche per azienda): aprire le sezioni resta rapido anche con anni di storico
- **Ottimizzazione alla chiusura**: statistiche aggiornate per le ricerche e pulizia dei file XML rimasti senza esportazione; cache e tabelle temporanee in memoria
- **Backup verificati**: ogni copia nuova viene controllata (si apre, integrità, schema noto) prima di prendere il suo nome; una copia illeggibile non viene conservata e non fa eliminare quelle buone
- **«Stato del database» controlla tutti i backup** e dice quanti sono integri e quali sono danneggiati
- I dati restano in chiaro nel tuo profilo Windows: i backup non sono cifrati di proposito, perché una chiave legata a questo utente li renderebbe illeggibili proprio dopo un guasto del disco o una reinstallazione di Windows
- La conversione XML non è toccata

## [1.48.0]

### 🔔 Aggiornamenti in tempo reale
- **Controllo ogni 10 minuti** (prima ogni ora): una nuova Release arriva entro pochi minuti dalla pubblicazione. Sono 6 richieste all'ora, ben sotto il limite di GitHub
- **Controlli anche negli istanti giusti**: appena la rete torna disponibile, dodici secondi dopo l'uscita dalla sospensione del PC e quando si torna alla finestra dopo almeno cinque minuti. Mai due controlli a meno di due minuti l'uno dall'altro
- **L'avviso arriva anche a programma in uso**: dopo il primo controllo all'avvio, una versione nuova compare come cartellino nella finestra (clic per le novità e l'aggiornamento) invece di una finestra a sorpresa; a finestra nascosta o in secondo piano resta il fumetto di Windows con il lampeggio nella barra
- **Controllo fallito per la rete**: si riprova dopo 2 minuti (prima 5)
- **Avvia con Windows nel tray** (facoltativo): nelle Impostazioni → Aggiornamenti, dal menù dell'icona e come opzione dell'installer. Il programma parte senza aprire la finestra e controlla gli aggiornamenti anche quando non lo apri mai. Si disattiva dalle stesse voci e la disinstallazione toglie la voce

### 🎨 Menù dell'icona nel tray
- Voci con **icone** (aggiornamenti, impostazioni, dati, backup, cartelle), nello stile e nel tema dell'app; «Apri Vyrn Presence» in grassetto, aggiornamento disponibile in evidenza, «Esci» in rosso
- Gli interruttori mostrano lo stato nel testo («Notifiche di Windows · attive», «Avvia con Windows · sì»)
- La conversione XML non è toccata

## [1.47.0]

### 🎨 Menù, tendine e suggerimenti nello stile dell'app
- **Menù a comparsa rifatti** (tasto destro sul calendario, «Trasferisci», scelta del foglio, icona nell'area di notifica): sfondo e bordo del tema, voce evidenziata con angoli tondi, separatori leggeri, caratteri dell'app. Seguono il **tema scuro**, prima restavano grigio Windows
- **Spunta visibile** sul foglio scelto e sulle notifiche di Windows: prima lo spazio della spunta non c'era e la voce attiva non si distingueva
- **Tendine nuove** (mappatura colonne, filtro del Registro attività): campo con angoli tondi, bordo che si accende al passaggio e alla selezione, freccia disegnata che si capovolge ad elenco aperto, elenco con la stessa evidenziazione dei menù e barra di scorrimento scura nel tema scuro. L'elenco si allarga alla voce più lunga invece di tagliarla
- **Suggerimenti (tooltip)** con i colori del tema, testo a capo ordinato e margini regolari
- Test sul contrasto di menù e tendine in tema chiaro e scuro e controllo che nessun menù, tendina o suggerimento torni a quello di sistema
- La conversione XML non è toccata

## [1.46.0]

### ⌨️ Palette comandi
- **Ctrl+K apre la palette**: a campo vuoto propone i comandi più utili; scrivendo, accanto ai risultati (movimenti, aziende, dipendenti, giustificativi, esportazioni) compaiono i comandi pertinenti. Scrivendo **«>»** all'inizio si vedono **solo i comandi**
- **19 comandi**: apri un file Excel, genera anteprima e XML, salva l'XML, rimuovi il file caricato, vai a ogni sezione (Dashboard, Nuova conversione, Aziende, Gestione codici, Storico, Registro attività), apri le Impostazioni, cambia tema (scuro, chiaro, come Windows), crea un backup, apri la cartella dei dati o delle copie dei codici, cerca aggiornamenti, mostra le scorciatoie
- Ogni comando mostra la **scorciatoia** che lo richiama direttamente; si offrono solo i comandi eseguibili adesso (per esempio «Salva l'XML» solo con un'anteprima pronta)
- Su e Giù per scegliere, Invio per eseguire, Esc per chiudere; le parole si scrivono in qualsiasi ordine, senza accenti né maiuscole
- Se un testo non trova altro che movimenti, la ricerca fa quello di sempre
- Ogni cambio di tema dalla palette finisce nel Registro attività
- La conversione XML non è toccata

## [1.45.0]

### 🗂️ Registro delle attività
- **Nuova sezione «Registro attività»** nella barra laterale (scorciatoia **Ctrl+7**): cosa è stato fatto con il programma, con data e ora — importazioni e rimozioni del foglio, anteprime generate, XML esportati, verifiche dei file consegnati, codici eliminati, aziende salvate / eliminate / regole cambiate, backup, esportazioni e importazioni di codici, impostazioni salvate, aggiornamenti avviati
- **Ricerca** (parole in qualsiasi ordine, senza accenti) e **filtro per tipo** (Importazione, Anteprima, Esportazione, Codici, Aziende, Backup, Aggiornamento, Impostazioni, Controllo)
- **Esporta CSV** delle voci mostrate, pronto per Excel italiano (separatore «;», con BOM); le celle che sembrano formule sono neutralizzate
- Si conservano le **ultime 5.000 voci**; «Svuota registro» le elimina senza toccare codici, aziende ed esportazioni
- Il registro è un di più: se il database non risponde, l'operazione che stavi facendo non fallisce
- La conversione XML non è toccata

## [1.44.0]

### 🛡️ XML più robusti e sicuri (il file consegnato non cambia di un byte)
- **Il file contiene esattamente le ore dell'anteprima**: dopo il salvataggio l'app rilegge il file e confronta, per dipendente, giorno e giustificativo, i minuti scritti con quelli attesi. Se una sola ora differisce, il salvataggio è segnalato come da non consegnare e dice quale riga. Si aggiunge al controllo del tracciato che c'era già
- **Lettura protetta**: i file XML che l'app legge (il controllo del file scritto, l'esempio di Zucchetti per la forma) rifiutano le dichiarazioni DTD e non seguono riferimenti esterni, con un tetto alla dimensione: un file ostile non può far leggere risorse o riempire la memoria
- **Controlli in più sul tracciato**: elementi inattesi sotto «Fornitura», lo stesso dipendente in due nodi (PAGHE potrebbe contare due volte le ore), codici con spazi, date con anni fuori da 2000–2100 (tipico errore di foglio)
- **Storico → «Verifica file»**: confronta il file sul disco con la copia consegnata conservata nello storico e dice se è identico, modificato dopo l'esportazione o non si trova più; mostra l'**impronta SHA-256** e ricontrolla il tracciato
- Scrittura già a prova di interruzione (file temporaneo e sostituzione atomica) e invariata
- Test: il documento di riferimento accettato da Zucchetti è confrontato **byte per byte**, quindi un cambio involontario del formato farebbe fallire la CI

## [1.43.0]

### 🧭 Parte bassa della finestra più curata
- **Barra di stato a segmenti**: a sinistra il messaggio con un pallino disegnato (verde, giallo se è un avviso); a destra, separati da una linea sottile, **aggiornamenti**, **versione** e **ora**, ognuno con la sua icona. Un clic sul segmento degli aggiornamenti verifica subito se c'è una versione nuova. Con la finestra stretta spariscono prima l'ora e la versione; il messaggio ha sempre il suo spazio
- **Blocco «Dati locali» a scheda**: stato del database con pallino, ultimo backup, azienda in uso e versione, ciascuno con un'icona, dentro una scheda con i bordi arrotondati come il resto dell'app
- **Suggerimento a fine pagina** con l'icona «i» e la parola «Suggerimento» in evidenza, invece di una riga di testo isolata
- Tipografia coerente: stesso carattere e stesse spaziature in tutta la parte bassa (prima il testo della barra era più grande del resto e i pallini erano caratteri di testo)
- La conversione XML non è toccata

## [1.42.0]

### 🌙 Tema scuro
- **Nuovo tema scuro**, ispirato alla grafica di riferimento: fondo grafite, schede più chiare, azzurro luminoso per i pulsanti e le selezioni, verde/arancio/rosso schiariti per gli stati. La **barra del titolo di Windows** segue il tema
- **Si sceglie in Impostazioni → Generale → Aspetto dell'app**: *Come Windows* (predefinito: segue il tema delle app di Windows), *Chiaro* o *Scuro*. La scelta si applica **al prossimo avvio** del programma: chiudi e riapri
- Contrasto controllato con un test per ogni coppia testo/fondo (testi, pillole, pulsanti, griglie) in entrambi i temi, così nulla resta illeggibile
- Il tema chiaro resta identico a prima
- Restano di Windows (chiari anche nel tema scuro): finestre di messaggio e menu a comparsa del sistema
- Effetti sobri: nessuna animazione nuova oltre a quelle già presenti, che si fermano da sole se in Windows hai spento gli effetti
- La conversione XML non è toccata

## [1.41.0]

### 📊 Grafico «Ore al giorno»
- **Nuova sezione nella card «Analisi del mese»**: una colonna per ogni giorno del mese, **presenza in azzurro e assenza in arancio** sopra, con altezza proporzionale alle ore di tutti i dipendenti. I fine settimana restano vuoti, quindi si vedono a colpo d'occhio
- **Animazione a cascata**: le colonne crescono da sinistra a destra a ogni nuova anteprima (ferme se in Windows hai spento gli effetti di animazione)
- Il grafico compare solo se nella card c'è spazio anche per almeno una riga di giustificativi: con la finestra bassa tutto resta come prima, nulla viene tagliato
- La conversione XML non è toccata

## [1.40.0]

### 🔎 Ricerca globale
- **Il campo di ricerca in alto cerca ovunque**: oltre ai movimenti trova **aziende, dipendenti, giustificativi ed esportazioni**. Parole in qualsiasi ordine, senza distinguere maiuscole e accenti (stesse regole di prima)
- **Elenco dei risultati sotto il campo**: fino a 8 voci con icona, titolo e dettaglio. Si naviga con **Su/Giù**, si sceglie con **Invio** o con un clic, **Esc** lo chiude. La scelta apre la sezione giusta e seleziona la riga trovata (aziende ed esportazioni); per dipendenti e giustificativi apre Gestione codici sulla scheda giusta
- Nuova scorciatoia **Ctrl+K** per scrivere subito nel campo; **Ctrl+F** continua a filtrare i movimenti
- Se a corrispondere sono solo i movimenti, la ricerca fa quello di sempre: porta ai movimenti filtrati
- La conversione XML non è toccata

## [1.39.0]

### ✨ Dashboard più curata
- **Stato del sistema a pillole**: il valore di ogni riga è in una pillola colorata (verde = a posto, giallo = da controllare, rosso = problema), come nella grafica di riferimento
- **Nuova riga «Certificato»**: dice se l'app è firmata e fino a quando è valido il certificato (giallo negli ultimi 60 giorni, rosso se scaduto)
- **Anello animato** nell'area di rilascio mentre il foglio viene letto; fermo se in Windows hai spento gli effetti di animazione
- Testata, ricerca, pillola di stato, passi Importa · Verifica · Esporta e barra di stato in fondo c'erano già e restano come sono
- La conversione XML non è toccata

## [1.38.0]

### 🔄 Schermata di aggiornamento
- **Quattro passi ben visibili**: scaricamento, controllo del file, verifica della firma, installazione. Il passo in corso è evidenziato, quelli finiti hanno la spunta verde
- **Durante il download**: percentuale, dimensione scaricata, **velocità** e **tempo residuo** stimato (media mobile, non salta a ogni pacchetto). Se la rete si interrompe compare «nuovo tentativo» e le misure ripartono
- **Annulla** si può usare fino alla verifica della firma; dall'installazione in poi il pulsante si disattiva
- «Più tardi» e «Salta questa versione» restano nella finestra delle novità
- La conversione XML non è toccata

## [1.37.0]

### 🔔 Notifiche e Impostazioni
- **Clic sul fumetto**: toccando la notifica dell'icona vicino all'orologio si esegue l'azione (per esempio apre l'aggiornamento) invece di non fare nulla
- **Aggiornamento disponibile anche con la finestra chiusa**: voce **«Installa aggiornamento»** nel menu dell'icona e promemoria una volta al giorno, senza ripetersi a ogni controllo
- **Impostazioni, nuova scheda «Generale»**: comportamento alla chiusura (chiedi, riduci vicino all'orologio, esci), copia di sicurezza immediata dei codici e informazioni su dati e sicurezza
- La conversione XML non è toccata

## [1.36.0]

### 🧹 Tabelle e eliminazione
- **Righe delle tabelle non più ridimensionabili**: trascinando il bordo di una riga la si poteva allargare a dismisura (una riga alta mezza schermata). Ora righe e intestazioni hanno l'altezza decisa dall'app, in tutto il programma
- **Le aziende si possono eliminare**: il pulsante «Elimina» (e il tasto **Canc**) non trovava la riga selezionata perché le tabelle selezionavano la singola cella e non la riga. Ora aziende, storico delle esportazioni e ultime esportazioni della dashboard selezionano la **riga intera**, quindi anche «Usa in conversione», «Regole…», «Apri», «Copia» e «Salva una copia» agiscono sulla riga scelta
- **Gestione codici: si possono eliminare dipendenti e giustificativi**: nuovo pulsante **«Elimina»** (e tasto Canc) che toglie dagli abbinamenti la riga selezionata, con conferma. Per un dipendente si eliminano anche le sue ore e il suo periodo. Se il nome è nel file Excel aperto la riga resta, vuota, da compilare di nuovo
- La conversione XML non è toccata

## [1.35.0]

### 🔒 Aggiornamenti più sicuri
- **Firma controllata davvero**: prima di eseguire un installer scaricato l'app verifica con Windows (WinVerifyTrust) che la firma digitale sia **integra**, cioè che il file non sia stato modificato dopo la firma. Prima si leggeva solo il nome del firmatario, che non lo prova. Un installer modificato, con firma non valida, o non firmato al posto di uno firmato annulla l'aggiornamento
- **Cambio di certificato: decidi tu**: se l'installer è firmato da un certificato diverso da quello dell'app (rinnovo, ma potrebbe anche essere un file contraffatto) compare una richiesta di conferma con i due firmatari; prima veniva accettato in silenzio
- **Download in una cartella privata**: l'installer si scarica in `%LOCALAPPDATA%\DeGioia\PresenzeXmlConverter\updates` con un nome casuale, non più in `%TEMP%` con un nome prevedibile, e resta aperto in sola lettura fino all'avvio: nessun altro programma può sostituirlo fra la verifica e l'esecuzione. I file vecchi si eliminano da soli
- **Indirizzi controllati**: gli asset della Release devono stare sul repository atteso, su api.github.com in HTTPS, prima che il token venga inviato
- **Token cifrato sul disco** (DPAPI, legato al tuo utente Windows): un token salvato in chiaro dalle versioni precedenti viene riscritto cifrato alla prima lettura; copiato su un altro PC o utente non si legge

### 📜 Certificato di firma
- Ricorda **una volta al giorno** (e in Impostazioni → Aggiornamenti) quando il certificato di firma scade fra meno di 60 giorni o è scaduto; le firme già fatte restano valide grazie alla marca temporale
- Lo script `New-CodeSigningCertificate.ps1` crea ora il certificato con **chiave RSA 4096**
- README: nuova sezione «Sicurezza degli aggiornamenti» con i controlli, il token e il limite noto dei certificati autofirmati (e cosa serve per una firma riconosciuta da Windows senza installare nulla)
- La conversione XML non è toccata

## [1.34.0]

### 🪟 Barra del titolo chiara
- La **barra del titolo di Windows** (quella con «Vyrn Presence 1.34.0» e i tre pulsanti) ora è **bianca**, come l'intestazione dell'app, invece che nera. Su Windows 11 usa i colori esatti dell'app (fondo bianco, testo scuro, bordo sottile); su Windows 10 passa al tema chiaro. Vale anche se il sistema è in modalità scura

### 🎨 Icone ridisegnate
- **Tutte le icone dell'app sono state ridisegnate** su una griglia unica di 24 unità, a tratto uniforme con estremità e angoli tondi: coerenti fra loro, più nitide e proporzionate (ingranaggio a otto denti, orologio, cestino, scudo con spunta, database a tre dischi, persone, chip, cartella, tabella, freccia circolare…)
- Le cinque icone della **barra laterale** (Dashboard, Conversione, Aziende, Codici, Storico) usano la stessa libreria: non ci sono più due stili diversi. Nuove: Dashboard (quattro riquadri), Foglio con freccia, Edificio, Elenco
- Le icone sono ora **dati** (un unico disegno usato dal programma e dai test): un test controlla che ognuna esista, sia diversa dalle altre e stia dentro il margine della griglia, così nessun tratto viene tagliato
- La conversione XML non è toccata

## [1.33.0]

### 🌅 Cambio sezione animato
- Quando passi da una sezione all'altra (Dashboard, Conversione, Aziende, Gestione codici, Storico) la sezione nuova **compare con una breve dissolvenza** (circa 0,2 secondi) e un lieve scorrimento verso l'alto, invece di apparire di colpo
- Funziona fotografando la sezione nuova, già pronta, e mostrandola in un velo che **non blocca il mouse**; a fine animazione il velo si toglie e resta la sezione vera, quindi nulla cambia nel funzionamento
- Non parte all'avvio né se riselezioni la stessa sezione; con gli effetti di animazione di Windows disattivati, a finestra non visibile o in caso di errore di disegno la sezione appare subito, come prima
- La conversione XML non è toccata

## [1.32.0]

### 🔢 Conteggio animato nelle schede
- I quattro numeri della dashboard (**Richieste, Dipendenti, Movimenti, Ore assegnate**) **scorrono fino al valore nuovo** in circa 0,4 secondi quando carichi un file o generi l'anteprima, invece di cambiare di colpo
- Ogni numero scorre per conto suo e **mantiene il formato finale**: «176h 30m» passa per «88h 15m» (i minuti hanno sempre due cifre) e «1,234» non perde il separatore delle migliaia; se il nuovo valore è più basso, scende
- Se il valore non cambia non si rianima; con gli effetti di animazione di Windows disattivati, o con la dashboard non visibile, salta subito al valore
- La conversione XML non è toccata

## [1.31.0]

### 💾 Copia automatica di codici e aziende
- Il programma tiene da solo una **copia di codici, ore dei dipendenti e aziende** nella cartella dei dati (sottocartella `codici-backup`): la salva all'avvio e alla chiusura, **solo se qualcosa è cambiato**, una al giorno, e conserva le ultime sette
- Se cambi PC o reinstalli, il file è già pronto: basta copiarlo sul nuovo PC e usare **Trasferisci ▾ → Importa codici e aziende…** (il dialogo parte dalla cartella delle copie)
- Nuove voci: **«Apri la cartella delle copie automatiche»** nel menu Trasferisci e **«Apri cartella delle copie dei codici»** nel menu dell'icona vicino all'orologio
- Una copia che non riesce non disturba mai l'avvio o la chiusura; una copia rovinata viene sostituita, mai considerata valida
- La conversione XML non è toccata

## [1.30.0]

### 🎨 Icone e pulsanti rivisti
- **Quattro icone nuove** nello stesso stile a tratto (Più, Cestino, Trasferisci, Salva) e **icone sui pulsanti che ne mancavano**: Gestione codici (Salva abbinamenti, Ricarica elenchi, Trasferisci), Aziende (Salva azienda, Usa in conversione, Nuova, Regole, Elimina, Trasferisci) e il menu «Esporta» del calendario. A colpo d'occhio si distingue «Elimina» da «Salva»
- Icona e testo stanno **insieme al centro** del pulsante; se la larghezza non basta per entrambi (finestra stretta, DPI alto) **l'icona sparisce e l'etichetta resta intera**, mai tagliata
- I pulsanti con icona sono larghi quanto serve (a prova di schermi 13–15")
- I pulsanti della dashboard e delle azioni principali ora hanno la **stessa transizione morbida** al passaggio del mouse degli altri (prima il colore scattava)
- La conversione XML non è toccata

## [1.29.1]

### 🛠️ Correzione di «Trasferisci»
- **Esporta e importa codici e aziende non chiudono più il programma**: il menu «Trasferisci ▾» veniva eliminato alla chiusura, prima che il clic sulla voce fosse eseguito («Cannot access a disposed object… ContextMenuStrip»). Ora il menu resta vivo e un test sui sorgenti controlla che non si ripeta
- «Trasferisci ▾» c'è anche nella pagina **Aziende**, oltre a Gestione codici
- La conversione XML non è toccata

## [1.29.0]

### ✨ Animazioni più fluide
- **Barra laterale**: la pillola blu della sezione attiva **scorre** da una voce all'altra e il testo vira al bianco man mano che la copre, invece di saltare
- **Notifiche**: i cartellini **entrano scorrendo da destra**, escono nello stesso modo e gli altri si ricompongono con un movimento morbido
- **Pulsanti** e **area di rilascio del file**: il colore al passaggio del mouse scorre con una curva a tempo (veloce all'ingresso, più piano all'uscita) invece di avanzare a passi fissi; l'area del file cambia colore e bordo in modo graduale quando ci passi sopra o ci trascini un file
- **Analisi del mese** (dashboard): le fasce colorate e le barrette **crescono** a ogni nuova anteprima; se i numeri non cambiano non si rianima
- Le animazioni usano il tempo reale (stessa durata anche su un PC lento), durano meno di mezzo secondo e **si spengono da sole** se in Windows gli effetti di animazione sono disattivati (Impostazioni → Accessibilità)
- La conversione XML non è toccata

## [1.28.0]

### 🧳 Codici e aziende da portare su un altro PC
- Nuovo pulsante **«Trasferisci ▾»** in Gestione codici (e nel menu dell'icona vicino all'orologio): **Esporta codici e aziende…** salva in un file `.json` gli abbinamenti dei dipendenti e dei giustificativi, le ore e il periodo dei dipendenti e le aziende con le loro regole proprie
- **Importa codici e aziende…** legge quel file su un altro PC (o dopo una reinstallazione) e **unisce**: aggiunge i nuovi, aggiorna quelli con un codice diverso, lascia ciò che nel file non c'è. Lo stesso dipendente scritto in un altro ordine («Rossi Mario» / «Mario Rossi») si aggiorna, non si duplica
- Prima di importare chiede conferma e fa una **copia di sicurezza** del database; le voci non valide si scartano una per una e si elencano a fine importazione, senza fermare le altre
- Un file che non è di Vyrn Presence, rovinato o di una versione più recente viene rifiutato con il motivo, senza cambiare nulla. Lo storico delle esportazioni e le preferenze non fanno parte del file
- La conversione XML non è toccata

## [1.27.0]

### 🗑️ Rimuovi il file Excel
- Con un file caricato, l'area del file (in dashboard e nella scheda Conversione) mostra una **«×»**: un clic (o il tasto **Canc**) lo rimuove, per un file importato per sbaglio o che non serve più
- Prima di rimuovere chiede conferma; se c'è un'anteprima non ancora esportata lo dice, perché è l'unico lavoro che si perde
- Tornano allo stato iniziale anteprima, controlli, calendario e lavoro salvato; **codici salvati ed esportazioni già fatte non si toccano**
- La conversione XML non è toccata

## [1.26.0]

### 📊 Dashboard più ricca
- Nuova card **«Analisi del mese»** accanto alle ultime esportazioni: una barra presenza/assenza, i **giustificativi che pesano di più** (con gli stessi colori del calendario, ore e barretta proporzionale) e i giorni **da controllare / da verificare**
- Si aggiorna a ogni anteprima; senza dati invita a generarla. Mostra solo le righe che entrano nell'altezza, quindi non si taglia sul portatile
- La conversione XML non è toccata

## [1.25.0]

### 📄 Report PDF del mese
- Nel calendario il nuovo menu **«Esporta ▾»** raccoglie le tre esportazioni: **Report del mese (PDF)**, Riepilogo ore (CSV per Excel) e Immagine del mese (PNG). Un solo pulsante al posto dei due precedenti: la barra resta ordinata anche sul portatile
- Il **report PDF** (A4) ha in testa azienda e data, quattro riquadri di sintesi (dipendenti, ore registrate, ore di assenza, giorni da controllare), la tabella per dipendente con **presenza, assenza, totale, ore teoriche, differenza e giorni scoperti**, il dettaglio delle assenze per giustificativo e l'elenco di ciò che va controllato. Più mesi → una sezione per mese; molti dipendenti → più pagine numerate
- I numeri sono gli stessi del calendario e del CSV. Il PDF non richiede nessun programma aggiuntivo
- Dopo ogni esportazione compare un cartellino: un clic apre il file

## [1.24.0] - Corrente

### ♻️ Riprendi dove eri rimasto
- Il programma **salva da solo il lavoro in corso** (file importato, anteprima con le correzioni fatte a mano, se l'XML è già stato esportato) pochi secondi dopo ogni cambiamento e alla chiusura
- All'apertura **riporta tutto com'era**: riapre il file, ricrea l'anteprima con le tue modifiche e ti porta nella pagina di lavoro, con un avviso «Lavoro ripristinato»
- Se il file Excel è stato modificato nel frattempo l'anteprima salvata non vale più: viene riaperto il file e basta ricreare l'anteprima. Se il file non si trova più, l'anteprima salvata resta comunque disponibile
- Dal menu dell'icona nel tray, **«Dimentica il lavoro salvato»** fa ripartire il programma da zero alla prossima apertura

## [1.23.4]

### 👤 Lo stesso dipendente scritto in due modi
- Salvando gli abbinamenti non compare più «codice già usato: … non salvato» per «Vincenzo Antenori» e «Antenori Vincenzo»: nomi con le stesse parole (ordine, maiuscole, accenti) sono la **stessa persona** e possono avere lo stesso codice
- La conversione li unisce in un solo dipendente: giorni e presenze non si dividono più in due righe del calendario e l'XML non si ferma più su «codice associato a più nominativi»
- Due persone **diverse** con lo stesso codice restano un errore da correggere, ma il salvataggio non scarta più nulla: salva tutto e lo segnala
- Se solo una delle due grafie ha il codice, il dipendente non risulta più «senza codice»

## [1.23.3]

### ⚠️ Il calendario segnala le anomalie
- **Segno sul giorno**: contorno e pallino **rossi** quando le ore del giorno superano la giornata (es. 13h 20m su 8h), pallino **ambra** per i giorni insoliti (più giustificativi nello stesso giorno, movimento di sabato, domenica o festivo, fuori dal rapporto di lavoro). Il tooltip spiega il motivo
- **Totale del mese in ambra** quando non torna con le ore teoriche (con il dettaglio «+17 h rispetto alle ore teoriche» al passaggio del mouse)
- **Barra in alto** finché l'anteprima è aperta: conta i giorni da controllare e i controlli ancora aperti, con **«Vai al primo / prossimo»** (cambia mese, toglie i filtri se nascondono il dipendente e cerchia il giorno) e **«Apri i controlli»**
- Dopo la conversione compare anche un cartellino «Da controllare nel calendario» che porta alla scheda
- Il filtro è diventato **«Solo da controllare»**: dipendenti con giorni scoperti, anomalie o totale del mese che non torna
- Il calendario ora si aggiorna anche quando si modifica la griglia dei movimenti

### 💾 Abbinamenti salvabili a metà
- **«Salva abbinamenti» non pretende più che sia tutto compilato**: i codici inseriti restano nel database anche se ne mancano altri (prima un solo codice mancante bloccava il salvataggio e, chiudendo, si perdeva tutto). Solo le righe scritte male (codice troppo lungo, doppio, data non valida) restano fuori, con una nota che le elenca
- **Salvataggio automatico** nella sezione Gestione codici: poco dopo l'ultima modifica e alla chiusura del programma
- Chiudendo la finestra degli abbinamenti con «Annulla» o con la X dopo aver scritto dei codici, il programma chiede se salvare quelli inseriti
- Creando l'anteprima con codici ancora mancanti, quelli inseriti vengono salvati e il programma dice quanti ne restano

### 🖼️ Calendario: difetto grafico
- Scorrendo il calendario l'intestazione dei giorni e la colonna dei nomi lasciavano copie fantasma: ora si ridisegna a ogni scorrimento

## [1.23.2]

### 🔔 Notifiche che si vedono davvero
- **Avvisi dentro la finestra**: un cartellino in basso a destra per conversione completata, file XML salvato (un clic lo mostra nella cartella), copia negli appunti e aggiornamento disponibile. Si ritira da solo; col mouse sopra resta, un clic lo chiude
- Prima la notifica di Windows partiva solo a finestra non in primo piano: chi lavorava nel programma non vedeva nulla e le notifiche sembravano ferme
- Con il programma in background, oltre al fumetto di Windows il pulsante nella barra delle applicazioni **lampeggia** finché non torna davanti (funziona anche con le notifiche di Windows spente); il fumetto ora usa l'icona giusta (info, attenzione, errore)
- **Impostazioni → Notifiche**: si scelgono i due canali, c'è «Prova le notifiche» e il collegamento alle impostazioni di Windows (Assistente focus, notifiche dell'app)

## [1.23.1]

### 🗓️ Calendario: barra in alto corretta
- Le frecce del mese, il nome del mese e la ricerca non si sovrappongono più (l'etichetta era tagliata: «ettembre 2026»). La barra ora si dispone da sola e regge anche gli schermi ingranditi; «Salva immagine» e «Riepilogo CSV» hanno il giusto spazio fra loro

### 🔘 Pulsanti
- Più rilievo e coerenza: ombra sottile sotto il pulsante a riposo, anello di focus morbido da tastiera

### ⬆️ Aggiornamenti
- Il download mostra megabyte scaricati, totale e velocità, oltre alla percentuale
- Prima di installare, se c'è un'anteprima non ancora esportata in XML, il programma lo segnala e chiede conferma (l'installazione chiude l'app); l'aggiornamento resta disponibile dal menu
- Le Release sono pubbliche: senza token lo stato in basso dice «Aggiornamenti · attivi» invece dell'avviso «token GitHub da configurare»
- Durante l'installazione il messaggio spiega che il programma si chiude e si riapre da solo

## [1.23.0]

### 🗓️ Calendario più completo
- **Totale ore** di ogni dipendente a fine riga, con un contatore rosso dei giorni lavorativi senza movimenti
- **Riga dei presenti** in fondo: quanti dipendenti hanno la presenza in ogni giorno
- **Cerca dipendente** e filtro «Solo giorni scoperti» per trovare subito chi ha dei buchi
- Il giorno corrente è evidenziato, riga e colonna sotto il mouse si illuminano, le festività infrasettimanali sono in rosso nell'intestazione; passando sul nome o sul totale si leggono ore e giorni scoperti
- **Salva immagine**: esporta il mese in PNG (titolo, griglia e legenda)

### 📊 Riepilogo ore in CSV
- Nuovo pulsante **Riepilogo CSV** nel calendario: per ogni dipendente e mese ore di presenza, ore di assenza, totale, ore teoriche, differenza, giorni senza movimenti e dettaglio per giustificativo. Il file si apre direttamente in Excel (separatore «;», virgola decimale)
- La conversione XML per Zucchetti non cambia

## [1.22.16]

### 🗓️ Calendario del mese e controllo delle ore
- Nuova scheda **Calendario** nell'area di revisione: una riga per dipendente e una colonna per giorno, colorata per giustificativo (presenza in verde, ogni assenza con il suo colore, giorno misto diviso a metà). I giorni lavorativi senza movimenti hanno il contorno rosso tratteggiato; passando il mouse su un giorno si vede il dettaglio. Si sfoglia mese per mese
- Nuovo controllo in **Controlli**: per ogni dipendente e mese confronta le ore registrate con quelle teoriche (giorni lavorativi × ore della sua giornata, nel suo rapporto di lavoro) e segnala le differenze. Si attiva solo con le presenze automatiche accese
- Corretto un difetto dei controlli con le presenze automatiche: le presenze aggiunte dall'app non sono più segnalate come «giustificativo non configurato» né contate come «più giustificativi nello stesso giorno», e non falsano il confronto con le ore del foglio

## [1.22.15]

### 👤 Ore e rapporto di lavoro per dipendente
- **Gestione codici → Dipendenti**: tre colonne facoltative accanto al codice: **Ore al giorno** (part-time: 4, 6, 6,5…), **Assunto dal** e **Fino al**. Si salvano nel database locale insieme agli abbinamenti
- Le presenze `01` dei giorni senza assenza usano le ore del dipendente (non più le 8 standard) e non vengono scritte prima dell'assunzione né dopo la cessazione
- Anche la conversione delle richieste in giorni usa le ore del dipendente (4 ore di ferie per un part-time da 4 ore sono una giornata intera)
- Un dipendente senza questi dati segue le regole generali, come prima. Un valore scritto male blocca il salvataggio con un messaggio chiaro

## [1.22.14]

### ⚙️ Rifiniture
- **Impostazioni → Conversione**: nuova casella «Registra la presenza (01) nei giorni lavorativi senza assenze». Prima l'opzione c'era ma non si poteva spegnere; se la cambi l'anteprima va rigenerata
- Finestra di associazione delle colonne: il segnaposto non dice più «Colonna 0: Seleziona una colonna» ma solo «Seleziona una colonna», e il primo menu non parte più evidenziato di blu

## [1.22.13]

### 🎨 Nuovo aspetto in stile macOS (la conversione non cambia)
- Tavolozza chiara alla Apple: sfondo grigio perla, blu di sistema, verde/arancio/rosso di sistema, testi grigio antracite; titoli in «Segoe UI Semibold» al posto del grassetto
- Intestazione chiara (bianca, con bordo sottile) con il nuovo marchio e il campo di ricerca grigio; sparito il fondo blu notte
- Barra laterale: la sezione attiva è una pillola blu piena con testo e icona bianchi, al passaggio del mouse un velo grigio
- Schede dell'area di revisione a pillola (azzurra l'attiva) invece della sottolineatura
- Pulsanti bianchi con bordo sottile, transizione morbida al passaggio del mouse; card con un filo d'ombra; barra colorata in cima alle metriche; righe delle griglie evidenziate al passaggio

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
