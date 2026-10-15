using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace PresenzeXmlConverter;

/// <summary>
/// Dissolvenza quando si cambia sezione: la sezione nuova compare con una breve dissolvenza e un lieve scorrimento
/// verso l'alto invece di apparire di colpo. Si fotografa la sezione nuova (già impaginata e riempita) e se ne
/// dipinge l'immagine in un velo sopra l'area delle sezioni; a fine corsa il velo si toglie e resta la sezione vera.
/// Il velo non intercetta il mouse e, se qualcosa non va (finestra non visibile, errore di disegno, effetti di
/// Windows spenti), la sezione appare semplicemente subito, come prima.
/// </summary>
internal sealed class SectionTransition : IDisposable
{
    private readonly Control _host;
    private AnimationClock? _clock;
    private Veil? _veil;

    public SectionTransition(Control host) => _host = host;

    /// <summary>Anima l'ingresso di <paramref name="incoming"/> (già visibile, impaginata e aggiornata).</summary>
    public void Play(Control incoming)
    {
        Cancel();
        if (!AnimationClock.EffectsEnabled || !_host.IsHandleCreated || !_host.Visible || incoming.IsDisposed)
        {
            return;
        }
        var size = _host.ClientSize;
        if (size.Width < 80 || size.Height < 80)
        {
            return;
        }

        Bitmap? snapshot = null;
        try
        {
            incoming.PerformLayout();
            snapshot = new Bitmap(size.Width, size.Height);
            incoming.DrawToBitmap(snapshot, new Rectangle(0, 0, size.Width, size.Height));
            _veil = new Veil(snapshot, _host.BackColor, Math.Max(6, (int)Math.Round(12 * _host.DeviceDpi / 96.0)))
            {
                Bounds = new Rectangle(Point.Empty, size)
            };
            snapshot = null;
            _host.Controls.Add(_veil);
            _veil.BringToFront();
            _clock ??= CreateClock();
            _clock.Jump(0f);
            _clock.AnimateTo(1f, Motion.Standard);
            if (!_clock.IsRunning)
            {
                Cancel();
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or
            OutOfMemoryException or ExternalException)
        {
            // La dissolvenza è un abbellimento: se non si può fare, la sezione resta quella vera, senza velo.
            snapshot?.Dispose();
            Cancel();
        }
    }

    private AnimationClock CreateClock()
    {
        var clock = new AnimationClock(_host);
        clock.Frame += (_, _) =>
        {
            if (_veil is null)
            {
                return;
            }
            _veil.Progress = clock.Value;
            if (!clock.IsRunning)
            {
                Cancel();
            }
        };
        return clock;
    }

    /// <summary>Toglie subito il velo (per esempio perché si è cambiata di nuovo sezione).</summary>
    public void Cancel()
    {
        _clock?.Jump(1f);
        if (_veil is null)
        {
            return;
        }
        var veil = _veil;
        _veil = null;
        if (!_host.IsDisposed)
        {
            _host.Controls.Remove(veil);
        }
        veil.Dispose();
    }

    public void Dispose()
    {
        Cancel();
        _clock?.Dispose();
        _clock = null;
    }

    /// <summary>Il velo: dipinge la foto della sezione nuova con opacità e posizione che seguono l'avanzamento.</summary>
    private sealed class Veil : Control
    {
        private const int HitTestTransparent = -1;
        private const int WmNcHitTest = 0x0084;

        private readonly Bitmap _image;
        private readonly Color _background;
        private readonly int _rise;

        public Veil(Bitmap image, Color background, int rise)
        {
            _image = image;
            _background = background;
            _rise = rise;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            TabStop = false;
        }

        public float Progress
        {
            get => _progress;
            set
            {
                _progress = Math.Clamp(value, 0f, 1f);
                Invalidate();
            }
        }

        private float _progress;

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var back = new SolidBrush(_background))
            {
                e.Graphics.FillRectangle(back, ClientRectangle);
            }
            var offset = (int)Math.Round((1f - _progress) * _rise);
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = _progress });
            e.Graphics.DrawImage(
                _image,
                new Rectangle(0, offset, _image.Width, _image.Height),
                0,
                0,
                _image.Width,
                _image.Height,
                GraphicsUnit.Pixel,
                attributes);
        }

        // Il mouse passa al contenuto sotto: nemmeno per un istante il velo blocca i clic.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest)
            {
                m.Result = (IntPtr)HitTestTransparent;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
