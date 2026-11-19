using Godot;

/// <summary>
/// Locked interaction point prepared for a future level. It can be unlocked
/// at runtime without replacing the office scene.
/// </summary>
public partial class RestrictedDoor : Area2D
{
    [Export] public bool IsUnlocked { get; set; }
    [Export] public string DestinationScenePath { get; set; } = "";
    [Export] public string DestinationSpawnName { get; set; } = "";
    [Export] public string LockedMessage { get; set; } =
        "ACESSO RESTRITO — credencial necessária.";

    private bool _playerInside;
    private double _messageSecondsRemaining;
    private Label _interactHint;
    private Label _statusLabel;

    public override void _Ready()
    {
        _interactHint = GetNodeOrNull<Label>("InteractHint");
        _statusLabel = GetNodeOrNull<Label>("StatusLabel");
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        if (_messageSecondsRemaining > 0.0)
        {
            _messageSecondsRemaining -= delta;

            if (_messageSecondsRemaining <= 0.0 && _statusLabel != null)
                _statusLabel.Visible = false;
        }

        if (
            _playerInside
            && Input.IsActionJustPressed("interact")
        )
        {
            TryEnter();
        }
    }

    public void Unlock()
    {
        IsUnlocked = true;

        if (_interactHint != null)
            _interactHint.Text = "E  ENTRAR";
    }

    private void TryEnter()
    {
        if (!IsUnlocked)
        {
            AudioManager.Instance?.PlayError();
            ShowStatus(LockedMessage);
            return;
        }

        if (string.IsNullOrEmpty(DestinationScenePath))
        {
            AudioManager.Instance?.PlayError();
            ShowStatus("A área restrita ainda não está disponível.");
            return;
        }

        AudioManager.Instance?.PlayDoor();
        GameManager.Instance?.ChangeScene(
            DestinationScenePath,
            DestinationSpawnName
        );
    }

    private void ShowStatus(string message)
    {
        if (_statusLabel == null)
            return;

        _statusLabel.Text = message;
        _statusLabel.Visible = true;
        _messageSecondsRemaining = 2.5;
    }

    private void OnBodyEntered(Node body)
    {
        if (!body.IsInGroup("Player"))
            return;

        _playerInside = true;

        if (_interactHint != null)
        {
            _interactHint.Text = IsUnlocked
                ? "E  ENTRAR"
                : "E  ACESSO RESTRITO";
            _interactHint.Visible = true;
        }
    }

    private void OnBodyExited(Node body)
    {
        if (!body.IsInGroup("Player"))
            return;

        _playerInside = false;

        if (_interactHint != null)
            _interactHint.Visible = false;

        if (_statusLabel != null)
            _statusLabel.Visible = false;

        _messageSecondsRemaining = 0.0;
    }
}
