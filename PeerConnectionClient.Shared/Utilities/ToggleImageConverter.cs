using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.UI.Xaml.Data;

namespace PeerConnectionClient.Utilities
{
    public class ToggleImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isChecked = value as bool?;
            var imageName = parameter as string;
            Debug.Assert(isChecked != null);
            Debug.Assert(imageName != null);
            string noPrefix = (isChecked == true) ? "" : "No ";
            string pathBeginning = "/Assets/";
#if WINDOWS_PHONE_APP
            pathBeginning = "ms-appx:///Assets/";
#endif
            return string.Format("{0}{1}{2}", pathBeginning, noPrefix, imageName);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
