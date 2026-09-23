using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Translation.Fomod;
using Xunit;

namespace Wabbajack.Translation.Test;

public class SimpleFomodInstallerTests
{
    private const string ChineseFixShape = """
        <config xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="http://qconsulting.ca/fo3/ModConfig5.0.xsd">
          <moduleName>Fix</moduleName>
          <requiredInstallFiles>
            <folder source="00-General\Strings" destination="Strings" />
            <file source="00-General\interface\Translate_cn.txt" destination="interface\Translate_cn.txt" />
          </requiredInstallFiles>
          <installSteps order="Explicit">
            <installStep name="one">
              <optionalFileGroups order="Explicit">
                <group name="one" type="SelectExactlyOne">
                  <plugins order="Explicit">
                    <plugin name="vanilla">
                      <files>
                        <file source="01-Vanilla\Strings\Fallout4_cn.STRINGS" destination="Strings\Fallout4_cn.STRINGS" priority="0" />
                      </files>
                      <typeDescriptor><type name="Recommended"/></typeDescriptor>
                    </plugin>
                  </plugins>
                </group>
              </optionalFileGroups>
            </installStep>
            <installStep name="fonts">
              <optionalFileGroups order="Explicit">
                <group name="fonts" type="SelectExactlyOne">
                  <plugins order="Explicit">
                    <plugin name="full">
                      <files><folder source="97-Full Font\Data\interface" destination="interface" priority="0" /></files>
                      <typeDescriptor><type name="Optional"/></typeDescriptor>
                    </plugin>
                    <plugin name="simple">
                      <files><folder source="98-Simple Font\Data\interface" destination="interface" priority="0" /></files>
                      <typeDescriptor><type name="Optional"/></typeDescriptor>
                    </plugin>
                    <plugin name="none">
                      <typeDescriptor><type name="Optional"/></typeDescriptor>
                    </plugin>
                  </plugins>
                </group>
              </optionalFileGroups>
            </installStep>
          </installSteps>
        </config>
        """;

    [Fact]
    public void TakesRequiredFilesTheRecommendedOptionAndTheFirstOfAnExactlyOneGroup()
    {
        var plan = SimpleFomodInstaller.Plan(ChineseFixShape);
        Assert.Equal(
        [
            new FomodCopy(@"00-General\Strings", "Strings", true),
            new FomodCopy(@"00-General\interface\Translate_cn.txt", @"interface\Translate_cn.txt", false),
            new FomodCopy(@"01-Vanilla\Strings\Fallout4_cn.STRINGS", @"Strings\Fallout4_cn.STRINGS", false),
            new FomodCopy(@"97-Full Font\Data\interface", "interface", true)
        ], plan);
    }

    [Fact]
    public void RemovesAnIniKeyEverywhereItAppears()
    {
        var text = ProfileText.Parse("[General]\r\nsLanguage=cn\r\nsValidNameCharsFile=Interface\\FontConfig_cn.txt\r\n[Fonts]\r\nsFontConfigFile=Interface\\FontConfig_cn.txt\r\n");
        text.RemoveIniValue("sFontConfigFile");
        text.RemoveIniValue("svalidnamecharsfile");
        Assert.Equal("[General]\r\nsLanguage=cn\r\n[Fonts]\r\n", text.ToString());
    }

    [Theory]
    [InlineData("Fallout4", "cn", "schinese")]
    [InlineData("Fallout4", "Simplified Chinese", "schinese")]
    [InlineData("Fallout4", "ptbr", "brazilian")]
    [InlineData("Fallout4", "russian", null)]
    [InlineData("SkyrimSpecialEdition", "GERMAN", "german")]
    [InlineData("SkyrimSpecialEdition", "russian", "russian")]
    [InlineData("SkyrimSpecialEdition", "brazilian", "brazilian")]
    [InlineData("SkyrimSpecialEdition", "klingon", null)]
    public void FindsLanguagesPerGame(string game, string query, string? id) =>
        Assert.Equal(id, TranslationGames.For(Enum.Parse<Wabbajack.DTOs.Game>(game))!.Find(query)?.Id);

    [Fact]
    public void SkyrimLanguagesKeepEnglishSlotsForCommunityPacks()
    {
        var skyrim = TranslationGames.SkyrimSpecialEdition;
        Assert.Equal("ENGLISH", skyrim.Find("brazilian")!.SLanguage);
        Assert.Equal("ENGLISH", skyrim.Find("schinese")!.SLanguage);
        Assert.IsType<NexusVoices>(skyrim.Find("brazilian")!.Voices);
        Assert.Null(skyrim.Find("schinese")!.Voices);
        Assert.Equal("Skyrim - Voices_ru0.bsa", skyrim.VoicesArchive("ru"));
    }

    [Fact]
    public async Task CopiesAnArchiveWithoutAFomodUnwrappingDataAndSkippingReadmes()
    {
        var root = Path.Combine(Path.GetTempPath(), "wj-fomod-" + Guid.NewGuid().ToString("N")).ToAbsolutePath();
        try
        {
            var extracted = root.Combine("x");
            extracted.Combine("Data", "strings").CreateDirectory();
            await extracted.Combine("Data", "strings", "Skyrim_english.STRINGS").WriteAllTextAsync("s");
            await extracted.Combine("Data", "readme.jpg").WriteAllTextAsync("i");
            await extracted.Combine("INSTALACAO MANUAL.jpg").WriteAllTextAsync("i");
            var written = await SimpleFomodInstaller.Install(extracted, root.Combine("out"), CancellationToken.None);
            Assert.Equal(1, written);
            Assert.True(root.Combine("out", "strings", "Skyrim_english.STRINGS").FileExists());
        }
        finally
        {
            Directory.Delete(root.ToString(), true);
        }
    }
}
