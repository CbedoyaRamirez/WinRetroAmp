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
        Directory.CreateDirectory(Path.Combine(root, "skins"));
        SkinLoader.ExportDefault(Path.Combine(root, "skins", "WinRetroAmp-Graphite.wsz"));
        foreach (var edition in BuiltInSkins.All)
            SkinLoader.Export(BuiltInSkins.Create(edition.Path), Path.Combine(root, "skins", $"WinRetroAmp-{edition.Name}.wsz"));
        Console.WriteLine($"Assets written to {assets} and {Path.Combine(root, "skins")}");
    }
}
