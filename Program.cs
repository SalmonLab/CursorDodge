using System.Windows.Forms;

namespace CursorDodge;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new CursorDodgeContext());
    }
}
