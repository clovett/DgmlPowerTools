using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LovettSoftware.DgmlPowerTools
{
    public sealed partial class EditableTextBlock : UserControl
    {
        Brush defaultForeground;

        public EditableTextBlock()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.defaultForeground = this.Foreground; // save the foreground brush.
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Label.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(EditableTextBlock), new PropertyMetadata(null, new PropertyChangedCallback(OnLabelChanged)));

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EditableTextBlock)d).OnLabelChanged();
        }

        void OnLabelChanged()
        {
            LabelTextBlock.Text = LabelEditBox.Text = this.Label;
            if (LabelChanged != null)
            {
                LabelChanged(this, EventArgs.Empty);
            }
            if (this.Label.StartsWith("<"))
            {
                this.Foreground = Brushes.Gray;
            }
            else if (this.defaultForeground != null)
            {
                this.Foreground = this.defaultForeground;
            }
        }

        public event EventHandler LabelChanged;


        private void PageTitleEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitEdit();
        }

        public void CommitEdit()
        {
            LabelTextBlock.Visibility = System.Windows.Visibility.Visible;
            LabelEditBox.Visibility = System.Windows.Visibility.Collapsed;
            Label = LabelEditBox.Text;
        }

        private void PageTitleEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CommitEdit();
            }
        }

        private void OnBorderPointerPressed(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BeginEdit();
            e.Handled = true;
        }

        public void BeginEdit()
        {
            LabelTextBlock.Visibility = System.Windows.Visibility.Collapsed;
            LabelEditBox.Visibility = System.Windows.Visibility.Visible;

            Dispatcher.Invoke(new Action(() =>
            {
                LabelEditBox.SelectAll();
                LabelEditBox.Focus();
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

    }
}
