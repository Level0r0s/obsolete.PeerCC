using Windows.UI.Xaml;

namespace PeerConnectionClient.Controls
{
    public sealed partial class ErrorControl
    {
        public ErrorControl()
        {
            InitializeComponent();
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
            ((ErrorControl)d).MyPresenter.Content = e.NewValue as UIElement;
        }
    }
}
