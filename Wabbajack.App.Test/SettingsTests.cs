using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Test
{
    public class SettingsTests
    {
        [Fact]
        public async Task CanSaveAndLoadSettings()
        {
            var (settings, loadedSettings) = await MainSettings.TryLoadTypicalSettings();

            if (settings == null || !loadedSettings)
            {
                settings = new MainSettings();
            }

            await MainSettings.SaveSettings(settings);
            
            Assert.True((await MainSettings.TryLoadTypicalSettings()).loaded);
            
        }
    }
}
