using Godot;

public partial class Loading : Control
{
    [Export]
    private Label _loadingLabel;

    [Export]
    private AnimationPlayer _animationPlayer;

    public override void _Ready()
    {
        _animationPlayer?.Play("Pulse");
    }

    public void SetLoadingText(string text)
    {
        if (_loadingLabel != null)
        {
            _loadingLabel.Text = text;
        }
    }
}
