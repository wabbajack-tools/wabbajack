using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Test
{
    public class SettingsTests
    {
        [Fact]
        public async Task CanSaveAndLoadSettings()
        {
            MainSettings.TryLoadTypicalSettings(out var settings);

            if (settings == null)
            {
                settings = new MainSettings();
            }

            MainSettings.SaveSettings(settings);
            
            Assert.True(MainSettings.TryLoadTypicalSettings(out settings));
            
        }
    }
}
