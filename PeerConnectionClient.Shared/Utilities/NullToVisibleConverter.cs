﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace PeerConnectionClient.Utilities
{
    class NullToVisibleConverter : IValueConverter
    {
        public bool Negated { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
}
