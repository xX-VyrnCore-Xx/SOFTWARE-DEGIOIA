namespace PresenzeXmlConverter.Tests;

/// <summary>
/// «Esci definitivamente» nel dialogo di chiusura non chiudeva il programma: la
/// finestra principale considerava «scelta valida» solo l'esito OK, ma quel
/// pulsante restituisce Abort, che veniva trattato come annullamento.
/// </summary>
public class CloseDialogTests
{
    [Theory]
    [InlineData(DialogResult.OK, true)]      // Riduci in tray
    [InlineData(DialogResult.Abort, true)]   // Esci definitivamente
    [InlineData(DialogResult.Cancel, false)] // la X o Esc: ci ripensa, la finestra resta
    [InlineData(DialogResult.None, false)]
    public void OnlyTheTwoButtonsAreChoices(DialogResult result, bool isChoice)
    {
        Assert.Equal(isChoice, CloseBehaviorDialog.IsChoice(result));
    }

    [Fact]
    public void ExitMapsToTheExitBehaviorAndTrayToTheTrayBehavior()
    {
        using var dialog = new CloseBehaviorDialog();

        dialog.DialogResult = DialogResult.Abort;
        Assert.Equal("exit", dialog.SelectedBehavior);

        dialog.DialogResult = DialogResult.OK;
        Assert.Equal("tray", dialog.SelectedBehavior);
    }

    [Fact]
    public void RememberingTheChoiceIsNeverPreselected()
    {
        using var dialog = new CloseBehaviorDialog();

        // «Non chiedere più» è una scelta da fare di proposito: preselezionata, un
        // primo clic su «Riduci in tray» nascondeva per sempre il programma.
        Assert.False(dialog.RememberChoice);
    }
}
