using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BfntConverterApp
{
    // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox
    public class SelectTextOnFocus : DependencyObject
    {
        public static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached(
            "Active",
            typeof(bool),
            typeof(SelectTextOnFocus),
            new PropertyMetadata(false, ActivePropertyChanged));

        private static void ActivePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;
            if ((e.NewValue as bool?).GetValueOrDefault(false))
            {
                textBox.GotKeyboardFocus += OnKeyboardFocusSelectText;
                textBox.PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
            }
            else
            {
                textBox.GotKeyboardFocus -= OnKeyboardFocusSelectText;
                textBox.PreviewMouseLeftButtonDown -= OnMouseLeftButtonDown;
            }
        }

        private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dependencyObject = GetParentFromVisualTree(e.OriginalSource);
            var textBox = (TextBox)dependencyObject;
            if (textBox.IsKeyboardFocusWithin)
                return;
            textBox.Focus();
            e.Handled = true;
        }

        private static DependencyObject GetParentFromVisualTree(object source)
        {
            DependencyObject parent = source as UIElement;
            while (parent != null && parent is not TextBox)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent;
        }

        private static void OnKeyboardFocusSelectText(object sender, KeyboardFocusChangedEventArgs e)
        {
            var textBox = e.OriginalSource as TextBox;
            textBox?.SelectAll();
        }

        [AttachedPropertyBrowsableForChildrenAttribute(IncludeDescendants = false)]
        [AttachedPropertyBrowsableForType(typeof(TextBox))]
        public static bool GetActive(DependencyObject @object)
        {
            return (bool)@object.GetValue(ActiveProperty);
        }

        public static void SetActive(DependencyObject @object, bool value)
        {
            @object.SetValue(ActiveProperty, value);
        }
    }
}
