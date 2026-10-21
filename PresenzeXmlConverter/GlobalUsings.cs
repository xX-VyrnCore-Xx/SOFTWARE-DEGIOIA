// WPF aggiunge Microsoft.Win32 agli using impliciti: i suoi OpenFileDialog e SaveFileDialog si scontrano con
// quelli di Windows Forms, che il programma classico usa ovunque. Un alias vince sull'importazione dello spazio dei nomi.
global using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
global using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
