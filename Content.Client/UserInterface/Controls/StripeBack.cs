using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls
{
    [Virtual]
    public partial class StripeBack : Container // Starlight
    {
        public float PadSize { get; set; } = 4;
        public float EdgeSize { get; set; } = 2;
        public Color EdgeColor { get; set; } = Color.FromHex("#525252ff");

        private bool _hasTopEdge = true;
        private bool _hasBottomEdge = true;
        private bool _hasMargins = true;

        public const string StylePropertyBackground = "background";

        public bool HasTopEdge
        {
            get => _hasTopEdge;
            set
            {
                _hasTopEdge = value;
                InvalidateMeasure();
            }
        }

        public bool HasBottomEdge
        {
            get => _hasBottomEdge;
            set
            {
                _hasBottomEdge = value;
                InvalidateMeasure();
            }
        }

        public bool HasMargins
        {
            get => _hasMargins;
            set
            {
                _hasMargins = value;
                InvalidateMeasure();
            }
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var padSize = HasMargins ? MathF.Max(0f, PadSize) : 0f; // Starlight
            var edgeSize = MathF.Max(0f, EdgeSize); // Starlight
            var padSizeTotal = 0f;

            if (HasBottomEdge)
                padSizeTotal += padSize + edgeSize; // Starlight: because of a forced error with case
            if (HasTopEdge)
                padSizeTotal += padSize + edgeSize; // Starlight: same

            var size = Vector2.Zero;

            availableSize.Y = MathF.Max(0f, availableSize.Y - padSizeTotal); // Starlight

            foreach (var child in Children)
            {
                child.Measure(availableSize);
                size = Vector2.Max(size, child.DesiredSize);
            }

            return size + new Vector2(0, padSizeTotal);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            GetVerticalEdgeInsets(finalSize.Y, 1f, out var topInset, out var bottomInset); // Starlight
            var box = new UIBox2(0, topInset, finalSize.X, finalSize.Y - bottomInset); // Starlight

            foreach (var child in Children)
            {
                child.Arrange(box);
            }

            return finalSize;
        }


        protected override void Draw(DrawingHandleScreen handle)
        {
            UIBox2 centerBox = PixelSizeBox;

            var padSize = HasMargins ? MathF.Max(0f, PadSize) : 0f; // Starlight
            GetVerticalEdgeInsets(PixelHeight, UIScale, out var topInset, out var bottomInset); // Starlight

            if (HasTopEdge)
            {
                centerBox += (0, topInset, 0, 0); // Starlight
                var topEdgeTop = MathF.Min(padSize * UIScale, centerBox.Top); // Starlight
                if (PixelWidth > 0 && centerBox.Top > topEdgeTop) // Starlight
                    handle.DrawRect(new UIBox2(0, topEdgeTop, PixelWidth, centerBox.Top), EdgeColor); // Starlight
            }

            if (HasBottomEdge)
            {
                centerBox += (0, 0, 0, -bottomInset); // Starlight
                var bottomEdgeBottom = MathF.Max(centerBox.Bottom, PixelHeight - padSize * UIScale); // Starlight
                if (PixelWidth > 0 && bottomEdgeBottom > centerBox.Bottom) // Starlight
                    handle.DrawRect(new UIBox2(0, centerBox.Bottom, PixelWidth, bottomEdgeBottom), EdgeColor); // Starlight
            }

            if (centerBox.Width > 0 && centerBox.Height > 0) // Starlight
                GetActualStyleBox()?.Draw(handle, centerBox, UIScale); // Starlight
        }

        private StyleBox? GetActualStyleBox()
        {
            return TryGetStyleProperty(StylePropertyBackground, out StyleBox? box) ? box : null;
        }
    }
}
