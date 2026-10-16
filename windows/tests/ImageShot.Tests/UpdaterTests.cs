using System;
using ImageShot;
using Xunit;

public class UpdaterTests
{
    const string Sample = "{\"url\":\"x\",\"html_url\":\"https://github.com/Steensgithub/Imageshot/releases/tag/v2.1.0\"," +
        "\"tag_name\":\"v2.1.0\",\"assets\":[{\"name\":\"ImageShot-2.1.0-portable.zip\",\"browser_download_url\":\"https://github.com/Steensgithub/Imageshot/releases/download/v2.1.0/ImageShot-2.1.0-portable.zip\"}," +
        "{\"name\":\"ImageShot-Setup-2.1.0.exe\",\"browser_download_url\":\"https://github.com/Steensgithub/Imageshot/releases/download/v2.1.0/ImageShot-Setup-2.1.0.exe\"}]}";

    [Fact]
    public void ParsesVersionPageAndInstaller()
    {
        Updater.Release r = Updater.Parse(Sample);
        Assert.Equal(new Version(2, 1, 0), r.Version);
        Assert.EndsWith("/releases/tag/v2.1.0", r.Page);
        Assert.EndsWith("ImageShot-Setup-2.1.0.exe", r.Installer);
    }

    [Fact]
    public void IgnoresGarbage()
    {
        Assert.Null(Updater.Parse("{\"message\":\"Not Found\"}"));
        Assert.Null(Updater.Parse(""));
    }

    [Fact]
    public void NewerIsDetectedByVersionCompare()
    {
        Assert.True(Updater.Parse(Sample).Version > new Version(2, 0, 9));
        Assert.False(Updater.Parse(Sample).Version > new Version(2, 1, 0));
    }
}
