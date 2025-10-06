namespace KasseApp.Server.Services;
using System.Text;           
using System.Collections.Generic; 
using Microsoft.Extensions.Configuration; 

public class EscPosService
{
    private readonly string _printer;
    private readonly IConfiguration _cfg;

    public EscPosService(IConfiguration cfg)
    {
        _cfg = cfg;
        _printer = _cfg["Devices:PrinterName"] ?? "";
    }

    public void PrintText(string text, bool cut = true, bool openDrawerAfter = false)
    {
        var init = new byte[] { 0x1B, 0x40 }; // ESC @ init
        var data = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n").Replace("\r", "\n"));
        var lf   = new byte[] { 0x0A };
        var buf = new List<byte>();
        buf.AddRange(init);
        buf.AddRange(data);
        buf.AddRange(lf);

        if (openDrawerAfter) buf.AddRange(DrawerPulse());
        if (cut) buf.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // GS V B 0 (partial cut)

        RawPrinter.Send(_printer, buf.ToArray());
    }

    public void OpenDrawer()
    {
        var viaPrinter = bool.TryParse(_cfg["Devices:CashDrawer:OpenViaPrinter"], out var v) && v;
        if (viaPrinter)
            RawPrinter.Send(_printer, DrawerPulse());
        else
            OpenDrawerOnCom();
    }

    private static byte[] DrawerPulse()
    {
        var t1 = (byte)(int.Parse("100")); 
        var t2 = (byte)(int.Parse("100"));
        return new byte[] { 0x1B, 0x70, 0x00, t1, t2 };
    }

    private void OpenDrawerOnCom()
    {
        var port = _cfg["Devices:CashDrawer:ComPort"] ?? "COM1";
        var pulseOn  = int.TryParse(_cfg["Devices:CashDrawer:PulseOnMs"], out var pon) ? pon : 100;
        var pulseOff = int.TryParse(_cfg["Devices:CashDrawer:PulseOffMs"], out var poff) ? poff : 100;

        using var sp = new System.IO.Ports.SerialPort(port, 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
        sp.Open();
        sp.Write(new byte[] { 0x1B, 0x70, 0x00, (byte)pulseOn, (byte)pulseOff }, 0, 5);
        sp.Close();
    }
}
