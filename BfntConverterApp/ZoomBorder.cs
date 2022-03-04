using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BfntConverterApp
{
    // https://stackoverflow.com/a/6782715
    public class ZoomBorder : Border
    {
        private UIElement _child = null!;
        private Point _origin;
        private Point _start;

        private static TranslateTransform GetTranslateTransform(UIElement element)
        {
            return (TranslateTransform)((TransformGroup)element.RenderTransform)
              .Children.First(tr => tr is TranslateTransform);
        }

        private static ScaleTransform GetScaleTransform(UIElement element)
        {
            return (ScaleTransform)((TransformGroup)element.RenderTransform)
              .Children.First(tr => tr is ScaleTransform);
        }

        public override UIElement Child
        {
            get => base.Child;
            set
            {
                if (value != Child)
                    Initialize(value);
                base.Child = value;
            }
        }

        public void Initialize(UIElement element)
        {
            _child = element;
            
            var group = new TransformGroup();
            
            var st = new ScaleTransform();
            group.Children.Add(st);
            
            var tt = new TranslateTransform();
            group.Children.Add(tt);
            
            _child.RenderTransform = group;
            _child.RenderTransformOrigin = new Point(0.0, 0.0);
            MouseWheel += child_MouseWheel;
            MouseLeftButtonDown += Child_MouseLeftButtonDown;
            MouseLeftButtonUp += Child_MouseLeftButtonUp;
            MouseMove += Child_MouseMove;
        }

        public void Reset()
        {
            // reset zoom
            var st = GetScaleTransform(_child);
            st.ScaleX = 1.0;
            st.ScaleY = 1.0;

            // reset pan
            var tt = GetTranslateTransform(_child);
            tt.X = 0.0;
            tt.Y = 0.0;
        }

        #region Child Events

        private void child_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var st = GetScaleTransform(_child);
            var tt = GetTranslateTransform(_child);

            var zoom = e.Delta > 0 ? .2 : -.2;
           
            var relative = e.GetPosition(_child);

            if (relative.X < 0) relative.X = 0;
            if (relative.Y < 0) relative.Y = 0;
            if (relative.X > ((FrameworkElement)_child).ActualWidth) relative.X = ((FrameworkElement)_child).ActualWidth;
            if (relative.Y > ((FrameworkElement)_child).ActualWidth) relative.Y = ((FrameworkElement)_child).ActualHeight;


            var absoluteX = relative.X * st.ScaleX + tt.X;
            var absoluteY = relative.Y * st.ScaleY + tt.Y;

            var zoomCorrected = zoom * st.ScaleX;
            st.ScaleX += zoomCorrected;
            st.ScaleY += zoomCorrected;

            if (st.ScaleX < 1) st.ScaleX = 1;
            if (st.ScaleY < 1) st.ScaleY = 1;
            if (st.ScaleX > 8) st.ScaleX = 8;
            if (st.ScaleY > 8) st.ScaleY = 8;

            tt.X = absoluteX - relative.X * st.ScaleX;
            tt.Y = absoluteY - relative.Y * st.ScaleY;
        }

        private void Child_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((e.ChangedButton == MouseButton.Left && e.ClickCount == 1))
            {
                var tt = GetTranslateTransform(_child);
                _start = e.GetPosition(this);
                _origin = new Point(tt.X, tt.Y);
                Cursor = Cursors.Hand;
                _child.CaptureMouse();
            }

            if ((e.ChangedButton == MouseButton.Left && e.ClickCount == 2))
            {
                Reset();
            }

        }

        private void Child_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _child.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }
        

        private void Child_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_child.IsMouseCaptured) return;
            
            var tt = GetTranslateTransform(_child);
            var v = _start - e.GetPosition(this);
            tt.X = _origin.X - v.X;
            tt.Y = _origin.Y - v.Y;
        }

        #endregion
    }
}
