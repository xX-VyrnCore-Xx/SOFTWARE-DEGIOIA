using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PresenzeXmlConverter;

/// <summary>
/// Disegno dei menù a comparsa (tasto destro, «Trasferisci», foglio, area di notifica): sfondo e bordo della
/// tavolozza attiva, voce evidenziata con angoli tondi, spunta disegnata con l'icona dell'app. Prima i menù
/// restavano grigio Windows anche con il tema scuro.
/// </summary>
internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    public ThemedMenuRenderer()
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(MenuPalette.Back(Theme.Current));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Nessuna striscia grigia a sinistra: il fondo del menù è uniforme.
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is not ToolStripDropDown)
        {
            return;
        }
        using var pen = new Pen(MenuPalette.Border(Theme.Current));
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        if (!item.Enabled || !(item.Selected || item.Pressed))
        {
            return;
        }
        var graphics = e.Graphics;
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(3, 1, item.Width - 7, item.Height - 3);
        using var path = Theme.RoundedRectangle(area, 6);
        using var brush = new SolidBrush(MenuPalette.HoverFill(Theme.Current));
        graphics.FillPath(brush, path);
        graphics.SmoothingMode = previous;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = MenuPalette.Text(Theme.Current, e.Item.Enabled, e.Item.Selected || e.Item.Pressed);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Current.Divider);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        // Voce disattivata: l'icona si attenua come il testo invece di restare piena.
        if (e.Image is not null && !e.Item.Enabled)
        {
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.4f };
            attributes.SetColorMatrix(matrix);
            e.Graphics.DrawImage(
                e.Image,
                e.ImageRectangle,
                0,
                0,
                e.Image.Width,
                e.Image.Height,
                GraphicsUnit.Pixel,
                attributes);
            return;
        }
        base.OnRenderItemImage(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var box = e.ImageRectangle;
        var side = Math.Min(box.Width, box.Height);
        var glyph = new Rectangle(
            box.Left + ((box.Width - side) / 2),
            box.Top + ((box.Height - side) / 2),
            side,
            side);
        glyph.Inflate(-1, -1);
        Glyphs.Draw(e.Graphics, glyph, Glyph.Check, Theme.Current.Brand, 2f);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = MenuPalette.Text(Theme.Current, e.Item?.Enabled ?? true, e.Item?.Selected ?? false);
        base.OnRenderArrow(e);
    }
}

/// <summary>Fabbrica dei menù, delle tendine e dei suggerimenti dell'app, tutti con lo stesso stile.</summary>
internal static class ThemedMenus
{
    /// <summary>Un menù a comparsa nello stile dell'app. <paramref name="checks"/> riserva lo spazio delle spunte.</summary>
    public static ContextMenuStrip Create(bool checks = false, bool icons = false)
    {
        var menu = new ContextMenuStrip();
        Style(menu, checks, icons);
        return menu;
    }

