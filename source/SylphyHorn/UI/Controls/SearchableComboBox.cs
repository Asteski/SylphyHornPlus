using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SylphyHorn.UI.Controls
{
    /// <summary>
    /// Pesquisa uma cópia visual da lista sem filtrar a fonte nem apagar a seleção persistida.
    /// O texto pesquisado nunca é enviado ao binding de SelectedValue.
    /// </summary>
    public class SearchableComboBox : ComboBox
    {
        private TextBox _search;
        private ListBox _results;

        public override void OnApplyTemplate()
        {
            if (_search != null) _search.TextChanged -= SearchChanged;
            if (_results != null)
            {
                _results.PreviewMouseLeftButtonUp -= SelectResult;
                _results.PreviewKeyDown -= ResultKeyDown;
            }
            base.OnApplyTemplate();
            _search = GetTemplateChild("PART_SearchBox") as TextBox;
            _results = GetTemplateChild("PART_SearchResults") as ListBox;
            if (_search != null) _search.TextChanged += SearchChanged;
            if (_results != null)
            {
                _results.PreviewMouseLeftButtonUp += SelectResult;
                _results.PreviewKeyDown += ResultKeyDown;
            }
        }

        protected override void OnDropDownOpened(EventArgs e)
        {
            base.OnDropDownOpened(e);
            if (_search == null || _results == null) return;
            _search.Clear();
            RefreshResults();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (IsDropDownOpen) _search.Focus();
            }));
        }

        private string Label(object item)
            => string.IsNullOrEmpty(DisplayMemberPath) ? Convert.ToString(item)
                : Convert.ToString(item?.GetType().GetProperty(DisplayMemberPath)?.GetValue(item));

        internal static bool Matches(string label, string query)
            => CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                (label ?? "").Normalize(NormalizationForm.FormC),
                (query ?? "").Normalize(NormalizationForm.FormC),
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

        private void SearchChanged(object sender, TextChangedEventArgs e) => RefreshResults();

        private void RefreshResults()
        {
            if (_results == null || _search == null) return;
            var comparer = System.Collections.Generic.Comparer<string>.Create((a, b) =>
                CultureInfo.CurrentCulture.CompareInfo.Compare(a.Normalize(NormalizationForm.FormC),
                    b.Normalize(NormalizationForm.FormC), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
            _results.ItemsSource = Items.Cast<object>().Where(item => Matches(Label(item), _search.Text))
                .OrderBy(Label, comparer).ToArray();
            _results.DisplayMemberPath = DisplayMemberPath;
        }

        private void CommitResult()
        {
            var selected = _results?.SelectedItem;
            if (selected == null || !Items.Contains(selected)) return;
            SetCurrentValue(SelectedItemProperty, selected);
            IsDropDownOpen = false;
            Focus();
        }

        private void SelectResult(object sender, MouseButtonEventArgs e)
        {
            // Cliques na barra de rolagem ou no espaço vazio não confirmam uma opção antiga.
            if (ItemsControl.ContainerFromElement(_results, e.OriginalSource as DependencyObject) is ListBoxItem)
            {
                CommitResult();
                e.Handled = true;
            }
        }

        private void ResultKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitResult(); e.Handled = true; }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (IsDropDownOpen && e.Key == Key.Escape)
            {
                IsDropDownOpen = false;
                Focus();
                e.Handled = true;
                return;
            }
            if (IsDropDownOpen && _search?.IsKeyboardFocusWithin == true && e.Key == Key.Down && _results.Items.Count > 0)
            {
                _results.SelectedIndex = 0;
                (_results.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
