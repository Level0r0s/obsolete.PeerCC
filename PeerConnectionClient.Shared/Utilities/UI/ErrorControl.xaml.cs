using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace PeerConnectionClient.Controls
{
  public sealed partial class ErrorControl : UserControl
  {
    public ErrorControl()
    {
      this.InitializeComponent();
    }
    public UIElement InnerContent
    {
      get { return (UIElement)GetValue(InnerContentProperty); }
      set { SetValue(InnerContentProperty, value); }
    }
    public static readonly DependencyProperty InnerContentProperty =
            DependencyProperty.Register("InnerContent", typeof(UIElement),
            typeof(ErrorControl), new PropertyMetadata(null, InnerContentChanged));
    private static void InnerContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      (d as ErrorControl).MyPresenter.Content = e.NewValue as UIElement;
    }
  }
}
