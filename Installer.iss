#define AppName "PresenzeFlow"
#define AppVersion "1.11.0"
#define AppPublisher "DeGioia"
#define AppExeName "PresenzeFlow.exe"

; Versione minima di Windows: .NET 8 non funziona su Windows 7/8.1. Meglio un
; messaggio chiaro in fase di installazione che un'app che non parte.
; 10.0.14393 = Windows 10 1607, il minimo supportato da .NET 8 (self-contained).
#define WinMinVersion "10.0.14393"

; Motore AI locale: URL ufficiale e modelli selezionabili.
#define OllamaUrl "https://ollama.com/download/OllamaSetup.exe"
#define OllamaSetupName "OllamaSetup.exe"
#define ModelFast "qwen2.5:0.5b"
#define ModelBalanced "qwen2.5:1.5b"
#define ModelQuality "qwen2.5:3b"

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
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppComments=PresenzeFlow: conversione locale di fogli presenze Excel in XML TRRIPW per Zucchetti.
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Installazione di {#AppName} {#AppVersion}
VersionInfoProductName={#AppName}
MinVersion={#WinMinVersion}
DefaultDirName={localappdata}\Programs\PresenzeFlow
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=assets\PresenzeXmlConverter.ico
WizardImageFile=assets\WizardImage.bmp
WizardSmallImageFile=assets\WizardSmallImage.bmp
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
Name: "desktopicon"; Description: "Crea un collegamento sul desktop"; GroupDescription: "Collegamenti aggiuntivi:"; Flags: unchecked
Name: "ai"; Description: "Installa l'AI locale (Ollama se assente + il modello scelto nella pagina successiva)"; GroupDescription: "Funzionalità opzionali:"; Flags: unchecked

[Messages]
SelectTasksLabel2=Seleziona le funzionalità opzionali da installare. L'AI locale NON è obbligatoria: la conversione funziona comunque senza.

[Files]
Source: "dist\app\PresenzeFlow.exe"; DestDir: "{app}"; Flags: ignoreversion
; Dipendenza nativa NON inclusa nel single-file publish: senza questo file l'app
; termina all'avvio con DllNotFoundException (SQLite illeggibile) e non mostra alcuna finestra.
Source: "dist\app\e_sqlite3.dll"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\PresenzeXmlConverter.exe"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; I passi di Ollama (download dell'installer, installazione e download del
; modello) NON sono più qui: sono gestiti dalla sezione [Code], dove l'utente
; vede una barra di avanzamento reale, può annullare e il download viene
; ripetuto automaticamente se la rete è instabile (PrepareToInstall).
; skipifsilent volutamente assente: dopo un aggiornamento silenzioso (avviato
; dall'app) PresenzeFlow si riapre da solo e mostra la finestra "Novità".
Filename: "{app}\{#AppExeName}"; Description: "Avvia {#AppName}"; WorkingDir: "{app}"; Flags: postinstall nowait

[Code]
const
  UninstallKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall';
  { Vista a 32 bit delle installazioni macchina: percorso esplicito per non }
  { dipendere dalle costanti di vista del registro. }
  UninstallKeyPathWow = 'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall';
  ModelMarkerName = 'ai-model.txt';
  { Token di sola lettura per le Release della repository privata. }
  GithubTokenName = 'github.token';

var
  ReviewPage: TWizardPage;
  ReviewCheckBox: TNewCheckBox;
  AiPage: TWizardPage;
  AiIntroText: TNewStaticText;
  AiFastRadio: TNewRadioButton;
  AiBalancedRadio: TNewRadioButton;
  AiQualityRadio: TNewRadioButton;
  AiNoneRadio: TNewRadioButton;
  AiNoteText: TNewStaticText;
  DownloadPage: TDownloadWizardPage;
  { Vero solo se l'installer di Ollama è stato scaricato nella cartella temporanea e va eseguito. }
  OllamaInstallerDownloaded: Boolean;
  { Modello scelto per questa installazione ('' = nessun modello da scaricare). }
  ModelToPull: String;
  RemoveLocalData: Boolean;
  RemoveAiModel: Boolean;

{ Cartella dei dati locali dell'utente, condivisa con l'applicazione. }
function DataDirectory(): String;
begin
  Result := ExpandConstant('{localappdata}\DeGioia\PresenzeXmlConverter');
end;

{ Legge la cartella di installazione di Ollama da una chiave Uninstall.
  Serve per le installazioni fatte in cartelle personalizzate (OllamaSetup /DIR=). }
function OllamaDirectoryFromRegistry(const RootKey: HKEY; const SubKeyPath: String): String;
var
  Names: TArrayOfString;
  Index: Integer;
  DisplayName: String;
  Location: String;
begin
  Result := '';
  if not RegGetSubkeyNames(RootKey, SubKeyPath, Names) then
    Exit;
  for Index := 0 to GetArrayLength(Names) - 1 do
  begin
    if RegQueryStringValue(RootKey, SubKeyPath + '\' + Names[Index], 'DisplayName', DisplayName) then
    begin
      if Pos('ollama', Lowercase(DisplayName)) > 0 then
      begin
        if RegQueryStringValue(RootKey, SubKeyPath + '\' + Names[Index], 'InstallLocation', Location) then
        begin
          if Location <> '' then
          begin
            Result := Location;
            Exit;
          end;
        end;
      end;
    end;
  end;
end;

{ Esiste una voce di disinstallazione di Ollama in questa chiave? }
function OllamaInUninstallKey(const RootKey: HKEY; const SubKeyPath: String): Boolean;
var
  Names: TArrayOfString;
  Index: Integer;
  DisplayName: String;
begin
  Result := False;
  if not RegGetSubkeyNames(RootKey, SubKeyPath, Names) then
    Exit;
  for Index := 0 to GetArrayLength(Names) - 1 do
  begin
    if RegQueryStringValue(RootKey, SubKeyPath + '\' + Names[Index], 'DisplayName', DisplayName) then
    begin
      if Pos('ollama', Lowercase(DisplayName)) > 0 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

{ Percorso di ollama.exe, cercato in tutti i posti in cui Windows può averlo
  messo: cartelle documentate, registro (installazioni personalizzate) e PATH.
  Restituisce '' se Ollama non è installato. }
function OllamaExecutablePath(): String;
var
  Candidate: String;
  Location: String;
begin
  Result := '';

  Candidate := ExpandConstant('{localappdata}\Programs\Ollama\ollama.exe');
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;
  Candidate := ExpandConstant('{localappdata}\Ollama\ollama.exe');
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;
  Candidate := ExpandConstant('{pf}\Ollama\ollama.exe');
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;
  Candidate := ExpandConstant('{pf32}\Ollama\ollama.exe');
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;

  Location := OllamaDirectoryFromRegistry(HKEY_CURRENT_USER, UninstallKeyPath);
  if Location = '' then
    Location := OllamaDirectoryFromRegistry(HKEY_LOCAL_MACHINE, UninstallKeyPath);
  if Location = '' then
    Location := OllamaDirectoryFromRegistry(HKEY_LOCAL_MACHINE, UninstallKeyPathWow);
  if Location <> '' then
  begin
    Candidate := AddBackslash(Location) + 'ollama.exe';
    if FileExists(Candidate) then
    begin
      Result := Candidate;
      Exit;
    end;
  end;

  Result := FileSearch('ollama.exe', GetEnv('Path'));
end;

{ Ollama è installato? Controlla l'eseguibile, il registro e il PATH: una
  installazione in una cartella personalizzata non deve sfuggire. }
function OllamaInstalled(): Boolean;
begin
  Result := OllamaExecutablePath() <> '';
  if not Result then
    Result := OllamaInUninstallKey(HKEY_CURRENT_USER, UninstallKeyPath);
  if not Result then
    Result := OllamaInUninstallKey(HKEY_LOCAL_MACHINE, UninstallKeyPath);
  if not Result then
    Result := OllamaInUninstallKey(HKEY_LOCAL_MACHINE, UninstallKeyPathWow);
end;

{ Token GitHub delle Release (la repository è privata). Viene preso dal
  parametro /GITHUBTOKEN=... (utile per installazioni guidate a distanza)
  oppure da un file github.token lasciato accanto all'installer. Se non c'è
  nulla, l'app lo chiede al primo avvio: qui non si inventa nulla e un token
  già presente non viene mai sovrascritto con un valore vuoto. }
function ProvidedGithubToken(): String;
var
  Value: String;
  Source: String;
begin
  Result := Trim(ExpandConstant('{param:GITHUBTOKEN|}'));
  if Result = '' then
  begin
    Source := AddBackslash(ExpandConstant('{src}')) + GithubTokenName;
    if FileExists(Source) and LoadStringFromFile(Source, Value) then
      Result := Trim(Value);
  end;
  StringChangeEx(Result, #13, '', True);
  StringChangeEx(Result, #10, '', True);
  StringChangeEx(Result, ' ', '', True);
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
  if SaveStringToFile(Target, Token, False) then
    Log('Token GitHub delle Release salvato: l''app si collega subito agli aggiornamenti.')
  else
    Log('Token GitHub non salvato: l''app lo chiederà al primo avvio.');
end;

{ Modello scelto in una installazione precedente (file scritto in [Code]). }
function InstalledModelFromMarker(): String;
var
  MarkerPath: String;
  Value: String;
begin
  Result := '';
  MarkerPath := AddBackslash(DataDirectory()) + ModelMarkerName;
  if not FileExists(MarkerPath) then
    Exit;
  if LoadStringFromFile(MarkerPath, Value) then
    Result := Trim(Value);
end;

{ Il modello è già nella cartella dei modelli di Ollama? In tal caso "ollama pull"
  è inutile e va saltato: è il caso tipico degli aggiornamenti e delle
  reinstallazioni, dove riscaricare 1 GB sarebbe solo una perdita di tempo.
  Controlla sia il layout storico (manifests) sia quello nuovo (manifests-v2). }
function ModelAlreadyDownloaded(const ModelName: String): Boolean;
var
  Repository: String;
  Tag: String;
  Separator: Integer;
  ModelsRoot: String;
  Relative: String;
begin
  Result := False;
  Repository := ModelName;
  Tag := 'latest';
  Separator := Pos(':', Repository);
  if Separator > 0 then
  begin
    Tag := Copy(Repository, Separator + 1, Length(Repository) - Separator);
    Repository := Copy(Repository, 1, Separator - 1);
  end;
  Separator := Pos('/', Repository);
  while Separator > 0 do
  begin
    Repository := Copy(Repository, Separator + 1, Length(Repository) - Separator);
    Separator := Pos('/', Repository);
  end;
  if (Repository = '') or (Tag = '') then
    Exit;

  ModelsRoot := GetEnv('OLLAMA_MODELS');
  if ModelsRoot = '' then
    ModelsRoot := ExpandConstant('{userprofile}\.ollama\models');

  Relative := 'library\' + Repository + '\' + Tag;
  if FileExists(AddBackslash(ModelsRoot) + 'manifests\registry.ollama.ai\' + Relative) then
  begin
    Result := True;
    Exit;
  end;
  if FileExists(AddBackslash(ModelsRoot) + 'manifests\ollama.com\' + Relative) then
  begin
    Result := True;
    Exit;
  end;
  if FileExists(AddBackslash(ModelsRoot) + 'manifests-v2\ollama.com\' + Relative) then
    Result := True;
end;

{ Modello da scaricare in base alla scelta dell'utente e al task "ai". }
function SelectedModel(): String;
begin
  Result := '';
  if not WizardIsTaskSelected('ai') then
    Exit;
  if AiFastRadio.Checked then
    Result := '{#ModelFast}'
  else if AiQualityRadio.Checked then
    Result := '{#ModelQuality}'
  else if AiNoneRadio.Checked then
    Result := ''
  else
    Result := '{#ModelBalanced}';
end;

{ La pagina dei modelli ha senso solo con il task "ai" attivo: senza, i pulsanti
  sono spenti e una nota spiega come attivare l'AI in seguito. }
procedure RefreshAiPage(Sender: TObject);
var
  AiSelected: Boolean;
begin
  AiSelected := WizardIsTaskSelected('ai');
  AiFastRadio.Enabled := AiSelected;
  AiBalancedRadio.Enabled := AiSelected;
  AiQualityRadio.Enabled := AiSelected;
  AiNoneRadio.Enabled := AiSelected;
  if AiSelected then
    AiNoteText.Caption :=
      'Ollama viene scaricato solo se non è già installato sul PC (può richiedere qualche minuto). ' +
      'Se è già presente viene scaricato soltanto il modello scelto: nessuna reinstallazione. ' +
      'Puoi cambiare scelta in seguito da PresenzeFlow → “Configura AI locale”.'
  else
    AiNoteText.Caption :=
      'L''AI locale non è selezionata: non verrà scaricato nulla. ' +
      'La conversione Excel → XML funziona comunque; potrai attivare l''AI in seguito da ' +
      'PresenzeFlow → “Configura AI locale”.';
end;

procedure TasksListClick(Sender: TObject);
begin
  RefreshAiPage(Sender);
end;

procedure InitializeWizard;
var
  IntroText: TNewStaticText;
  DataText: TNewStaticText;
  ExistingModel: String;
begin
  ReviewPage := CreateCustomPage(
    wpSelectDir,
    'Configurazione e dati',
    'Prima di installare PresenzeFlow'
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
    'PresenzeFlow importa fogli Excel, revisiona i movimenti giornalieri e genera XML TRRIPW per Zucchetti.' +
    Chr(13) + Chr(10) + Chr(13) + Chr(10) +
    'L''AI è opzionale. Se la configuri, l''assistente lavora tramite Ollama sul PC e non sostituisce la verifica dei codici PAGHE.';

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

  { Pagina di scelta del modello: compare subito dopo quella dei task, così }
  { l'utente vede esattamente quanti MB sta per scaricare. }
  AiPage := CreateCustomPage(
    wpSelectTasks,
    'AI locale (opzionale)',
    'Scegli il modello da scaricare'
  );

  AiIntroText := TNewStaticText.Create(AiPage);
  AiIntroText.Parent := AiPage.Surface;
  AiIntroText.Left := ScaleX(0);
  AiIntroText.Top := ScaleY(0);
  AiIntroText.Width := AiPage.SurfaceWidth;
  AiIntroText.Height := ScaleY(36);
  AiIntroText.WordWrap := True;
  AiIntroText.Caption :=
    'Tutti i modelli restano sul tuo PC e funzionano senza connessione dopo il download. ' +
    'Puoi cambiare modello in qualsiasi momento dall''app.';

  AiFastRadio := TNewRadioButton.Create(AiPage);
  AiFastRadio.Parent := AiPage.Surface;
  AiFastRadio.Left := ScaleX(0);
  AiFastRadio.Top := ScaleY(44);
  AiFastRadio.Width := AiPage.SurfaceWidth;
  AiFastRadio.Height := ScaleY(22);
  AiFastRadio.Caption := 'Minimo — {#ModelFast} (circa 400 MB): risposte immediate anche su PC datati';

  AiBalancedRadio := TNewRadioButton.Create(AiPage);
  AiBalancedRadio.Parent := AiPage.Surface;
  AiBalancedRadio.Left := ScaleX(0);
  AiBalancedRadio.Top := ScaleY(68);
  AiBalancedRadio.Width := AiPage.SurfaceWidth;
  AiBalancedRadio.Height := ScaleY(22);
  AiBalancedRadio.Caption := 'Rapido — {#ModelBalanced} (circa 1 GB): buon equilibrio, un po'' meno preciso sui casi dubbi';

  AiQualityRadio := TNewRadioButton.Create(AiPage);
  AiQualityRadio.Parent := AiPage.Surface;
  AiQualityRadio.Left := ScaleX(0);
  AiQualityRadio.Top := ScaleY(92);
  AiQualityRadio.Width := AiPage.SurfaceWidth;
  AiQualityRadio.Height := ScaleY(22);
  AiQualityRadio.Caption := 'Consigliato — {#ModelQuality} (circa 2 GB): il più affidabile nell''abbinare codici e colonne';

  AiNoneRadio := TNewRadioButton.Create(AiPage);
  AiNoneRadio.Parent := AiPage.Surface;
  AiNoneRadio.Left := ScaleX(0);
  AiNoneRadio.Top := ScaleY(116);
  AiNoneRadio.Width := AiPage.SurfaceWidth;
  AiNoneRadio.Height := ScaleY(22);
  AiNoneRadio.Caption := 'Nessun modello adesso — installa solo Ollama: potrai scaricarlo dall''app';

  AiNoteText := TNewStaticText.Create(AiPage);
  AiNoteText.Parent := AiPage.Surface;
  AiNoteText.Left := ScaleX(0);
  AiNoteText.Top := ScaleY(148);
  AiNoteText.Width := AiPage.SurfaceWidth;
  AiNoteText.Height := ScaleY(48);
  AiNoteText.WordWrap := True;

  { La scelta fatta in una installazione precedente viene riproposta: un }
  { aggiornamento non deve riscaricare un modello diverso da quello in uso. }
  ExistingModel := InstalledModelFromMarker();
  if ExistingModel = '{#ModelFast}' then
    AiFastRadio.Checked := True
  else if ExistingModel = '{#ModelBalanced}' then
    AiBalancedRadio.Checked := True
  else
    AiQualityRadio.Checked := True;

  DownloadPage := CreateDownloadPage(
    'Scaricamento dei componenti opzionali',
    'Il motore AI locale (Ollama) viene scaricato dal sito ufficiale. Puoi annullare: l''installazione proseguirà senza AI.',
    nil
  );
  DownloadPage.ShowBaseNameInsteadOfUrl := True;

  WizardForm.TasksList.OnClickCheck := @TasksListClick;
  RefreshAiPage(nil);
end;

{ Un tentativo di download con barra di avanzamento: restituisce True se il file
  è stato scaricato in {tmp}. }
function TryDownloadOllama(const AttemptNumber: Integer): Boolean;
begin
  Result := False;
  DownloadPage.Clear;
  DownloadPage.Add('{#OllamaUrl}', '{#OllamaSetupName}', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      Result := True;
    except
      if DownloadPage.AbortedByUser then
        Log('Download di Ollama annullato dall''utente.')
      else
        Log(Format('Tentativo %d di download di Ollama non riuscito: %s', [AttemptNumber, GetExceptionMessage]));
    end;
  finally
    DownloadPage.Hide;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Attempt: Integer;
  SetupPath: String;
begin
  Result := True;

  { Installazione/aggiornamento silenzioso (avviato dall'app): non c'è interfaccia
    e non si scaricano componenti. Un modello mancante lo scarica l'app, con la
    propria barra di avanzamento. }
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
    Exit;
  end;

  if CurPageID <> wpReady then
    Exit;

  ModelToPull := SelectedModel();
  OllamaInstallerDownloaded := False;

  if not WizardIsTaskSelected('ai') then
    Exit;

  { Ollama già installato: nessun download da fare, si scaricherà solo il modello. }
  if OllamaInstalled() then
  begin
    Log('Ollama è già installato: nessun download del motore AI.');
    Exit;
  end;

  SetupPath := ExpandConstant('{tmp}\{#OllamaSetupName}');
  if FileExists(SetupPath) then
  begin
    OllamaInstallerDownloaded := True;
    Exit;
  end;

  for Attempt := 1 to 3 do
  begin
    if TryDownloadOllama(Attempt) then
    begin
      OllamaInstallerDownloaded := True;
      Break;
    end;
    if DownloadPage.AbortedByUser then
      Break;
    Log('Nuovo tentativo di download tra 2 secondi.');
    Sleep(2000);
  end;

  if OllamaInstallerDownloaded then
    Exit;

  if MsgBox(
    'Non è stato possibile scaricare Ollama (rete assente o download interrotto).' + Chr(13) + Chr(10) + Chr(13) + Chr(10) +
    'Sì: prosegui senza AI locale (potrai attivarla in seguito dall''app).' + Chr(13) + Chr(10) +
    'No: torna indietro e riprova.',
    mbConfirmation,
    MB_YESNO
  ) = IDYES then
  begin
    ModelToPull := '';
    Result := True;
  end
  else
    Result := False;
end;

{ Il download in {tmp} viene eseguito qui: siamo già nella fase "Installazione",
  quindi la finestra mostra l'avanzamento e l'utente vede che sta lavorando. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  SetupPath: String;
  ResultCode: Integer;
begin
  Result := '';
  if not OllamaInstallerDownloaded then
    Exit;

  SetupPath := ExpandConstant('{tmp}\{#OllamaSetupName}');
  if not FileExists(SetupPath) then
    Exit;

  WizardForm.StatusLabel.Caption := 'Installazione del motore AI locale (Ollama)…';
  if not Exec(SetupPath, '/VERYSILENT /NORESTART /SP-', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Impossibile avviare l''installer di Ollama.');
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    Log(Format('L''installer di Ollama è terminato con codice %d: secondo tentativo in modalità interattiva.', [ResultCode]));
    if Exec(SetupPath, '', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      Log(Format('Secondo tentativo di installazione di Ollama terminato con codice %d.', [ResultCode]))
    else
      Log('Secondo tentativo di installazione di Ollama non avviato.');
  end;

  if OllamaExecutablePath() <> '' then
    Log('Ollama risulta installato.')
  else
    Log('Ollama non rilevato dopo l''installazione: il modello verrà scaricato dall''app al primo uso.');
end;

{ Scrive il modello preferito per l'app e scarica il modello scelto (solo se
  non è già presente). In caso di problemi l'installazione NON fallisce: l'app
  scarica il modello al primo uso, con la propria barra di avanzamento. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  MarkerPath: String;
  ExePath: String;
  ResultCode: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;

  { Il token delle Release si salva anche in installazione silenziosa: così una
    distribuzione aziendale arriva con gli aggiornamenti già collegati. }
  ProvisionGithubToken();

  { Aggiornamento silenzioso: le preferenze dell'utente (incluso il modello
    scelto) restano esattamente come sono, senza download imprevisti. }
  if WizardSilent then
    Exit;

  MarkerPath := AddBackslash(DataDirectory()) + ModelMarkerName;
  if ModelToPull <> '' then
  begin
    ForceDirectories(DataDirectory());
    if not SaveStringToFile(MarkerPath, ModelToPull, False) then
      Log('Non è stato possibile salvare il modello preferito: l''app userà il modello predefinito.');
  end
  else
    DeleteFile(MarkerPath);

  if ModelToPull = '' then
    Exit;

  if ModelAlreadyDownloaded(ModelToPull) then
  begin
    Log(Format('Il modello %s è già presente: download saltato.', [ModelToPull]));
    Exit;
  end;

  ExePath := OllamaExecutablePath();
  if ExePath = '' then
  begin
    Log('Ollama non trovato: il modello verrà scaricato dall''app al primo uso.');
    Exit;
  end;

  WizardForm.StatusLabel.Caption :=
    Format('Scaricamento del modello AI %s (alcuni minuti)…', [ModelToPull]);
  if Exec(ExePath, 'pull ' + ModelToPull, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode <> 0 then
      Log(Format('"ollama pull %s" è terminato con codice %d: il modello verrà scaricato dall''app.', [ModelToPull, ResultCode]));
  end
  else
    Log('Impossibile avviare ollama per scaricare il modello: lo farà l''app al primo uso.');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (AiPage <> nil) and (CurPageID = AiPage.ID) then
    RefreshAiPage(nil);
end;

function InitializeUninstall: Boolean;
var
  Answer: Integer;
  NewLine: String;
begin
  Result := True;
  RemoveLocalData := False;
  RemoveAiModel := False;
  if UninstallSilent then
    Exit;

  NewLine := Chr(13) + Chr(10);
  Answer := MsgBox(
    'PresenzeFlow: vuoi eliminare anche i dati locali (mapping dipendenti, giustificativi e codice azienda)?' +
    NewLine + NewLine +
    'Sì: rimuovi i dati locali dell''app.' + NewLine +
    'No: disinstalla il programma ma conserva i dati per una reinstallazione.' +
    NewLine + NewLine +
    'Annulla: interrompi la disinstallazione.',
    mbConfirmation,
    MB_YESNOCANCEL
  );

  if Answer = IDYES then
    RemoveLocalData := True
  else if Answer = IDCANCEL then
  begin
    Result := False;
    Exit;
  end;

  // Seconda domanda: solo se Ollama è presente chiedi se rimuovere il modello.
  if OllamaInstalled() then
  begin
    if MsgBox(
      'Vuoi rimuovere anche il modello AI locale scaricato per PresenzeFlow?' +
      NewLine + NewLine +
      'Sì: elimina solo il modello scelto in fase di installazione; Ollama resta installato.' + NewLine +
      'No: mantieni il modello per eventuali reinstallazioni o altri programmi.' +
      NewLine + NewLine +
      'La disinstallazione di Ollama non viene mai toccata.',
      mbConfirmation,
      MB_YESNO
    ) = IDYES then
      RemoveAiModel := True;
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
  SetupDirectory: String;
  ModelName: String;
  ExePath: String;
  DataRemoved: Boolean;
  ResultCode: Integer;
begin
  if (CurUninstallStep = usUninstall) and RemoveAiModel then
  begin
    // Rimuove solo il modello installato per PresenzeFlow (annotato su disco
    // dall'installer); ignora gli errori perché Ollama potrebbe non rispondere.
    ModelName := InstalledModelFromMarker();
    if ModelName = '' then
      ModelName := '{#ModelBalanced}';
    ExePath := OllamaExecutablePath();
    if ExePath <> '' then
      Exec(ExePath, 'rm ' + ModelName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if (CurUninstallStep <> usPostUninstall) or not RemoveLocalData then
    Exit;

  DataDirectoryPath := DataDirectory();
  SetupDirectory := AddBackslash(DataDirectoryPath) + 'setup';

  DataRemoved := DeleteDataFile(AddBackslash(DataDirectoryPath) + 'settings.db-wal');
  DataRemoved := DeleteDataFile(AddBackslash(DataDirectoryPath) + 'settings.db-shm') and DataRemoved;
  DataRemoved := DeleteDataFile(AddBackslash(DataDirectoryPath) + 'settings.db') and DataRemoved;
  DeleteDataFile(AddBackslash(DataDirectoryPath) + ModelMarkerName);
  // Il token di accesso alle Release private non deve restare sul PC.
  DeleteDataFile(AddBackslash(DataDirectoryPath) + 'github.token');
  DeleteFile(AddBackslash(SetupDirectory) + '{#OllamaSetupName}');
  RemoveDir(SetupDirectory);
  RemoveDir(DataDirectoryPath);
  RemoveDir(ExtractFileDir(DataDirectoryPath));

  if not DataRemoved then
    MsgBox(
      'Il programma è stato disinstallato, ma non è stato possibile rimuovere tutti i file locali.' +
      Chr(13) + Chr(10) +
      'Chiudi le altre istanze dell''app e, se necessario, elimina manualmente settings.db in:' +
      Chr(13) + Chr(10) +
      DataDirectoryPath,
      mbError,
      MB_OK
    );
end;
