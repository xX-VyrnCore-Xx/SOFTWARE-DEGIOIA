#define AppName "PresenzeFlow"
#define AppVersion "1.7.0"
#define AppPublisher "DeGioia"
#define AppExeName "PresenzeFlow.exe"

; Build sempre pulita: a ogni compilazione vengono eliminati gli installer
; presenti in dist, così resta SEMPRE e SOLO l'ultima versione compilata.
; Exec è una funzione ISPP eseguita a tempo di compilazione: va lanciato
; iscc dalla root del progetto (come da README), dove risiede la cartella dist.
#define CleanupDone Exec("cmd.exe", "/C del /Q dist\PresenzeFlow-Setup-*.exe & exit /b 0", "", SW_HIDE)
#if CleanupDone
#endif

[Setup]
AppId={{5D1D02CA-FE8F-4B86-9AF0-4B4F70A1F509}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppComments=PresenzeFlow: conversione locale di fogli presenze Excel in XML TRRIPW per Zucchetti.
DefaultDirName={localappdata}\Programs\PresenzeFlow
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=assets\PresenzeXmlConverter.ico
WizardImageFile=assets\WizardImage.bmp
WizardSmallImageFile=assets\WizardSmallImage.bmp
OutputDir=dist
OutputBaseFilename=PresenzeFlow-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
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
Name: "ai"; Description: "Installa l'AI locale (Ollama + modello qwen2.5:1.5b, circa 2 GB, richiede connessione)"; GroupDescription: "Funzionalità opzionali:"; Flags: unchecked

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
; Installazione AI locale (opzionale, selezionata dal task "ai"):
; 1) scarica l'installer ufficiale di Ollama, 2) lo esegue in modalità silenziosa,
; 3) scarica il modello qwen2.5:1.5b. In caso di errore l'installazione prosegue:
; l'AI resta comunque configurabile dal programma.
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Invoke-WebRequest -Uri 'https://ollama.com/download/OllamaSetup.exe' -OutFile '{tmp}\OllamaSetup.exe'"""; StatusMsg: "Scaricamento di Ollama (oltre 1 GB, può richiedere alcuni minuti)…"; Tasks: ai; Flags: runascurrentuser waituntilterminated
Filename: "{tmp}\OllamaSetup.exe"; Parameters: "/VERYSILENT /NORESTART /SP-"; StatusMsg: "Installazione di Ollama in corso…"; Tasks: ai; Flags: runascurrentuser waituntilterminated shellexec
; "ollama pull" avvia automaticamente il server locale se non è ancora attivo.
Filename: "{localappdata}\Programs\Ollama\ollama.exe"; Parameters: "pull qwen2.5:1.5b"; StatusMsg: "Scaricamento del modello AI locale qwen2.5:1.5b (circa 1 GB)…"; Tasks: ai; Flags: runascurrentuser waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "Avvia {#AppName}"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent

[Code]
var
  ReviewPage: TWizardPage;
  ReviewCheckBox: TNewCheckBox;
  RemoveLocalData: Boolean;
  RemoveAiModel: Boolean;

procedure InitializeWizard;
var
  IntroText: TNewStaticText;
  DataText: TNewStaticText;
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
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
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
  if FileExists(ExpandConstant('{localappdata}\Programs\Ollama\ollama.exe')) then
  begin
    if MsgBox(
      'Vuoi rimuovere anche il modello AI locale qwen2.5:1.5b scaricato per PresenzeFlow?' +
      NewLine + NewLine +
      'Sì: elimina solo il modello (circa 1 GB); Ollama resta installato.' + NewLine +
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
  DataDirectory: String;
  SetupDirectory: String;
  DataRemoved: Boolean;
  ResultCode: Integer;
begin
  if (CurUninstallStep = usUninstall) and RemoveAiModel then
  begin
    // Rimuove solo il modello scaricato per PresenzeFlow; ignora gli errori
    // (Ollama potrebbe non essere più raggiungibile).
    Exec(ExpandConstant('{localappdata}\Programs\Ollama\ollama.exe'),
      'rm qwen2.5:1.5b', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if (CurUninstallStep <> usPostUninstall) or not RemoveLocalData then
    Exit;

  DataDirectory := ExpandConstant('{localappdata}\DeGioia\PresenzeXmlConverter');
  SetupDirectory := AddBackslash(DataDirectory) + 'setup';

  DataRemoved := DeleteDataFile(AddBackslash(DataDirectory) + 'settings.db-wal');
  DataRemoved := DeleteDataFile(AddBackslash(DataDirectory) + 'settings.db-shm') and DataRemoved;
  DataRemoved := DeleteDataFile(AddBackslash(DataDirectory) + 'settings.db') and DataRemoved;
  DeleteFile(AddBackslash(SetupDirectory) + 'OllamaSetup.exe');
  RemoveDir(SetupDirectory);
  RemoveDir(DataDirectory);
  RemoveDir(ExtractFileDir(DataDirectory));

  if not DataRemoved then
    MsgBox(
      'Il programma è stato disinstallato, ma non è stato possibile rimuovere tutti i file locali.' +
      Chr(13) + Chr(10) +
      'Chiudi le altre istanze dell''app e, se necessario, elimina manualmente settings.db in:' +
      Chr(13) + Chr(10) +
      DataDirectory,
      mbError,
      MB_OK
    );
end;
