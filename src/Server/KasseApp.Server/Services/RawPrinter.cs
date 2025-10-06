namespace KasseApp.Server.Services;

using System.Runtime.InteropServices;
using System.Text;

public static class RawPrinter
{
    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool OpenPrinter(string pPrinterName, out IntPtr hPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 di);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct DOC_INFO_1
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
    }

    public static void Send(string printerName, byte[] bytes)
    {
        if (!OpenPrinter(printerName, out var h, IntPtr.Zero))
            throw new InvalidOperationException($"OpenPrinter failed: {printerName}");

        try
        {
            var di = new DOC_INFO_1 { pDocName = "RAW", pDataType = "RAW", pOutputFile = null };
            if (!StartDocPrinter(h, 1, ref di)) throw new InvalidOperationException("StartDocPrinter failed");
            if (!StartPagePrinter(h)) throw new InvalidOperationException("StartPagePrinter failed");

            var unmanagedPtr = Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, unmanagedPtr, bytes.Length);
                if (!WritePrinter(h, unmanagedPtr, bytes.Length, out var written) || written != bytes.Length)
                    throw new InvalidOperationException("WritePrinter failed");
            }
            finally { Marshal.FreeCoTaskMem(unmanagedPtr); }

            EndPagePrinter(h);
            EndDocPrinter(h);
        }
        finally { ClosePrinter(h); }
    }

    public static void SendText(string printerName, string text, bool addLf = true)
        => Send(printerName, Encoding.UTF8.GetBytes(addLf ? text + "\n" : text));
}
