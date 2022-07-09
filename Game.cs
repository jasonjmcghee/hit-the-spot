using Godot;
using System;

public enum GameState {
    MainMenu,
    PauseMenu,
    Playing
}

public class Game : Node2D {
    [Export] public GameState StartState = GameState.MainMenu;
    private GameState _gameState = GameState.MainMenu;

    private string _highscorePath = "user://best.score";
    private ulong _bestSpotsHit;
    private ulong _spotsHit;

    private ulong SpotsHit {
        get => _spotsHit;
        set {
            _spotsHit = value;
            _currentScoreNode.Text = $"Spots Hit: {_spotsHit}";
            if (_spotsHit > BestSpotsHit) {
                BestSpotsHit = _spotsHit;
            }
        }
    }

    public ulong BestSpotsHit {
        get => _bestSpotsHit;
        set {
            _bestSpotsHit = value;
            _bestScoreNode.Text = $"Best: {_bestSpotsHit}";
        }
    }

    private GameState GameState {
        get => _gameState;
        set {
            _gameState = value;
            UpdateGameState();
        }
    }

    private void UpdateGameState() {
        if (GameState == GameState.MainMenu) {
            _menuOverlay.Visible = true;
            _playButton.Visible = true;
            _resumeButton.Visible = false;
            _mainMenuButton.Visible = false;
        } else if (GameState == GameState.Playing) {
            GetTree().Paused = false;
            _menuOverlay.Visible = false;
        } else if (GameState == GameState.PauseMenu) {
            GetTree().Paused = true;
            _menuOverlay.Visible = true;
            _playButton.Visible = false;
            _resumeButton.Visible = true;
            _mainMenuButton.Visible = true;
        }
    }

    private ColorRect _menuOverlay;
    private VBoxContainer _menuOptions;
    private Button _playButton;
    private Button _resumeButton;
    private Button _mainMenuButton;
    private Button _quitButton;
    private VBoxContainer _scores;
    private Label _currentScoreNode;
    private Label _bestScoreNode;
    
    private Sprite _bar;
    private Sprite _dot;
    private Sprite _spot;
    private Vector2 _dotDirection = Vector2.Right;

    private bool _alreadyPressed;
    private bool _fire;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        _menuOverlay = GetNode<ColorRect>("Menu/ColorRect");
        _menuOptions = _menuOverlay.GetNode<VBoxContainer>("MenuOptions");
        _playButton = _menuOptions.GetNode<Button>("Play");
        _resumeButton = _menuOptions.GetNode<Button>("Resume");
        _mainMenuButton = _menuOptions.GetNode<Button>("MainMenu");
        _quitButton = _menuOptions.GetNode<Button>("Quit");
        _playButton.Connect("pressed", this, nameof(StartGame));
        _resumeButton.Connect("pressed", this, nameof(StartGame));
        _mainMenuButton.Connect("pressed", this, nameof(MainMenu));
        _quitButton.Connect("pressed", this, nameof(QuitGame));
        // Only show quit if this isn't a browser window
        _quitButton.Visible = !OS.HasFeature("JavaScript");
        
        _scores = GetNode<VBoxContainer>("Hud/Scores");
        _currentScoreNode = _scores.GetNode<Label>("Score");
        _bestScoreNode = _scores.GetNode<Label>("Best");
        
        CustomInput.EnsureActionKey("pause", KeyList.Escape);
        CustomInput.EnsureActionKey("fire", KeyList.Space);
        CustomInput.EnsureActionEvent("fire", new InputEventMouseButton { ButtonIndex = 1, Pressed = true });
        CustomInput.EnsureActionEvent("fire", new InputEventScreenTouch { Pressed = true });

        _bar = GetNode<Sprite>("Bar");
        _dot = _bar.GetNode<Sprite>("Dot");
        _spot = _bar.GetNode<Sprite>("Spot");
        _rng = new RandomNumberGenerator();
        
        GameState = StartState;
        LoadHighScore();
        ResetLevel();
    }

    public void StartGame() {
        GetTree().Paused = false;
        GameState = GameState.Playing;
    }
    
    public void MainMenu() {
        GetTree().Paused = true;
        ResetLevel();
        GetTree().Paused = false;
        GameState = GameState.MainMenu;
    }

    public void PauseGame() {
        GameState = GameState.PauseMenu;
    }

    public void GameOver() {
        SaveHighScore();
        GameState = GameState.MainMenu;
        ResetLevel();
    }
    
    public void QuitGame() {
        SaveHighScore();
        GetTree().Notification(NotificationWmQuitRequest);
    }

    public override void _Process(float delta) {
        if (GameState != GameState.MainMenu && Input.IsActionJustPressed("pause")) {
            PauseGame();
        }

        if (GameState == GameState.Playing) {
            if (Input.IsActionJustPressed("fire") || _fire) {
                _fire = false;
                if (DidHit()) {
                    AdvanceLevel();
                } else {
                    GameOver();
                }
            }
            
            if (_dotDirection == Vector2.Right && _dot.Position.x >= 1 - _dot.Scale.x) {
                _dotDirection = Vector2.Left;
            } else if (_dotDirection == Vector2.Left && _dot.Position.x <= 0) {
                _dotDirection = Vector2.Right;
            }
            _dot.Position += _dotDirection * DotSpeed * delta;
        }
    }

    private void ResetLevel() {
        SpotsHit = 0;
        DotSpeed = StartingDotSpeed;
        _spot.Scale = new Vector2(StartingSpotScale, 1.0f);
        PickNewSpotPosition();
    }

    private void AdvanceLevel() {
        SpotsHit += 1;
        DotSpeed *= 1.025f;
        _spot.Scale = new Vector2(_spot.Scale.x * 0.9f, 1.0f);
        PickNewSpotPosition();
    }

    private void PickNewSpotPosition() {
        _spot.Position = Vector2.Right * _rng.RandfRange(0.0f, 1.0f - _spot.Scale.x);
    }

    private bool DidHit() {
        return _dot.Position.x >= _spot.Position.x
               && _dot.Position.x + _dot.Scale.x <= _spot.Position.x + _spot.Scale.x;
    }

    [Export] public float StartingDotSpeed = 0.5f;
    [Export] public float StartingSpotScale = 0.5f;
    
    public float DotSpeed = 0.5f;
    private RandomNumberGenerator _rng;

    private void SaveHighScore() {
        var file = new File();
        file.Open(_highscorePath, File.ModeFlags.Write);
        file.Store64(_bestSpotsHit);
        file.Close();
    }
    
    private void LoadHighScore() {
        var file = new File();
        if (!file.FileExists(_highscorePath)) {
            BestSpotsHit = 0;
            return;
        }
        file.Open(_highscorePath, File.ModeFlags.Read);
        BestSpotsHit = file.Get64();
        file.Close();
    }
    
    // Workaround for Touch + HTML5
    public override void _UnhandledInput(InputEvent @event) {
        if (@event is InputEventMouseButton mouse) {
            if (mouse.Pressed && !_alreadyPressed) {
                _fire = true;
                _alreadyPressed = true;
            }

            if (!mouse.Pressed) {
                _fire = false;
                _alreadyPressed = false;
            }
        }
    }
}
