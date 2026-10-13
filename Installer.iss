#define AppName "Vyrn Presence"
#define AppVersion "1.22.12"
#define AppPublisher "DeGioia"
#define AppExeName "PresenzeFlow.exe"

; Mutex tenuto aperto dall'app (vedi AppIdentity.cs): permette a un
; aggiornamento di riconoscere l'esecuzione in corso e di chiudere il
; programma in modo ordinato, invece di fallire sui file in uso.
#define AppMutex "DeGioia.PresenzeFlow.Setup"

; Identificatore delle notifiche e della barra delle applicazioni: ripetuto in
; AppIdentity.cs (AppUserModelId), il test lo tiene allineato.
#define AppUserModelId "DeGioia.PresenzeFlow"

; Attendibilita' del certificato: la CI passa -dTrustCertificate=1 e -dCertThumbprint=...
; (il file dist\PresenzeFlow-codesign.cer deve esistere). Senza questi simboli la voce
; non c'e', cosi' l'installer si compila anche in locale senza certificato.
#ifdef TrustCertificate
#ifndef CertThumbprint
#error "TrustCertificate richiede anche -dCertThumbprint=<impronta del certificato>."
#endif
#endif

; Versione minima di Windows: .NET 8 non funziona su Windows 7/8.1. Meglio un
; messaggio chiaro in fase di installazione che un'app che non parte.
; 10.0.14393 = Windows 10 1607, il minimo supportato da .NET 8 (self-contained).
#define WinMinVersion "10.0.14393"


; Build sempre pulita: a ogni compilazione vengono eliminati gli installer
; presenti in dist, così resta SEMPRE e SOLO l'ultima versione compilata.
; Exec è una funzione ISPP eseguita a tempo di compilazione: va lanciato
; iscc dalla root del progetto (come da README), dove risiede la cartella dist.
#define CleanupDone Exec("cmd.exe", "/C del /Q dist\PresenzeFlow-Setup-*.exe & exit /b 0", "", SW_HIDE)
#if CleanupDone
#endif

[Setup]
; Firma digitale: viene attivata solo quando il workflow passa -dEnableSigning=1
; insieme a un certificato configurato (secret CODE_SIGNING_PFX_BASE64).
; Senza certificato l'installer viene prodotto non firmato, senza errori.
#ifdef EnableSigning
SignTool=ci
SignedUninstaller=yes
#else
SignedUninstaller=no
#endif

