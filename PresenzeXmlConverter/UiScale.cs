namespace PresenzeXmlConverter;

/// <summary>
/// Misure in pixel del dispositivo per ciò che si disegna o si imposta a finestra
/// aperta. I valori dichiarati nel costruttore li scala la finestra da sola
/// (AutoScaleMode.Dpi), ma a 125% o 150% i font crescono e le misure scritte a
/// mano dentro OnPaint o in un aggiornamento successivo restano ferme: voci della
/// barra troppo basse, righe che tagliano il testo. Qui il valore "a 96 dpi" viene
/// portato alla scala reale del controllo.
/// </summary>
internal static class UiScale
{
    /// <summary>Fattore di scala (1,0 a 96 dpi) di un controllo con la finestra già creata.</summary>
    public static float Factor(this Control control) =>
        control.IsHandleCreated ? Math.Max(1f, control.DeviceDpi / 96f) : 1f;

    /// <summary>Un valore a 96 dpi portato alla scala del controllo (invariato prima che la finestra esista).</summary>
    public static int Px(this Control control, int logical) =>
        (int)Math.Round(logical * control.Factor());

    /// <summary>Come <see cref="Px(Control,int)"/>, con decimali (spessori di linea, raggi).</summary>
    public static float Px(this Control control, float logical) => logical * control.Factor();

    /// <summary>Quante volte la rete di sicurezza ha dovuto scalare una finestra (per i test e la diagnostica).</summary>
    public static int Corrections { get; private set; }

    /// <summary>
    /// Rete di sicurezza: se a finestra aperta le misure dei controlli (righe, card, margini) non sono
    /// state scalate con il DPI mentre i font sì, i testi escono dalle loro scatole (titoli tagliati,
    /// righe sovrapposte). Un riquadro di prova, fuori vista, ha 96 px di lato a 96 dpi: se a 125% è
    /// ancora 96 px, la scalatura automatica non c'è stata e viene fatta qui, una volta sola.
    /// </summary>
    public static void GuardAutoScale(Form form)
    {
        var sentinel = new Panel
        {
            Name = "dpi-sentinel",
            Size = new Size(96, 96),
            Location = new Point(-4000, -4000),
            TabStop = false
        };
        form.Controls.Add(sentinel);
        form.Shown += (_, _) => Verify(form, sentinel);
    }

    private static void Verify(Form form, Control sentinel)
    {
        try
        {
            var factor = form.DeviceDpi / 96f;
            if (factor > 1.04f && !sentinel.IsDisposed && sentinel.Width < (int)(96 * factor * 0.9f))
            {
                Corrections++;
                form.SuspendLayout();
                foreach (Control child in form.Controls)
                {
                    if (!ReferenceEquals(child, sentinel))
                    {
                        child.Scale(new SizeF(factor, factor));
                    }
                }
                form.ResumeLayout(true);
            }
        }
        finally
        {
            form.Controls.Remove(sentinel);
            sentinel.Dispose();
        }
    }
}
