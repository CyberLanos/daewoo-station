using Content.Shared._F14.SCP;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using System.Numerics; 
using System;

namespace Content.Client._F14.Security;

public sealed class KeylockWindow : DefaultWindow
{
    private readonly EntityUid _keylock;
    private LineEdit _codeInput = null!;
    private Label _statusLabel = null!;
    private IEntityManager _entityManager = null!;
    
    public Action<string>? OnSubmitCode; 

    public KeylockWindow(EntityUid keylock, IEntityManager entityManager)
    {
        _keylock = keylock;
        _entityManager = entityManager;
        Title = "Keylock Panel";
        BuildUI();
    }

    private void BuildUI()
    {
        var vBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        Contents.AddChild(vBox);

        _statusLabel = new Label { Text = "Enter code:" };
        vBox.AddChild(_statusLabel);

        vBox.AddChild(new Control { MinSize = new Vector2(0, 10) });

        _codeInput = new LineEdit 
        { 
            PlaceHolder = "****"
        };
        vBox.AddChild(_codeInput);

        vBox.AddChild(new Control { MinSize = new Vector2(0, 10) });

        var gridButtons = new GridContainer { Columns = 3 };
        vBox.AddChild(gridButtons);

        for (int i = 1; i <= 9; i++)
        {
            var numStr = i.ToString();
            var btn = new Button { Text = numStr };
            btn.OnPressed += _ => AddDigit(numStr);
            gridButtons.AddChild(btn);
        }

        var zeroBtn = new Button { Text = "0" };
        zeroBtn.OnPressed += _ => AddDigit("0");
        gridButtons.AddChild(zeroBtn);

        var clearBtn = new Button { Text = "C" };
        clearBtn.OnPressed += _ => OnClear();
        gridButtons.AddChild(clearBtn);

        vBox.AddChild(new Control { MinSize = new Vector2(0, 10) });

        var submitBtn = new Button { Text = "ENTER" };
        submitBtn.OnPressed += _ => OnSubmit();
        vBox.AddChild(submitBtn);

        SetSize = new Vector2(300, 400);
    }

    private void AddDigit(string digit)
    {
        var currentStatus = _statusLabel.Text ?? "";
        if (currentStatus == "ACCESS GRANTED!" || currentStatus.StartsWith("ACCESS DENIED"))
        {
             _statusLabel.Text = "Enter code:";
             _statusLabel.FontColorOverride = null;
             _codeInput.Text = ""; 
        }

        // Безпечна перевірка вводу
        var currentCode = _codeInput.Text ?? "";
        if (currentCode.Length < 4)
        {
            _codeInput.Text = currentCode + digit;
        }
    }

    private void OnClear()
    {
        _codeInput.Text = "";
        _statusLabel.Text = "Enter code:";
        _statusLabel.FontColorOverride = null;
    }

    private void OnSubmit()
    {
        var code = _codeInput.Text ?? "";
        
        if (code.Length != 4)
        {
            _statusLabel.Text = "Code must be 4 digits!";
            _statusLabel.FontColorOverride = Robust.Shared.Maths.Color.Orange;
            return;
        }

        _statusLabel.Text = "Processing...";
        _statusLabel.FontColorOverride = null; 
        
        OnSubmitCode?.Invoke(code);
        _codeInput.Text = "";
    }

    public void UpdateStatus(bool isLocked, int failedAttempts, int maxAttempts)
    {
        if (!isLocked)
        {
            _statusLabel.Text = "ACCESS GRANTED!";
            _statusLabel.FontColorOverride = Robust.Shared.Maths.Color.LimeGreen;
        }
        else
        {
            _statusLabel.Text = $"ACCESS DENIED! ({failedAttempts}/{maxAttempts})";
            _statusLabel.FontColorOverride = Robust.Shared.Maths.Color.Red;
        }
    }
}

public sealed class KeylockBoundUserInterface : BoundUserInterface
{
    private KeylockWindow? _window;

    public KeylockBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new KeylockWindow(Owner, EntMan);
        
        _window.OnSubmitCode += (code) => 
        {
            SendMessage(new KeylockAttemptMessage(code));
        };

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        
        if (state is KeylockBuiState buiState)
        {
            _window?.UpdateStatus(buiState.IsLocked, buiState.FailedAttempts, buiState.MaxAttempts);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            if (_window != null)
            {
                _window.OnClose -= Close;
                _window.Close();
            }
        }
    }
}