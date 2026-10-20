namespace PresenzeXmlConverter;

/// <summary>Cosa mostra la dashboard, deciso solo dai dati presenti (calcolo puro, verificabile con un test).</summary>
internal readonly record struct DashboardLayout(bool ShowMetrics, bool ShowBottom, bool ShowInsights)
{
    /// <param name="hasRequests">Un foglio è stato letto.</param>
    /// <param name="hasPreview">L'anteprima dei movimenti è pronta.</param>
    /// <param name="hasExports">Esiste almeno un'esportazione registrata.</param>
    public static DashboardLayout For(bool hasRequests, bool hasPreview, bool hasExports) =>
        new(
            ShowMetrics: hasRequests || hasPreview,
            ShowBottom: hasPreview || hasExports,
            ShowInsights: hasPreview);
}
