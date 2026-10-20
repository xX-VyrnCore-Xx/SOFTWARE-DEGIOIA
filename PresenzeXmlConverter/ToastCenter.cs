using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PresenzeXmlConverter;

internal enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Notifiche dentro la finestra: un cartellino per evento (conversione finita, file salvato, copia
/// negli appunti, aggiornamento pronto…) in basso a destra, che si ritira da solo. Le notifiche di
/// Windows (fumetto del tray) arrivano solo quando la finestra non è in primo piano, quindi chi sta
/// lavorando non vedeva nulla e le notifiche sembravano «non funzionare»: queste si vedono sempre.
/// Passando il mouse il conto alla rovescia si ferma; un clic le chiude (o esegue l'azione collegata).
/// </summary>
internal sealed class ToastCenter : IDisposable
{
    private const int MaxVisible = 4;
    private const int CardWidth = 372;
    private const int Gap = 10;
    private const int RightMargin = 24;
    // Sotto la riga di stato: 34 px del piè di pagina più il margine del contenitore.
    private const int BottomMargin = 56;

    private readonly Form _owner;
    private readonly List<ToastCard> _cards = [];
    private readonly List<ToastCard> _closing = [];
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 80 };
    // Animazione dei cartellini: entrano scorrendo da destra, escono nello stesso modo e gli altri si ricompongono
    // con un movimento morbido invece di scattare. Il timer resta acceso solo finché qualcosa si muove.
    private readonly System.Windows.Forms.Timer _motion = new() { Interval = 15 };
    private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
    private int _right;
    private bool _disposed;

    private double NowMs => _watch.Elapsed.TotalMilliseconds;

    public ToastCenter(Form owner)
    {
        _owner = owner;
        _clock.Tick += (_, _) => Tick();
        _motion.Tick += (_, _) => Step();
        _owner.Resize += (_, _) => Arrange();
        _owner.Disposed += (_, _) => Dispose();
    }

    /// <summary>Mostra un cartellino. Lo stesso testo già visibile viene solo rinnovato.</summary>
    public void Show(ToastKind kind, string title, string message, Action? onClick = null)
    {
        if (_disposed || _owner.IsDisposed || !_owner.IsHandleCreated)
        {
            return;
        }
        var existing = _cards.FirstOrDefault(card => card.Title == title && card.Message == message);
        if (existing is not null)
        {
            existing.Remaining = existing.Lifetime;
            return;
        }

        var card = new ToastCard(kind, title, message, onClick, _owner.DeviceDpi / 96f);
        card.Dismissed += (_, _) => Remove(card);
        _cards.Add(card);
        while (_cards.Count > MaxVisible)
        {
            Remove(_cards[0]);
        }
        _owner.Controls.Add(card);
        card.BringToFront();
        if (AnimationClock.EffectsEnabled)
        {
            card.Slide.Jump(SlideDistance(card));
            card.Slide.MoveTo(0f, NowMs, Motion.Standard);
        }
        Arrange();
        _clock.Start();
        _motion.Start();
    }

    private static float SlideDistance(ToastCard card) => card.Width * 0.35f;

    private void Remove(ToastCard card)
    {
        if (!_cards.Remove(card))
        {
            return;
        }
        if (AnimationClock.EffectsEnabled && !_disposed && !_owner.IsDisposed)
        {
            // Esce scorrendo verso destra; il controllo si elimina a fine corsa.
            card.Closing = true;
            card.Slide.MoveTo(SlideDistance(card), NowMs, Motion.Quick);
            _closing.Add(card);
            _motion.Start();
        }
        else
        {
            Discard(card);
        }
        Arrange();
        if (_cards.Count == 0)
        {
            _clock.Stop();
        }
    }

    private void Discard(ToastCard card)
    {
        _closing.Remove(card);
        _owner.Controls.Remove(card);
        card.Dispose();
    }

    /// <summary>Un fotogramma: posiziona i cartellini, toglie quelli usciti, spegne il timer quando tutto è fermo.</summary>
    private void Step()
    {
        if (_disposed || _owner.IsDisposed)
        {
            _motion.Stop();
            return;
        }
        var now = NowMs;
        var running = false;
        foreach (var card in _cards.Concat(_closing).ToArray())
        {
            card.Location = new Point(
                _right - card.Width + (int)Math.Round(card.Slide.ValueAt(now)),
                (int)Math.Round(card.Lift.ValueAt(now)));
            running |= card.Slide.IsRunning(now) || card.Lift.IsRunning(now);
            if (card.Closing && !card.Slide.IsRunning(now))
            {
                Discard(card);
            }
        }
        if (!running && _closing.Count == 0)
        {
            _motion.Stop();
        }
    }

    private void Tick()
    {
        foreach (var card in _cards.ToArray())
        {
            if (card.IsHovered)
            {
                continue;
            }
            card.Remaining -= _clock.Interval;
            if (card.Remaining <= 0)
            {
                Remove(card);
            }
        }
    }

    /// <summary>Impila i cartellini dal basso verso l'alto, il più recente in basso.</summary>
    private void Arrange()
    {
        if (_owner.IsDisposed)
        {
            return;
        }
        var scale = _owner.DeviceDpi / 96f;
        _right = _owner.ClientSize.Width - (int)(RightMargin * scale);
        var y = _owner.ClientSize.Height - (int)(BottomMargin * scale);
        var now = NowMs;
        for (var i = _cards.Count - 1; i >= 0; i--)
        {
            var card = _cards[i];
            y -= card.Height;
            var target = Math.Max(0, y);
            if (!card.Placed)
            {
                // Il primo posto è subito quello giusto: a muoversi è solo l'ingresso da destra.
                card.Placed = true;
                card.Lift.Jump(target);
            }
            else if (Math.Abs(card.Lift.Target - target) > 0.5f)
            {
                card.Lift.MoveTo(target, now, AnimationClock.EffectsEnabled ? Motion.Standard : 0);
            }
            y -= (int)(Gap * scale);
        }
        Step();
        if (_cards.Concat(_closing).Any(card => card.Lift.IsRunning(now) || card.Slide.IsRunning(now)))
        {
            _motion.Start();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _clock.Stop();
        _clock.Dispose();
        _motion.Stop();
        _motion.Dispose();
    }

    private sealed class ToastCard : Control
    {
        private readonly ToastKind _kind;
        private readonly Action? _onClick;
        private readonly float _scale;
        private bool _hovered;

        public ToastCard(ToastKind kind, string title, string message, Action? onClick, float scale)
        {
            _kind = kind;
            _onClick = onClick;
            _scale = scale;
            Title = title;
            Message = message;
            Lifetime = kind is ToastKind.Error or ToastKind.Warning ? 9000 : 5500;
            Remaining = Lifetime;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Alert;
            AccessibleName = $"{title}. {message}";
            TabStop = false;

            var width = (int)(CardWidth * scale);
            var textWidth = width - TextLeft - Px(16);
            var messageHeight = TextRenderer.MeasureText(
                message,
                Theme.Fonts.Small,
                new Size(textWidth, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            var height = Math.Max(Px(62), Px(14) + Px(20) + messageHeight + Px(14));
            Size = new Size(width, height);
            ApplyRoundedRegion();
        }

        public string Title { get; }

        public string Message { get; }

        public int Lifetime { get; }

        public int Remaining { get; set; }

        public bool IsHovered => _hovered;

        /// <summary>Scostamento orizzontale dell'ingresso e dell'uscita (0 = al suo posto).</summary>
        public Tween Slide { get; } = new();

        /// <summary>Posizione verticale: segue quella di impilamento con un movimento morbido.</summary>
        public Tween Lift { get; } = new();

        public bool Placed { get; set; }

        public bool Closing { get; set; }

        public event EventHandler? Dismissed;

        private int TextLeft => Px(58);

        private int Px(int value) => (int)Math.Round(value * _scale);

        private void ApplyRoundedRegion()
        {
            using var path = Theme.RoundedRectangle(new Rectangle(0, 0, Width, Height), Px(14));
            var old = Region;
            Region = new Region(path);
            old?.Dispose();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Remaining = Lifetime;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            try
            {
                _onClick?.Invoke();
            }
            finally
            {
                // Il cartellino si toglie a clic finito: eliminarlo dentro il suo stesso evento lascerebbe
                // a Windows Forms da completare il clic su un controllo già distrutto.
                BeginInvoke(new Action(() => Dismissed?.Invoke(this, EventArgs.Empty)));
            }
        }

        private (Color Accent, string Glyph) Style => _kind switch
        {
            ToastKind.Success => (Theme.Success, "✓"),
            ToastKind.Warning => (Theme.Warning, "!"),
            ToastKind.Error => (Theme.Danger, "×"),
            _ => (Theme.Brand, "i")
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRectangle(bounds, Px(14)))
            {
                using var fill = new SolidBrush(_hovered ? Theme.AltRow : Theme.Surface);
                graphics.FillPath(fill, path);
                using var border = new Pen(Theme.DividerStrong, 1f);
                graphics.DrawPath(border, path);
            }

            var (accent, glyph) = Style;
            var badge = new Rectangle(Px(16), (Height - Px(28)) / 2, Px(28), Px(28));
            using (var badgeFill = new SolidBrush(accent))
            {
                graphics.FillEllipse(badgeFill, badge);
            }
            UiText.Draw(
                graphics,
                glyph,
                Theme.Fonts.DialogTitle,
                badge,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var textWidth = Width - TextLeft - Px(16);
            UiText.Draw(
                graphics,
                Title,
                Theme.Fonts.SmallBold,
                new Rectangle(TextLeft, Px(12), textWidth, Px(20)),
                Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            UiText.Draw(
                graphics,
                Message,
                Theme.Fonts.Small,
                new Rectangle(TextLeft, Px(12) + Px(20), textWidth, Height - Px(12) - Px(20) - Px(8)),
                Theme.InkSecondary,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }
    }
}

/// <summary>
/// Fa lampeggiare il pulsante nella barra delle applicazioni finché la finestra non torna davanti:
/// è l'avviso che funziona anche quando i fumetti di Windows sono disattivati (Assistente focus,
/// notifiche spente per l'app).
/// </summary>
internal static class TaskbarFlash
{
    private const uint FlashAll = 3;
    private const uint FlashUntilForeground = 12;

    public static void Flash(Form form)
    {
        if (!OperatingSystem.IsWindows() || form.IsDisposed || !form.IsHandleCreated)
        {
            return;
        }
        try
        {
            var info = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf<FlashInfo>(),
                Window = form.Handle,
                Flags = FlashAll | FlashUntilForeground,
                Count = uint.MaxValue,
                Timeout = 0
            };
            FlashWindowEx(ref info);
        }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException or InvalidOperationException)
        {
            // Avviso accessorio: se non è possibile, resta il fumetto.
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);
}
