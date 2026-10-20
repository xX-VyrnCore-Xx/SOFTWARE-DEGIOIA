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
    public void LegacyChoicesMoveToLightExactlyOnce()
    {
        foreach (var legacy in new[] { "system", "glass" })
        {
            var (dir, file, marker) = Setup();
            try
            {
                File.WriteAllText(file, legacy);
                Assert.Equal(ThemeMode.Light, ThemeStore.Load(file, marker));
                Assert.Equal("light", File.ReadAllText(file));
                Assert.True(File.Exists(marker));

                // Dopo il passaggio, scegliere di nuovo «Vetro» o «Come Windows» viene rispettato.
                File.WriteAllText(file, legacy);
                Assert.Equal(legacy == "glass" ? ThemeMode.Glass : ThemeMode.System, ThemeStore.Load(file, marker));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("light", "Light")]
    [InlineData("dark", "Dark")]
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
    public void MissingFileStartsWithLight()
    {
        var (dir, file, marker) = Setup();
        try
        {
            Assert.Equal(ThemeMode.Light, ThemeStore.Load(file, marker));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
