using Content.Shared._F14.SCP;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using System;
using System.Numerics;

namespace Content.Client._F14.SCP;

public sealed class SCP294Window : DefaultWindow
{
    private readonly BoundUserInterface _bui;
    private Label _display = null!;
    private string _currentInput = "";
    private int _quartersInserted = 0;
    private int _quartersRequired = 2;

    public SCP294Window(BoundUserInterface bui)
    {
        _bui = bui;
        Title = "Old Coffee machine";
        BuildUI();
    }

    private void BuildUI()
    {
        var mainPanel = new PanelContainer();
        Contents.AddChild(mainPanel);

        var mainVBox = new GridContainer { Columns = 1 };
        mainPanel.AddChild(mainVBox);

        var displayBg = new Control();
        displayBg.MinSize = new Vector2(300, 50);
        displayBg.ModulateSelfOverride = Color.Black;

        _display = new Label
        {
            Text = "INSERT QUARTER",
            Align = Label.AlignMode.Center,
            FontColorOverride = Color.Green
        };

        var displayPanel = new PanelContainer();
        displayPanel.MinSize = new Vector2(300, 50);
        displayPanel.AddChild(_display);
        mainVBox.AddChild(displayPanel);

        mainVBox.AddChild(new Control { MinSize = new Vector2(0, 10) });

        var row1 = new GridContainer { Columns = 10 };
        for (int i = 1; i <= 9; i++)
            row1.AddChild(MakeButton(i.ToString()));
        row1.AddChild(MakeButton("0"));
        mainVBox.AddChild(row1);

        var row2 = new GridContainer { Columns = 10 };
        foreach (var letter in new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" })
            row2.AddChild(MakeButton(letter));
        mainVBox.AddChild(row2);

        var row3 = new GridContainer { Columns = 10 };
        row3.AddChild(new Control { MinSize = new Vector2(14, 0) });
        foreach (var letter in new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" })
            row3.AddChild(MakeButton(letter));
        mainVBox.AddChild(row3);

        var row4 = new GridContainer { Columns = 10 };
        row4.AddChild(new Control { MinSize = new Vector2(28, 0) });
        foreach (var letter in new[] { "Z", "X", "C", "V", "B", "N", "M" })
            row4.AddChild(MakeButton(letter));
        row4.AddChild(MakeButton("←"));
        mainVBox.AddChild(row4);

        mainVBox.AddChild(new Control { MinSize = new Vector2(0, 10) });

        var row5 = new GridContainer { Columns = 4 };

        var insertBtn = new Button { Text = "INSERT\nQUARTER", MinSize = new Vector2(75, 40) };
        insertBtn.OnPressed += _ => OnInsertQuarter();
        row5.AddChild(insertBtn);

        var dispenseBtn = new Button { Text = "DISPENSE", MinSize = new Vector2(75, 40) };
        dispenseBtn.OnPressed += _ => OnDispense();
        row5.AddChild(dispenseBtn);

        var clearBtn = new Button { Text = "CLEAR", MinSize = new Vector2(75, 40) };
        clearBtn.OnPressed += _ => OnClear();
        row5.AddChild(clearBtn);

        var spaceBtn = new Button { Text = "SPACE", MinSize = new Vector2(75, 40) };
        spaceBtn.OnPressed += _ => AddCharacter(" ");
        row5.AddChild(spaceBtn);

        mainVBox.AddChild(row5);

        MinSize = new Vector2(360, 430);
    }

    private Button MakeButton(string text)
    {
        var btn = new Button { Text = text, MinSize = new Vector2(30, 30) };
        btn.OnPressed += _ => OnKeyPressed(text);
        return btn;
    }

    private void OnKeyPressed(string key)
    {
        if (key == "←") OnBackspace();
        else AddCharacter(key);
    }

    private void AddCharacter(string character)
    {
        if (_currentInput.Length < 30)
        {
            _currentInput += character;
            UpdateDisplay();
        }
    }

    private void OnBackspace()
    {
        if (_currentInput.Length > 0)
            _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
        UpdateDisplay();
    }

    private void OnClear()
    {
        _currentInput = "";
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (string.IsNullOrEmpty(_currentInput))
            _display.Text = $"CREDITS: {_quartersInserted}/{_quartersRequired}";
        else
            _display.Text = _currentInput;
    }

    public void UpdateState(SCP294BuiState state)
    {
        _quartersInserted = state.QuartersInserted;
        _quartersRequired = state.QuartersRequired;

        if (state.LastMessage != null)
        {
            _display.Text = state.LastMessage;
            _currentInput = "";
        }
        else
        {
            UpdateDisplay();
        }
    }

    private void OnInsertQuarter()
    {
        _bui.SendMessage(new SCP294InsertQuarterMessage());
    }

    private void OnDispense()
    {
        if (string.IsNullOrWhiteSpace(_currentInput)) return;

        _display.Text = "DISPENSING...";
        _bui.SendMessage(new SCP294RequestLiquidMessage(_currentInput));
    }
}

public sealed class SCP294BoundUserInterface : BoundUserInterface
{
    private SCP294Window? _window;

    public SCP294BoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        if (_window != null)
        {
            if (_window.IsOpen)
                _window.MoveToFront();
            return;
        }

        _window = new SCP294Window(this);
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is SCP294BuiState buiState)
        {
            _window?.UpdateState(buiState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Dispose();
            _window = null;
        }
    }
}