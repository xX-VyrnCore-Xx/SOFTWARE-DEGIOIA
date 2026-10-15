using System.Runtime.InteropServices;

namespace PresenzeXmlConverter;

/// <summary>
/// Controlla con WinVerifyTrust che la firma digitale di un file sia integra: il file non è stato modificato
/// dopo la firma. Leggere il certificato di un file firmato (come fa <see cref="ExecutableSignature"/>) non lo
/// prova: la firma di un eseguibile può essere copiata su un altro. Senza controlli di revoca (nessuna rete).
/// </summary>
internal static class AuthenticodeVerifier
{
    private const uint UiNone = 2;
    private const uint RevokeNone = 0;
    private const uint ChoiceFile = 1;
    private const uint StateVerify = 1;
    private const uint StateClose = 2;
    private const uint RevocationCheckNone = 0x00000010;
    private const uint CacheOnlyUrlRetrieval = 0x00001000;
    private const int VerifierUnavailable = unchecked((int)0x80004005);

    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    /// <summary>L'esito di WinVerifyTrust (0 = firma valida e attendibile); un codice di errore se non si riesce a controllare.</summary>
    public static int Verify(string path)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
        {
            return VerifierUnavailable;
        }

        var filePointer = IntPtr.Zero;
        var dataPointer = IntPtr.Zero;
        var action = GenericVerifyV2;
        try
        {
            var file = new WintrustFileInfo
            {
                Size = (uint)Marshal.SizeOf<WintrustFileInfo>(),
                FilePath = path
            };
            filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
            Marshal.StructureToPtr(file, filePointer, false);

            var data = new WintrustData
            {
                Size = (uint)Marshal.SizeOf<WintrustData>(),
                UiChoice = UiNone,
                RevocationChecks = RevokeNone,
                UnionChoice = ChoiceFile,
                File = filePointer,
                StateAction = StateVerify,
                ProviderFlags = RevocationCheckNone | CacheOnlyUrlRetrieval
            };
            dataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustData>());
            Marshal.StructureToPtr(data, dataPointer, false);

            var result = WinVerifyTrust(new IntPtr(-1), ref action, dataPointer);

            // Si libera lo stato tenuto da Windows per la verifica.
            var closing = Marshal.PtrToStructure<WintrustData>(dataPointer);
            closing.StateAction = StateClose;
            Marshal.StructureToPtr(closing, dataPointer, false);
            WinVerifyTrust(new IntPtr(-1), ref action, dataPointer);
            return result;
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException or
            ArgumentException or AccessViolationException)
        {
            return VerifierUnavailable;
        }
        finally
        {
            if (dataPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(dataPointer);
            }
            if (filePointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(filePointer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, IntPtr data);
}
