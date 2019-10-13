using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LovettSoftware.DgmlPowerTools
{
    /// <summary>
    /// Interaction logic for MyControl.xaml
    /// </summary>
    public partial class FilterView : UserControl
    {
        IGraphDocumentWindowPane graphDocumentWindowPane;
        GraphControl graphControl;
        Graph graph;
        IServiceProvider serviceProvider;
        SelectionTracker tracker;
        GroupViewModel viewModel = new GroupViewModel();

        public FilterView()
        {
            InitializeComponent();

            StartWatchingItems();

            viewModel.AddNewItem();

            FilterList.ItemsSource = viewModel.Items;
        }

        void StopWatchingItems()
        {
            foreach (var item in viewModel.Items)
            {
                item.PropertyChanged -= OnPropertyChanged;
            }
            viewModel.Items.CollectionChanged -= Items_CollectionChanged;
        }

        void StartWatchingItems()
        {
            viewModel.Items.CollectionChanged += Items_CollectionChanged;
            foreach (var item in viewModel.Items)
            {
                item.PropertyChanged -= OnPropertyChanged;
                item.PropertyChanged += OnPropertyChanged;
            }
        }

        void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (GroupItemViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnPropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (GroupItemViewModel item in e.NewItems)
                {
                    item.PropertyChanged -= OnPropertyChanged;
                    item.PropertyChanged += OnPropertyChanged;
                }
            }
        }

        void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Label")
            {
                GroupItemViewModel item = (GroupItemViewModel)sender;
                if (item.Label != GroupViewModel.NewItemCaption)
                {
                    // See if there is any placeholder in the list
                    if (!(from x in viewModel.Items where x.Label == GroupViewModel.NewItemCaption select x).Any())
                    {
                        // then add one.
                        viewModel.AddNewItem();
                    }
                }
            }
        }

        internal void OnInitialized(IServiceProvider sp)
        {
            serviceProvider = sp;
            tracker = new SelectionTracker();
            tracker.ActiveWindowChanged += OnActiveWindowChanged;
            tracker.Initialize(sp);
        }

        private void OnActiveWindowChanged(object sender, ActiveWindowChangedEventArgs e)
        {
            graphDocumentWindowPane = e.Window;
            GraphControl graphControl = null;

            if (graphDocumentWindowPane != null)
            {
                WindowPane windowPane = graphDocumentWindowPane as WindowPane;
                if (windowPane != null)
                {
                    graphControl = windowPane.Content as GraphControl;
                }
            }

            if (graphControl != this.graphControl)
            {
                OnGraphControlChanged(graphControl);
            }
        }

        private void OnGraphControlChanged(GraphControl graphControl)
        {
            this.graphControl = graphControl;
            Graph graph = null;

            if (graphControl != null)
            {
                graph = graphControl.Graph;
            }
            if (graph != this.graph)
            {
                OnGraphChanged(graph);
            }
        }

        private void OnGraphChanged(Graph graph)
        {
            this.graph = graph;
            if (this.graph != null)
            {
                this.graph.AddSchema(GroupViewModelSchema.Schema);
                StopWatchingItems();
                viewModel.SetGraph(graph);
                StartWatchingItems();
                viewModel.AddNewItem();
            }
        }

        internal void OnClose()
        {
            serviceProvider = null;
            if (tracker != null)
            {
                tracker.ActiveWindowChanged -= OnActiveWindowChanged;
                tracker = null;
            }
        }

        private void OnApplyGroups(object sender, RoutedEventArgs e)
        {
            if (this.graph == null)
            {
                return;
            }

            viewModel.ApplyGroups();

            // force full layout since it looks prettier that way.
            GraphControl control = this.graphControl;
            if (control != null && control.Diagram != null)
            {
                control.Diagram.RedoLayout();
            }

        }

        private void OnRemoveGroups(object sender, RoutedEventArgs e)
        {
            if (this.graph == null)
            {
                return;
            }

            viewModel.RemoveGroups();

            // force full layout since it looks prettier that way.
            GraphControl control = this.graphControl;
            if (control != null && control.Diagram != null)
            {
                control.Diagram.RedoLayout();
            }

        }

        private void OnClearList(object sender, RoutedEventArgs e)
        {
            viewModel.RemoveGroups();
            viewModel.Items.Clear();
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var item = FilterList.SelectedItem as GroupItemViewModel;
            if (item != null)
            {
                this.viewModel.RemoveItem(item);
            }
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                // user is editing...so +/- could be valid search strings.
                return;
            }
            var item = FilterList.SelectedItem as GroupItemViewModel;
            if (item != null)
            {
                int index = FilterList.SelectedIndex;

                if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    e.Handled = true;

                    if (index > 0)
                    {
                        StopWatchingItems();
                        viewModel.Items.Remove(item);
                        viewModel.Items.Insert(index - 1, item);
                        FilterList.SelectedItem = item;
                        StartWatchingItems();
                    }
                }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    e.Handled = true;
                    if (index < viewModel.Items.Count - 1)
                    {
                        StopWatchingItems();
                        viewModel.Items.Remove(item);
                        viewModel.Items.Insert(index + 1, item);
                        FilterList.SelectedItem = item;
                        StartWatchingItems();
                    }
                }
                else if (e.Key == Key.Insert)
                {
                    e.Handled = true;
                    StopWatchingItems();
                    viewModel.AddNewItem();
                    item = viewModel.Items.Last();
                    viewModel.Items.Remove(item);
                    viewModel.Items.Insert(index, item);
                    FilterList.SelectedItem = item;
                    StartWatchingItems();
                }
            }
        }

        private void OnLabelKeyDown(object sender, KeyEventArgs e)
        {
            EditableTextBlock editable = (EditableTextBlock)sender;
            if (e.Key == Key.Tab)
            {
                Grid parent = editable.Parent as Grid;
                int i = parent.Children.IndexOf(editable);
                if (parent.Children.Count >= i + 1)
                {
                    EditableTextBlock next = (EditableTextBlock)parent.Children[i + 1];
                    next.BeginEdit();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                editable.CommitEdit();
                Dispatcher.BeginInvoke(new System.Action(() => { FilterList.Focus(); }));
                e.Handled = true;
            }
        }

        private void OnExpressionKeyDown(object sender, KeyEventArgs e)
        {
            EditableTextBlock editable = (EditableTextBlock)sender;
            if (e.Key == Key.Tab && (e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift)))
            {
                Grid parent = editable.Parent as Grid;
                int i = parent.Children.IndexOf(editable);
                if (i > 0)
                {
                    EditableTextBlock next = (EditableTextBlock)parent.Children[i - 1];
                    next.BeginEdit();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                editable.CommitEdit();
                Dispatcher.BeginInvoke(new System.Action(() => { FilterList.Focus(); }));
                e.Handled = true;
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            CloseBox box = sender as CloseBox;
            GroupItemViewModel model = box.DataContext as GroupItemViewModel;
            if (model != null)
            {
                viewModel.Items.Remove(model);
            }
        }

        private void OnItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems != null)
            {
                foreach (GroupItemViewModel model in e.RemovedItems)
                {
                    model.IsSelected = false;
                }
            }
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                GroupItemViewModel model = e.AddedItems[0] as GroupItemViewModel;
                foreach (var item in viewModel.Items)
                {
                    if (item != model)
                    {
                        item.IsSelected = false;
                    }
                }
                model.IsSelected = true;
            }
        }

        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            FilterList.ContextMenu.IsOpen = true;
        }

        private void OnSaveGroups(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog fd = new Microsoft.Win32.SaveFileDialog();
            fd.CheckPathExists = true;
            fd.AddExtension = true;
            fd.Filter = "XML File (.xml)|*.xml";

            if (fd.ShowDialog(Application.Current.MainWindow) == true)
            {
                try
                {
                    string filename = fd.FileName;
                    viewModel.Save(filename);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Saving Group Patterns: " + ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void OnLoadGroups(object sender, RoutedEventArgs e)
        {
            // Restore SQL CE database from a backup.
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = "Restore Database";
            fd.Filter = "XML File (.xml)|*.xml";
            fd.CheckFileExists = true;
            if (fd.ShowDialog(Application.Current.MainWindow) == true)
            {
                try
                {
                    StopWatchingItems();
                    viewModel.Load(fd.FileName);
                    StartWatchingItems();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Saving Group Patterns: " + ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnLabelChanged(object sender, EventArgs e)
        {
            viewModel.CheckNewItem();
        }

        private void OnExpressionChanged(object sender, EventArgs e)
        {
            viewModel.CheckNewItem();
        }

        private void OnLabelGotFocus(object sender, RoutedEventArgs e)
        {
            EditableTextBlock box = (EditableTextBlock)sender;
            var item = box.DataContext as GroupItemViewModel;
            if (item != null)
            {
                this.FilterList.SelectedItem = item;
            }
        }
    }
}