using Content.Shared._F14.SCP;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._F14.SCP;

public sealed class SCP294Window : DefaultWindow
{
    private readonly BoundUserInterface _bui;
    private Label _display = null!;
    private string _currentInput = "";
    private int _quartersInserted = 0;

    public SCP294Window(BoundUserInterface bui)
    {
        _bui = bui;
        Title = "SCP-294";
        BuildUI();
    }

    private void BuildUI()
    {
        var mainPanel = new PanelContainer();
        Contents.AddChild(mainPanel);

        var mainVBox = new GridContainer { Columns = 1 };
        mainPanel.AddChild(mainVBox);

        // Display screen with background
        var displayBg = new Control();
        displayBg.MinSize = new(280, 40);
        displayBg.ModulateSelfOverride = Color.Black;
        
        _display = new Label 
        { 
            Text = "INSERT QUARTER",
            Align = Label.AlignMode.Center,
            FontColorOverride = Color.Green
        };
        
        var displayPanel = new PanelContainer();
        displayPanel.MinSize = new(280, 40);
        displayPanel.AddChild(_display);
        mainVBox.AddChild(displayPanel);

        mainVBox.AddChild(new Control { MinSize = new(0, 5) });

        // Number row: 1-0
        var row1 = new GridContainer { Columns = 10 };
        for (int i = 1; i <= 9; i++)
            row1.AddChild(MakeButton(i.ToString()));
        row1.AddChild(MakeButton("0"));
        mainVBox.AddChild(row1);

        // QWERTY row
        var row2 = new GridContainer { Columns = 10 };
        foreach (var letter in new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" })
            row2.AddChild(MakeButton(letter));
        mainVBox.AddChild(row2);

        // ASDF row
        var row3 = new GridContainer { Columns = 10 };
        row3.AddChild(new Control());
        foreach (var letter in new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" })
            row3.AddChild(MakeButton(letter));
        mainVBox.AddChild(row3);

        // ZXCV row
        var row4 = new GridContainer { Columns = 10 };
        row4.AddChild(new Control());
        row4.AddChild(new Control());
        foreach (var letter in new[] { "Z", "X", "C", "V", "B", "N", "M" })
            row4.AddChild(MakeButton(letter));
        row4.AddChild(MakeButton("←"));
        mainVBox.AddChild(row4);

        // Control buttons row
        var row5 = new GridContainer { Columns = 4 };
        var insertBtn = new Button { Text = "INSERT\nQUARTER", MinSize = new(60, 30) };
        insertBtn.OnToggled += _ => OnInsertQuarter();
        row5.AddChild(insertBtn);

        var dispenseBtn = new Button { Text = "DISPENSE", MinSize = new(60, 30) };
        dispenseBtn.OnToggled += _ => OnDispense();
        row5.AddChild(dispenseBtn);

        var clearBtn = new Button { Text = "CLEAR", MinSize = new(60, 30) };
        clearBtn.OnToggled += _ => OnClear();
        row5.AddChild(clearBtn);

        var spaceBtn = new Button { Text = "SPACE", MinSize = new(60, 30) };
        spaceBtn.OnToggled += _ => AddCharacter(" ");
        row5.AddChild(spaceBtn);

        mainVBox.AddChild(row5);

        SetSize = new(310, 380);
    }

    private Button MakeButton(string text)
    {
        var btn = new Button 
        { 
            Text = text,
            MinSize = new(25, 25)
        };
        btn.OnToggled += _ => OnKeyPressed(text);
        return btn;
    }

    private void OnKeyPressed(string key)
    {
        if (key == "←")
        {
            OnBackspace();
        }
        else
        {
            AddCharacter(key);
        }
    }

    private void AddCharacter(string character)
    {
        if (_currentInput.Length < 30) // Limit input
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
        if (_currentInput.Length == 0)
            _display.Text = $"CREDITS: {_quartersInserted}/2";
        else
            _display.Text = _currentInput;
    }

    private void OnInsertQuarter()
    {
        _quartersInserted++;
        _display.Text = $"CREDITS: {_quartersInserted}/2";
        _bui.SendMessage(new SCP294InsertQuarterMessage());
    }

    private void OnDispense()
    {
        if (string.IsNullOrWhiteSpace(_currentInput))
        {
            _display.Text = "EMPTY INPUT";
            return;
        }

        if (_quartersInserted < 2)
        {
            _display.Text = $"NEED {2 - _quartersInserted} MORE";
            return;
        }

        _display.Text = "DISPENSING...";
        _bui.SendMessage(new SCP294RequestLiquidMessage(_currentInput));
        _currentInput = "";
        _quartersInserted = 0;
    }
}

public sealed class SCP294BoundUserInterface : BoundUserInterface
{
    private SCP294Window? _window;

    public SCP294BoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new SCP294Window(this);
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _window != null)
            _window.OnClose -= Close;
    }
}