    /// <summary>L'icona di una voce: il disegno dell'app nel colore del testo secondario, alla scala dello schermo.</summary>
    public static Bitmap Icon(Glyph glyph, int dpi, Color? ink = null)
    {
        var side = Math.Max(16, (int)Math.Round(16 * dpi / 96.0));
        var bitmap = new Bitmap(side, side, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        Glyphs.Draw(graphics, new Rectangle(0, 0, side, side), glyph, ink ?? Theme.MutedInk, 1.5f * side / 16f);
        return bitmap;
    }

    /// <summary>Aggiunge una voce con la sua icona.</summary>
    public static ToolStripMenuItem Add(ToolStripDropDown menu, string text, Glyph glyph, EventHandler click, int dpi, Color? ink = null)
    {
        var item = new ToolStripMenuItem(text, Icon(glyph, dpi, ink), click);
        menu.Items.Add(item);
        return item;
    }

    /// <summary>Applica lo stile a un menù già creato; le voci aggiunte dopo sono sistemate all'apertura.</summary>
    public static void Style(ContextMenuStrip menu, bool checks = false, bool icons = false)
    {
        menu.Renderer = new ThemedMenuRenderer();
        menu.Font = Theme.Fonts.Ui;
        menu.ShowImageMargin = icons;
        menu.ShowCheckMargin = checks;
        menu.Padding = new Padding(0, 4, 0, 4);
        menu.BackColor = Theme.Surface;
        menu.ForeColor = Theme.Ink;
        menu.Opening += (_, _) => StyleItems(menu);
        StyleItems(menu);
    }

    private static void StyleItems(ToolStripDropDown menu)
    {
        menu.BackColor = Theme.Surface;
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripMenuItem entry)
            {
                entry.Padding = new Padding(2, 5, 2, 5);
                entry.ForeColor = Theme.Ink;
                entry.BackColor = Theme.Surface;
                if (entry.DropDown is { } child && !ReferenceEquals(child.Renderer, menu.Renderer))
                {
                    child.Renderer = menu.Renderer;
                    child.BackColor = Theme.Surface;
                    child.Font = Theme.Fonts.Ui;
                }
            }
        }
    }

    /// <summary>Suggerimento (tooltip) con i colori del tema e testo a capo ordinato.</summary>
    public static ToolTip CreateToolTip()
    {
        var tip = new ToolTip { OwnerDraw = true, AutoPopDelay = 12000, InitialDelay = 450, ReshowDelay = 150 };
        tip.Popup += (_, e) =>
        {
            var text = (e.AssociatedControl is { } owner ? tip.GetToolTip(owner) : null) ?? string.Empty;
            e.ToolTipSize = MeasureTip(text);
        };
        tip.Draw += (_, e) =>
        {
            var palette = Theme.Current;
            using (var fill = new SolidBrush(palette.Surface))
            {
                e.Graphics.FillRectangle(fill, e.Bounds);
            }
            using (var border = new Pen(palette.DividerStrong))
            {
                e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
            var inner = Rectangle.Inflate(new Rectangle(Point.Empty, e.Bounds.Size), -TipPaddingX, -TipPaddingY);
            UiText.Draw(e.Graphics, e.ToolTipText, Theme.Fonts.Small, inner, palette.Ink, TipFlags);
        };
        return tip;
    }

    private const int TipPaddingX = 10;
    private const int TipPaddingY = 7;
    private const int TipMaxWidth = 340;

    private const TextFormatFlags TipFlags =
        TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding;

    private static Size MeasureTip(string text)
    {
        var measured = TextRenderer.MeasureText(text, Theme.Fonts.Small, new Size(TipMaxWidth, 0), TipFlags);
        return new Size(measured.Width + (TipPaddingX * 2), measured.Height + (TipPaddingY * 2));
    }
}

