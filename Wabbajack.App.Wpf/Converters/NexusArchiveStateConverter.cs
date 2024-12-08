using System;
using System.Globalization;
using System.Windows.Data;
using Wabbajack.Common;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack
{
    public class NexusArchiveStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is Nexus nexus)
            {
                var nexusType = value.GetType();
                var nexusProperty = nexusType.GetProperty(parameter.ToString());
                return nexusProperty.GetValue(nexus);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