AppId={{5D1D02CA-FE8F-4B86-9AF0-4B4F70A1F509}
AppName={#AppName}
AppVersion={#AppVersion}
AppMutex={#AppMutex}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppComments=Vyrn Presence: conversione locale di fogli presenze Excel in XML TRRIPW per Zucchetti.
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Installazione di {#AppName} {#AppVersion}
VersionInfoProductName={#AppName}
MinVersion={#WinMinVersion}
DefaultDirName={localappdata}\Programs\Vyrn Presence
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=assets\PresenzeXmlConverter.ico
; Tre scale per ciascuna immagine (100%, 150%, 200%): Inno Setup sceglie quella giusta
; per il DPI dello schermo, quindi il pannello resta nitido anche sugli schermi ad alta risoluzione.
WizardImageFile=assets\WizardImage.bmp,assets\WizardImage-150.bmp,assets\WizardImage-200.bmp
WizardSmallImageFile=assets\WizardSmallImage.bmp,assets\WizardSmallImage-150.bmp,assets\WizardSmallImage-200.bmp
AllowNoIcons=yes
OutputDir=dist
OutputBaseFilename=PresenzeFlow-Setup-{#AppVersion}
; Livello "normal" invece del default "max": l'installazione decomprime più
; velocemente (dizionario più piccolo) e consuma meno memoria, a fronte di un
; installer leggermente più grande. È un compromesso scelto per la velocità.
Compression=lzma2/normal
SolidCompression=yes
; Compressione parallela e in un processo separato: la build in CI è più rapida
; e la memoria del dizionario non grava sul processo dell'interfaccia.
LZMANumBlockThreads=4
LZMAUseSeparateProcess=yes
; Un file di log per ogni installazione (in %TEMP%): rende diagnosticabile
; a distanza qualsiasi problema segnalato dall'utente.
SetupLogging=yes
WizardStyle=modern
WizardSizePercent=120
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
UsePreviousGroup=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
#ifdef TrustCertificate
; Impronta del certificato pubblico incluso nell'installer: la verifica avviene durante l'installazione.
#define CertFileSha1 GetSHA1OfFile("dist\PresenzeFlow-codesign.cer")
; Certificato di firma dell'editore: con la firma autofirmata Windows mostra
; "editore sconosciuto" e SmartScreen/Defender sono diffidenti finche' il PC non lo
; riconosce. La voce lo aggiunge agli archivi dell'UTENTE CORRENTE (non del PC e
; senza privilegi di amministratore); Windows chiede conferma una volta.
Name: "trustcert"; Description: "Considera attendibile il certificato di firma di Vyrn Presence (solo per il tuo utente, evita gli avvisi di Windows)"; GroupDescription: "Sicurezza:"
#endif
Name: "desktopicon"; Description: "Crea un collegamento sul desktop"; GroupDescription: "Collegamenti aggiuntivi:"; Flags: unchecked

[Messages]
WelcomeLabel2=Questa procedura installa [name/ver] sul tuo computer.%n%nIl programma converte i fogli presenze Excel in file XML TRRIPW per Zucchetti, con controlli prima dell'esportazione. Tutto avviene sul tuo PC: nessun dato lascia il computer.%n%nNon servono privilegi di amministratore. Prima di proseguire chiudi gli altri programmi aperti.
FinishedLabel=[name] è installato. Lo trovi nel menu Start e, se scelto, sul desktop.%n%nPrima volta? Importa un foglio Excel, abbina i codici PAGHE e prova l'importazione in Zucchetti con un file di prova.
ClickFinish=Fai clic su Fine per chiudere l'installazione.

[Files]
Source: "dist\app\PresenzeFlow.exe"; DestDir: "{app}"; Flags: ignoreversion
; Dipendenza nativa NON inclusa nel single-file publish: senza questo file l'app
; termina all'avvio con DllNotFoundException (SQLite illeggibile) e non mostra alcuna finestra.
Source: "dist\app\e_sqlite3.dll"; DestDir: "{app}"; Flags: ignoreversion
#ifdef TrustCertificate
Source: "dist\PresenzeFlow-codesign.cer"; DestDir: "{app}"; Flags: ignoreversion
#endif

[InstallDelete]
Type: files; Name: "{app}\PresenzeXmlConverter.exe"
; Collegamenti con il vecchio nome (PresenzeFlow): rimossi, restano solo quelli di Vyrn Presence.
Type: files; Name: "{userprograms}\PresenzeFlow\PresenzeFlow.lnk"
Type: dirifempty; Name: "{userprograms}\PresenzeFlow"
Type: files; Name: "{autodesktop}\PresenzeFlow.lnk"
; Marcatore del vecchio modello AI locale (funzione rimossa).
Type: files; Name: "{localappdata}\DeGioia\PresenzeXmlConverter\ai-model.txt"

[Icons]
; AppUserModelID: lo stesso valore che l'app dichiara a Windows (AppIdentity.cs): senza
; un collegamento con questo identificatore le notifiche del tray non vengono
; attribuite al programma e Windows puo' sopprimerle.
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; AppUserModelID: "{#AppUserModelId}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon; AppUserModelID: "{#AppUserModelId}"

[UninstallDelete]
; Cartella del programma: se resta vuota dopo la rimozione dei file, sparisce anch'essa.
Type: dirifempty; Name: "{app}"

[Run]
; skipifsilent volutamente assente: dopo un aggiornamento silenzioso (avviato
; dall'app) Vyrn Presence si riapre da solo e mostra la finestra "Novità".
Filename: "{app}\{#AppExeName}"; Description: "Avvia {#AppName}"; WorkingDir: "{app}"; Flags: postinstall nowait

[Code]
const
  UninstallKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall';
  { Vista a 32 bit delle installazioni macchina: percorso esplicito per non }
  { dipendere dalle costanti di vista del registro. }
  UninstallKeyPathWow = 'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall';
  { Token di sola lettura per le Release della repository privata. }
  GithubTokenName = 'github.token';

var
  ReviewPage: TWizardPage;
  ReviewCheckBox: TNewCheckBox;
  RemoveLocalData: Boolean;

{ Cartella dei dati locali dell'utente, condivisa con l'applicazione. }
function DataDirectory(): String;
begin
  Result := ExpandConstant('{localappdata}\DeGioia\PresenzeXmlConverter');
end;

{ Token GitHub delle Release (la repository è privata). Viene preso dal
  parametro /GITHUBTOKEN=... (utile per installazioni guidate a distanza)
  oppure da un file github.token lasciato accanto all'installer. Se non c'è
  nulla, l'app lo chiede al primo avvio: qui non si inventa nulla e un token
  già presente non viene mai sovrascritto con un valore vuoto. }
{ Nota sui tipi: In Inno Setup 6 'String' e' Unicode e 'AnsiString' e' un tipo
  distinto. LoadStringFromFile vuole 'var S: AnsiString' e per un parametro var
  non esiste conversione implicita: passare una variabile String farebbe fallire
  la compilazione dell'installer con "Type mismatch" (errore realmente occorso).
  Si usa quindi una variabile AnsiString e la conversione esplicita UTF8Decode. }
function ProvidedGithubToken(): String;
var
  Raw: AnsiString;
  Source: String;
begin
  Result := Trim(ExpandConstant('{param:GITHUBTOKEN|}'));
  if Result = '' then
  begin
    Source := AddBackslash(ExpandConstant('{src}')) + GithubTokenName;
    if FileExists(Source) and LoadStringFromFile(Source, Raw) then
      Result := Trim(UTF8Decode(Raw));
  end;
  { Spazi e a capo vengono comunque ripuliti dall'app quando legge il file. }
  Result := Trim(Result);
end;

procedure ProvisionGithubToken();
var
  Token: String;
  Target: String;
begin
  Token := ProvidedGithubToken();
  if Token = '' then
    Exit;
  Target := AddBackslash(DataDirectory()) + GithubTokenName;
  ForceDirectories(DataDirectory());
  if SaveStringToFile(Target, UTF8Encode(Token), False) then
    Log('Token GitHub delle Release salvato: l''app si collega subito agli aggiornamenti.')
  else
    Log('Token GitHub non salvato: l''app lo chiederà al primo avvio.');
end;

#ifdef TrustCertificate
{ Il certificato e' attendibile in questo archivio? Nessun avviso di Windows: il comando
  si limita a cercare l'impronta nell'archivio dell'utente. }
function CertificateInStore(const StoreName: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\certutil.exe'), '-user -store ' + StoreName + ' {#CertThumbprint}',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

{ Aggiunge il certificato di firma agli archivi dell'utente corrente: "TrustedPublisher"
  (nessuna conferma) e "Root" (Windows mostra un avviso di sicurezza da confermare: e'
  quello che fa riconoscere la firma autofirmata). Mai in installazione silenziosa, cioe'
  durante un aggiornamento: il certificato e' gia' stato accettato la prima volta e un
  avviso a sorpresa non deve mai comparire. Un errore non ferma l'installazione. }
procedure TrustPublisherCertificate();
var
  CertificatePath: String;
  ResultCode: Integer;
begin
  if WizardSilent or not WizardIsTaskSelected('trustcert') then
    Exit;
  CertificatePath := ExpandConstant('{app}\PresenzeFlow-codesign.cer');
  if not FileExists(CertificatePath) then
  begin
    Log('Certificato di firma non trovato: ' + CertificatePath);
    Exit;
  end;
  { Si rende attendibile solo il certificato con cui e' stato firmato questo installer: l'impronta
    del file e' calcolata alla compilazione. Un file diverso (sostituito o danneggiato) non entra
    mai fra le autorita' attendibili del PC. }
  if CompareText(GetSHA1OfFile(CertificatePath), '{#CertFileSha1}') <> 0 then
  begin
    Log('Il certificato incluso non corrisponde a quello di firma: non viene reso attendibile.');
    Exit;
  end;
  { Gia' attendibile (reinstallazione, aggiornamento guidato): nessun nuovo avviso di Windows. }
  if CertificateInStore('TrustedPublisher') then
    Log('Il certificato e'' gia'' fra gli editori attendibili dell''utente.')
  else if Exec(ExpandConstant('{sys}\certutil.exe'), '-user -addstore -f TrustedPublisher "' + CertificatePath + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    Log('Certificato aggiunto agli editori attendibili dell''utente.')
  else
    Log(Format('Certificato non aggiunto agli editori attendibili (codice %d).', [ResultCode]));
  if CertificateInStore('Root') then
    Log('Il certificato e'' gia'' fra le autorita'' attendibili dell''utente.')
  else if Exec(ExpandConstant('{sys}\certutil.exe'), '-user -addstore -f Root "' + CertificatePath + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    Log('Certificato aggiunto alle autorita'' attendibili dell''utente.')
  else
    Log(Format('Certificato non aggiunto alle autorita'' attendibili (codice %d): conferma rifiutata o non consentita.', [ResultCode]));
end;

{ Alla disinstallazione il certificato aggiunto non resta: se ne torna al punto di
  partenza. In silenzioso si toglie solo quello degli editori (nessun avviso). }
procedure UntrustPublisherCertificate();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\certutil.exe'), '-user -delstore TrustedPublisher {#CertThumbprint}',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if not UninstallSilent then
    Exec(ExpandConstant('{sys}\certutil.exe'), '-user -delstore Root {#CertThumbprint}',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
#else
procedure TrustPublisherCertificate();
begin
end;

procedure UntrustPublisherCertificate();
begin
end;
#endif

procedure InitializeWizard;
var
  IntroText: TNewStaticText;
  DataText: TNewStaticText;
begin
  ReviewPage := CreateCustomPage(
    wpSelectDir,
    'Configurazione e dati',
    'Prima di installare Vyrn Presence'
  );

  IntroText := TNewStaticText.Create(ReviewPage);
  IntroText.Parent := ReviewPage.Surface;
  IntroText.Left := ScaleX(0);
  IntroText.Top := ScaleY(8);
  IntroText.Width := ReviewPage.SurfaceWidth;
  IntroText.Height := ScaleY(94);
  IntroText.WordWrap := True;
  IntroText.Font.Style := [fsBold];
  IntroText.Caption :=
    'Vyrn Presence importa fogli Excel, revisiona i movimenti giornalieri e genera XML TRRIPW per Zucchetti.' +
    Chr(13) + Chr(10) + Chr(13) + Chr(10) +
    'Il rilevamento di sabati e ore effettive dalle date è automatico e si può regolare dalle impostazioni; i codici PAGHE restano sempre sotto il tuo controllo.';

  DataText := TNewStaticText.Create(ReviewPage);
  DataText.Parent := ReviewPage.Surface;
  DataText.Left := ScaleX(0);
  DataText.Top := ScaleY(120);
  DataText.Width := ReviewPage.SurfaceWidth;
  DataText.Height := ScaleY(76);
  DataText.WordWrap := True;
  DataText.Caption :=
    'I mapping di dipendenti, giustificativi e codice azienda sono conservati nel profilo Windows dell''utente.' +
    ' Un aggiornamento mantiene questi dati; la disinstallazione permette di scegliere se conservarli o rimuoverli.' +
    Chr(13) + Chr(10) + Chr(13) + Chr(10) +
    'L''importazione in Zucchetti va verificata con un file di prova nella configurazione aziendale.';

  ReviewCheckBox := TNewCheckBox.Create(ReviewPage);
  ReviewCheckBox.Parent := ReviewPage.Surface;
  ReviewCheckBox.Left := ScaleX(0);
  ReviewCheckBox.Top := ScaleY(220);
  ReviewCheckBox.Width := ReviewPage.SurfaceWidth;
  ReviewCheckBox.Height := ScaleY(40);
  ReviewCheckBox.Caption :=
    'Confermo di aver letto e verificato le informazioni prima di procedere.';
  ReviewCheckBox.Checked := WizardSilent;

end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  { Installazione/aggiornamento silenzioso (avviato dall'app): nessuna interfaccia. }
  if WizardSilent then
    Exit;

  if (ReviewPage <> nil) and (CurPageID = ReviewPage.ID) and not ReviewCheckBox.Checked then
  begin
    MsgBox(
      'Conferma di aver letto le informazioni per proseguire.',
      mbInformation,
      MB_OK
    );
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;

  { Il token delle Release si salva anche in installazione silenziosa: così una
    distribuzione aziendale arriva con gli aggiornamenti già collegati. }
  ProvisionGithubToken();
  TrustPublisherCertificate();
end;

{ Interruttore presente sulla riga di comando del disinstallatore (senza distinzione fra maiuscole e minuscole). }
function UninstallSwitchPresent(const Switch: String): Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
    if CompareText(ParamStr(Index), Switch) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

{ Cartella dei dati con un nome inatteso: non si elimina mai ricorsivamente. }
function DataDirectoryIsSafeToDelete(): Boolean;
begin
  Result := CompareText(ExtractFileName(RemoveBackslash(DataDirectory())), 'PresenzeXmlConverter') = 0;
end;

function InitializeUninstall: Boolean;
var
  Answer: Integer;
  NewLine: String;
begin
  Result := True;
  RemoveLocalData := False;
  { Disinstallazione silenziosa: i dati si CONSERVANO, a meno che non sia
    richiesto /DELETEDATA (la cancellazione non deve mai essere una sorpresa). }
  if UninstallSilent then
  begin
    RemoveLocalData := UninstallSwitchPresent('/DELETEDATA') and not UninstallSwitchPresent('/KEEPDATA');
    Exit;
  end;

  NewLine := Chr(13) + Chr(10);
  Answer := MsgBox(
    'Vuoi CONSERVARE i dati di Vyrn Presence?' + NewLine + NewLine +
    'Sono il database locale con gli abbinamenti dei dipendenti e dei giustificativi, il codice azienda, ' +
    'lo storico delle esportazioni e i backup automatici.' + NewLine + NewLine +
    'Sì: conserva i dati. Se reinstalli Vyrn Presence li ritrovi com''erano.' + NewLine +
    'No: elimina i dati.' + NewLine +
    'Annulla: interrompi la disinstallazione.' + NewLine + NewLine +
    'Cartella dei dati: ' + DataDirectory(),
    mbConfirmation,
    MB_YESNOCANCEL
  );

  if Answer = IDCANCEL then
  begin
    Result := False;
    Exit;
  end;

  if Answer = IDNO then
  begin
    { L'eliminazione e' definitiva: serve una seconda conferma, con "No" preselezionato. }
    if MsgBox(
      'Confermi l''eliminazione DEFINITIVA di database, backup e storico delle esportazioni?' + NewLine + NewLine +
      'L''operazione non si può annullare. I file XML già salvati sul disco non vengono toccati.',
      mbConfirmation,
      MB_YESNO or MB_DEFBUTTON2
    ) = IDYES then
      RemoveLocalData := True;
  end;
end;

function DeleteDataFile(const FileName: String): Boolean;
begin
  if not FileExists(FileName) then
    Result := True
  else
    Result := DeleteFile(FileName);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDirectoryPath: String;
  DataRemoved: Boolean;
begin
  if CurUninstallStep = usUninstall then
    UntrustPublisherCertificate();

  if CurUninstallStep <> usPostUninstall then
    Exit;

  if not RemoveLocalData then
  begin
    { Dati conservati: restano database, backup, storico, token e impostazioni. }
    Log('Dati locali conservati in ' + DataDirectory());
    Exit;
  end;

  DataDirectoryPath := DataDirectory();
  if not DataDirectoryIsSafeToDelete() then
  begin
    Log('Cartella dei dati con nome inatteso: nessuna eliminazione ricorsiva (' + DataDirectoryPath + ').');
    Exit;
  end;

  { Database (con file WAL/SHM), backup, copie di file danneggiati, token e log: tutto cio' che Vyrn Presence
    ha creato nella propria cartella dei dati. }
  DataRemoved := (not DirExists(DataDirectoryPath)) or DelTree(DataDirectoryPath, True, True, True);
  RemoveDir(ExtractFileDir(DataDirectoryPath));

  if (not DataRemoved) or DirExists(DataDirectoryPath) then
    MsgBox(
      'Il programma è stato disinstallato, ma non è stato possibile rimuovere tutti i dati locali.' +
      Chr(13) + Chr(10) +
      'Chiudi le altre istanze dell''app e, se necessario, elimina manualmente la cartella:' +
      Chr(13) + Chr(10) +
      DataDirectoryPath,
      mbError,
      MB_OK
    );
end;