/// <summary>
/// Tendina nello stile dell'app: campo con angoli tondi, bordo che si accende al passaggio e alla selezione,
/// freccia disegnata e voci dell'elenco con la stessa evidenziazione dei menù. La casella di sistema mostrava
/// frecce e bordi Windows, fuori posto soprattutto con il tema scuro.
/// </summary>
internal sealed class ThemedComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmEraseBkgnd = 0x0014;
    private const int WmPrintClient = 0x0318;

    private bool _hot;

    public ThemedComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        Font = Theme.Fonts.Ui;
        ItemHeight = Math.Max(22, Font.Height + 8);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hot = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnDropDown(EventArgs e)
    {
        base.OnDropDown(e);
        WidenDropDown();
        ThemeDropDownList();
    }

    /// <summary>L'elenco non taglia mai la voce più lunga.</summary>
    private void WidenDropDown()
    {
        var widest = Width;
        foreach (var item in Items)
        {
            var text = GetItemText(item);
            widest = Math.Max(widest, TextRenderer.MeasureText(text, Font).Width + 36);
        }
        DropDownWidth = Math.Min(widest, 520);
    }

    /// <summary>Barra di scorrimento e bordo dell'elenco scuri insieme al tema.</summary>
    private void ThemeDropDownList()
    {
        try
        {
            var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
            if (GetComboBoxInfo(Handle, ref info) && info.ListHandle != IntPtr.Zero)
            {
                _ = SetWindowTheme(info.ListHandle, Theme.IsDark ? "DarkMode_Explorer" : "Explorer", null);
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Senza il tema nativo l'elenco resta quello di sistema: solo meno rifinito.
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }
        var palette = Theme.Current;
        var hot = (e.State & DrawItemState.Selected) != 0;
        var area = e.Bounds;
        using (var back = new SolidBrush(palette.Surface))
        {
            e.Graphics.FillRectangle(back, area);
        }
        if (hot)
        {
            var graphics = e.Graphics;
            var previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Theme.RoundedRectangle(new Rectangle(area.X + 3, area.Y + 1, area.Width - 7, area.Height - 3), 5);
            using var fill = new SolidBrush(MenuPalette.HoverFill(palette));
            graphics.FillPath(fill, path);
            graphics.SmoothingMode = previous;
        }
        var ink = MenuPalette.Text(palette, Enabled, hot);
        var textArea = new Rectangle(area.X + 12, area.Y, area.Width - 20, area.Height);
        UiText.Draw(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            textArea,
            ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void WndProc(ref Message m)
    {
        if (DropDownStyle == ComboBoxStyle.DropDownList)
        {
            if (m.Msg == WmEraseBkgnd)
            {
                m.Result = (IntPtr)1;
                return;
            }
            if (m.Msg == WmPaint || m.Msg == WmPrintClient)
            {
                PaintFromMessage(ref m);
                return;
            }
        }
        base.WndProc(ref m);
    }

    private void PaintFromMessage(ref Message m)
    {
        if (m.Msg == WmPrintClient || m.WParam != IntPtr.Zero)
        {
            using var printed = Graphics.FromHdc(m.WParam);
            PaintFace(printed);
            return;
        }
        var hdc = BeginPaint(Handle, out var paint);
        try
        {
            using var graphics = Graphics.FromHdc(hdc);
            PaintFace(graphics);
        }
        finally
        {
            _ = EndPaint(Handle, ref paint);
        }
    }

    private void PaintFace(Graphics graphics)
    {
        var palette = Theme.Current;
        var bounds = new Rectangle(Point.Empty, Size);
        var outside = Parent?.BackColor ?? palette.Canvas;
        using (var back = new SolidBrush(outside))
        {
            graphics.FillRectangle(back, bounds);
        }
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var face = Rectangle.Inflate(bounds, -1, -1);
        face.Width -= 1;
        face.Height -= 1;
        var focused = Focused || DroppedDown;
        var border = !Enabled ? palette.Divider : focused ? palette.Brand : _hot ? palette.NeutralBorder : palette.Divider;
        var fill = !Enabled ? palette.DisabledFill : palette.FieldFill;
        using (var path = Theme.RoundedRectangle(face, 8))
        using (var fillBrush = new SolidBrush(fill))
        using (var pen = new Pen(border, focused ? 1.6f : 1f))
        {
            graphics.FillPath(fillBrush, path);
            graphics.DrawPath(pen, path);
        }

        var ink = Enabled ? palette.Ink : palette.DisabledInk;
        var chevronWidth = Math.Max(26, Height - 4);
        var textArea = new Rectangle(face.Left + 11, face.Top, face.Width - chevronWidth - 11, face.Height);
        var text = SelectedIndex >= 0 ? GetItemText(SelectedItem) : Text;
        UiText.Draw(
            graphics,
            text,
            Font,
            textArea,
            ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        var centerX = face.Right - (chevronWidth / 2f);
        var centerY = face.Top + (face.Height / 2f);
        using var chevron = new Pen(Enabled ? palette.MutedInk : palette.DisabledInk, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var flip = DroppedDown ? -1f : 1f;
        graphics.DrawLines(chevron, new[]
        {
            new PointF(centerX - 4f, centerY - (1.8f * flip)),
            new PointF(centerX, centerY + (2.2f * flip)),
            new PointF(centerX + 4f, centerY - (1.8f * flip))
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int Size;
        public NativeRect Item;
        public NativeRect Button;
        public int ButtonState;
        public IntPtr ComboHandle;
        public IntPtr EditHandle;
        public IntPtr ListHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr Hdc;
        public int Erase;
        public NativeRect Paint;
        public int Restore;
        public int IncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Reserved;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr window, out PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr window, ref PaintStruct paint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetComboBoxInfo(IntPtr window, ref ComboBoxInfo info);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? subAppName, string? subIdList);
}
