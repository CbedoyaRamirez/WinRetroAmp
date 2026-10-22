// Generates the original app icon and a .wsz copy of the default skin from code.
// Usage: dotnet run --project tools/GenerateAssets -- <repo root>
using System.IO;
using WinRetroAmp.Core.Skins;
using WinRetroAmp.Services;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string assets = Path.Combine(root, "src", "WinRetroAmp", "Assets");
        Directory.CreateDirectory(assets);
        AppIcon.WriteIco(Path.Combine(assets, "app.ico"), 16, 24, 32, 48, 64, 256);
        SkinLoader.ExportDefault(Path.Combine(root, "skins", "WinRetroAmp-Graphite.wsz"));
        Console.WriteLine($"Assets written to {assets} and {Path.Combine(root, "skins")}");
    }
}
