using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Samba.Presentation.Common.Services;

namespace Samba.Presentation
{
    public class ToastItem
    {
        public string Text { get; set; }
        public ToastType Type { get; set; }
    }

    public class ToastTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value is ToastType ? (ToastType)value : ToastType.Info;
            var key = "PosLiteInfoBrush";
            switch (type)
            {
                case ToastType.Success: key = "PosLiteSuccessBrush"; break;
                case ToastType.Warning: key = "PosLiteWarningBrush"; break;
                case ToastType.Error: key = "PosLiteDangerBrush"; break;
            }
            var brush = Application.Current.TryFindResource(key) as Brush;
            return brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public partial class ToastHostControl : UserControl
    {
        private readonly ObservableCollection<ToastItem> _items = new ObservableCollection<ToastItem>();

        public ToastHostControl()
        {
            InitializeComponent();
            ToastList.ItemsSource = _items;
            ToastService.ShowRequested += AddToast;
            Unloaded += (s, e) => ToastService.ShowRequested -= AddToast;
        }

        private void AddToast(ToastArgs args)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<ToastArgs>(AddToast), args);
                return;
            }

            var item = new ToastItem { Text = args.Message, Type = args.Type };
            _items.Add(item);

            var container = ToastList.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            if (container != null)
            {
                container.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(args.DurationMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                DismissToast(item);
            };
            timer.Start();
        }

        private void DismissToast(ToastItem item)
        {
            var container = ToastList.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            if (container == null)
            {
                _items.Remove(item);
                return;
            }

            var animation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            animation.Completed += (s, e) => _items.Remove(item);
            container.BeginAnimation(OpacityProperty, animation);
        }
    }
}