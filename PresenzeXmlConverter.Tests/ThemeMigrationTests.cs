namespace PresenzeXmlConverter.Tests;

public class ThemeMigrationTests
{
    private static (string Dir, string File, string Marker) Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "presenze-theme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return (dir, Path.Combine(dir, "theme.txt"), Path.Combine(dir, "marker.txt"));
    }

    [Fact]
    public void LegacySystemChoiceMovesToGlassExactlyOnce()
    {
        var (dir, file, marker) = Setup();
        try
        {
            File.WriteAllText(file, "system");
            Assert.Equal(ThemeMode.Glass, ThemeStore.Load(file, marker));
            Assert.Equal("glass", File.ReadAllText(file));
            Assert.True(File.Exists(marker));

            // Dopo il passaggio, scegliere di nuovo «Come Windows» viene rispettato.
            File.WriteAllText(file, "system");
            Assert.Equal(ThemeMode.System, ThemeStore.Load(file, marker));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("light", "Light")]
    [InlineData("dark", "Dark")]
    [InlineData("glass", "Glass")]
    public void ExplicitChoicesAreNeverOverridden(string text, string expected)
    {
        var (dir, file, marker) = Setup();
        try
        {
            File.WriteAllText(file, text);
            Assert.Equal(expected, ThemeStore.Load(file, marker).ToString());
            Assert.False(File.Exists(marker));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MissingFileStartsWithGlass()
    {
        var (dir, file, marker) = Setup();
        try
        {
            Assert.Equal(ThemeMode.Glass, ThemeStore.Load(file, marker));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
