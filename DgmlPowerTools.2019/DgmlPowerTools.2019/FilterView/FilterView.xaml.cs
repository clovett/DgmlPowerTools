using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Shell;
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

            StartWatchingLastItem();

            viewModel.AddNewItem();

            FilterList.ItemsSource = viewModel.Items;
            EnableButtons();
        }

        void StopWatchingLastItem()
        {
            WatchLastItem(null);
            viewModel.Items.CollectionChanged -= Items_CollectionChanged;
        }

        void StartWatchingLastItem()
        {
            viewModel.Items.CollectionChanged += Items_CollectionChanged;
        }

        void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                int count = viewModel.Items.Count;
                if (e.NewStartingIndex + e.NewItems.Count == count)
                {
                    GroupItemViewModel item = (GroupItemViewModel)e.NewItems[e.NewItems.Count - 1];
                    WatchLastItem(item);
                }
            }
        }

        GroupItemViewModel lastItem;

        private void WatchLastItem(GroupItemViewModel item)
        {
            if (lastItem != null)
            {
                lastItem.PropertyChanged -= OnPropertyChanged;
            }
            lastItem = item;
            if (lastItem != null)
            {
                lastItem.PropertyChanged += OnPropertyChanged;
                OnPropertyChanged(lastItem, new System.ComponentModel.PropertyChangedEventArgs("Label"));
            }
        }

        void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Label")
            {
                GroupItemViewModel item = (GroupItemViewModel)sender;
                if (item.Label != GroupViewModel.NewItemCaption)
                {
                    // then we need a new last item for the next new item.
                    viewModel.AddNewItem();
                }
                else if (item.Label == "")
                {
                    // keep watching - if it stays empty, and this is not the last item, then delete it.
                }
            }
        }

        private void EnableButtons()
        {
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
                StopWatchingLastItem();
                viewModel.SetGraph(graph);
                StartWatchingLastItem();
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

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            EnableButtons();
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            viewModel.RemoveGroups();
            viewModel.Items.Clear();
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            var item = FilterList.SelectedItem as GroupItemViewModel;
            if (item != null)
            {
                int index = FilterList.SelectedIndex;

                if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    e.Handled = true;

                    if (index > 0)
                    {
                        viewModel.Items.Remove(item);
                        viewModel.Items.Insert(index - 1, item);
                        FilterList.SelectedItem = item;
                    }
                }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    e.Handled = true;
                    if (index < viewModel.Items.Count - 1)
                    {
                        viewModel.Items.Remove(item);
                        viewModel.Items.Insert(index + 1, item);
                        FilterList.SelectedItem = item;
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnLabelKeyDown(object sender, KeyEventArgs e)
        {
            EditableTextBlock editable = (EditableTextBlock)sender;
            if (e.Key == Key.Tab)
            {
                StackPanel parent = editable.Parent as StackPanel;
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
                StackPanel parent = editable.Parent as StackPanel;
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
                Dispatcher.BeginInvoke(new System.Action(() => FilterList.Focus()));
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

        /// <summary>
        /// Track the list item selection so we can update the GroupItemViewModel state
        /// which is used to trigger CloseBox visibility.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
    }
}