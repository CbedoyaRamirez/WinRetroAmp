using WinRetroAmp.Core.Skins;
using Xunit;

namespace WinRetroAmp.Core.Tests;

public class BuiltInSkinTests
{
    [Theory]
    [InlineData("Amber")]
    [InlineData("Arctic")]
    [InlineData("Rose")]
    [InlineData("Copper")]
    [InlineData("Emerald")]
    [InlineData("Violet")]
    public void Edition_loads_by_saved_identifier_and_round_trips_all_artwork(string name)
    {
        var skin = SkinLoader.Load("builtin:" + name);
        Assert.Equal(name, skin.Name);
        Assert.NotEqual(SkinLoader.Default.Playlist.Normal, skin.Playlist.Normal);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wsz");
        try
        {
            SkinLoader.Export(skin, path);
            var loaded = SkinLoader.Load(path);
            foreach (string sheet in SpriteMap.Sheets.All)
            {
                Assert.NotSame(SkinLoader.Default[sheet], skin[sheet]);
                Assert.Equal(skin[sheet].Pixels, loaded[sheet].Pixels);
            }
            Assert.Equal(skin.Playlist, loaded.Playlist);
            Assert.Equal(skin.VisColors, loaded.VisColors);
        }
        finally { File.Delete(path); }
    }
}
