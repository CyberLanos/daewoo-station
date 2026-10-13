// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Lobby.UI.Background;

/// <summary>
/// The Background tab's look, after the Traits tab: flat rectangular panels, thin lines, a coloured accent line per
/// section, and colour only where it means something (blue for counters, red for over the limit or removing).
/// </summary>
public static class BackgroundStyle
{
    public static readonly Color Panel = Color.FromHex("#20242e");
    public static readonly Color Row = Color.FromHex("#242834");
    public static readonly Color RowHover = Color.FromHex("#2a2f3b");
    public static readonly Color Head = Color.FromHex("#262b37");
    public static readonly Color Line = Color.FromHex("#343a47");
    public static readonly Color LineSoft = Color.FromHex("#2b313d");
    public static readonly Color Input = Color.FromHex("#1c2028");
    public static readonly Color Text = Color.FromHex("#e4e7ec");
    public static readonly Color TextDim = Color.FromHex("#98a0ac");
    public static readonly Color TextFaint = Color.FromHex("#6a7280");

    public static readonly Color Accent = Color.FromHex("#4f93d6");
    public static readonly Color AccentFill = Color.FromHex("#253649");
    public static readonly Color Good = Color.FromHex("#58bd7c");
    public static readonly Color Bad = Color.FromHex("#df6152");

    /// <summary>Section accents: IC, OOC and the station records (the HR file's gold).</summary>
    public static readonly Color Ic = Color.FromHex("#33b3c7");
    public static readonly Color Ooc = Color.FromHex("#b155c9");
    public static readonly Color Records = Color.FromHex("#dcb63c");

    /// <summary>
    /// A field's card: a row band with the section colour down its left edge.
    /// </summary>
    public static StyleBoxFlat Card(Color accent)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Row,
            BorderColor = accent.WithAlpha(0.75f),
            BorderThickness = new Thickness(3, 0, 0, 0),
        };
    }

    /// <summary>
    /// The frame around a text box or line edit.
    /// </summary>
    public static StyleBoxFlat InputFrame()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Input,
            BorderColor = Line,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };
    }

    /// <summary>
    /// Shows a text box's scroll bar only while its text doesn't fit; the engine always shows it. Call every frame,
    /// the text box works out its size while drawing.
    /// </summary>
    public static void FitScrollBar(TextEdit edit)
    {
        foreach (var child in edit.Children)
        {
            if (child is VScrollBar bar)
                bar.Visible = bar.MaxValue > bar.Page + 1f;
        }
    }

    /// <summary>
    /// A line edit in the tab's flat style.
    /// </summary>
    public static LineEdit LineEdit()
    {
        return new LineEdit { StyleBoxOverride = InputFrame() };
    }
}
