using Windows.UI.Xaml;

namespace PeerConnectionClient.Controls
{
    public sealed partial class ErrorControl
    {
        /// <summary>
        /// Creates an ErrorControl instance.
        /// </summary>
        public ErrorControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Inner content property for error control element.
        /// </summary>
        public UIElement InnerContent
        {
            get { return (UIElement)GetValue(InnerContentProperty); }
            set { SetValue(InnerContentProperty, value); }
        }

        public static readonly DependencyProperty InnerContentProperty =
                DependencyProperty.Register("InnerContent", typeof(UIElement),
                typeof(ErrorControl), new PropertyMetadata(null, InnerContentChanged));

        /// <summary>
        /// Property changed event handler.
        /// </summary>
        /// <param name="d">Dependency object.</param>
        /// <param name="e">Details about DependencyPropertyChanged event.</param>
        private static void InnerContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ErrorControl)d).MyPresenter.Content = e.NewValue as UIElement;
        }
    }
}
