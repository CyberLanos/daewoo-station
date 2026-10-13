// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Content.Client.Inventory;
using Content.Shared._Pirate;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Pirate.StationRecords;

/// <summary>
/// Which records console a mugshot is for; picks its background.
/// </summary>
public enum RecordPhotoStyle : byte
{
    /// <summary>Criminal records: height chart.</summary>
    Security,

    /// <summary>Crew records: studio grey with the NT logo.</summary>
    Civilian,

    /// <summary>Psionics registry (Mantis): purple backdrop.</summary>
    Psionics,
}

/// <summary>
/// What goes on the mugshot placard. <paramref name="BirthYear"/> 0 = unknown.
/// </summary>
public readonly record struct MugshotLabel(string Name, int Number, int BirthYear);

/// <summary>
/// Off-screen rendering for record photos, entirely in memory (nothing is written to the player's disk):
/// <list type="bullet">
/// <item><see cref="RenderEntity"/> - a bare render of a character dummy, what the records store;</item>
/// <item><see cref="Compose"/> - a console's mugshot of such a render: its background, the character at 6x with its
/// feet on the bottom edge, and a name placard over the lower body. 240x240 - the console photo box, shown 1:1.</item>
/// </list>
/// Drawn through render targets: the sandbox has no other way to draw an entity or put text into an image.
/// </summary>
public sealed class RecordPhotoComposerSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ClientInventorySystem _inventory = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public const int PhotoSize = 240;

    /// <summary>
    /// Record photo dress code: no headwear and nothing covering the face. Taken off the photo dummy before it is rendered.
    /// </summary>
    private static readonly string[] PhotoHiddenSlots = ["head", "mask", "eyes"];
    private const int CharacterScale = 6;

    private static readonly ResPath BackgroundRsi = new("/Textures/_Pirate/Interface/record_photos.rsi");
    private static readonly ResPath PlacardFont = new("/Fonts/unifont-17.0.03.otf");

    private readonly ComposerControl _control = new();
    private Font? _font;

    public override void Initialize()
    {
        base.Initialize();
        _ui.RootControl.AddChild(_control);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        while (_control.Queue.TryDequeue(out var job))
        {
            job.Target.Dispose();
            job.Cleanup?.Invoke();
            job.Result.TrySetCanceled();
        }

        _ui.RootControl.RemoveChild(_control);
    }

    public Texture GetBackground(RecordPhotoStyle style)
    {
        return _sprite.Frame0(new SpriteSpecifier.Rsi(BackgroundRsi, StateFor(style)));
    }

    /// <summary>
    /// Composes a mugshot from a bare character render. Completes a frame or two later (the pixels are read back
    /// from the GPU asynchronously); null if the render can't be read.
    /// </summary>
    public Task<byte[]?> Compose(byte[] renderPng, RecordPhotoStyle style, MugshotLabel label)
    {
        Texture character;
        try
        {
            using var stream = new MemoryStream(renderPng, writable: false);
            character = Texture.LoadFromPNGStream(stream, "record-mugshot-character");
        }
        catch (Exception)
        {
            return Task.FromResult<byte[]?>(null);
        }

        // Unifont is a 16px bitmap font: only crisp at exactly 16px. Font sizes are points at the display font DPI
        // (16pt came out ~21px and overflowed the plate), so ask for 12pt (16px at 96 DPI) and cancel any custom DPI.
        var font = _font ??= new VectorFont(_resourceCache.GetResource<FontResource>(PlacardFont), 12);
        var fontScale = 96f / Math.Max(1, _cfg.GetCVar(CVars.DisplayFontDpi));
        var background = GetBackground(style);

        return Render(new Vector2i(PhotoSize, PhotoSize),
            handle => DrawMugshot(handle, background, character, font, fontScale, label),
            () => DisposeTexture(character));
    }

    /// <summary>
    /// Applies the record photo dress code to a photo dummy (a client-side profile dummy, never a real mob):
    /// takes off headwear, masks and glasses, as the lobby does when it swaps loadout gear.
    /// </summary>
    public void DressForPhoto(EntityUid dummy)
    {
        foreach (var slot in PhotoHiddenSlots)
        {
            if (_inventory.TryUnequip(dummy, slot, out var removed, silent: true, force: true, reparent: false))
                Del(removed.Value);
        }
    }

    /// <summary>
    /// Renders an entity facing south into a PNG, the same picture the sprite export makes, without the file.
    /// Null if it has no visible sprite. The entity has to stay alive until the task completes.
    /// </summary>
    public Task<byte[]?> RenderEntity(EntityUid entity)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return Task.FromResult<byte[]?>(null);

        // Same canvas as ContentSpriteSystem.Export: the largest visible layer.
        var size = Vector2i.Zero;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer.Visible)
                size = Vector2i.ComponentMax(size, layer.PixelSize);
        }

        if (size == Vector2i.Zero)
            return Task.FromResult<byte[]?>(null);

        return Render(size, handle =>
        {
            if (!Deleted(entity))
                handle.DrawEntity(entity, size / 2, Vector2.One, Angle.Zero, overrideDirection: Direction.South);
        });
    }

    private Task<byte[]?> Render(Vector2i size, Action<DrawingHandleScreen> draw, Action? cleanup = null)
    {
        var target = _clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "record-photo");
        var result = new TaskCompletionSource<byte[]?>();
        _control.Queue.Enqueue(new RenderJob(target, draw, result, cleanup));
        return result.Task;
    }

    private static string StateFor(RecordPhotoStyle style)
    {
        return style switch
        {
            RecordPhotoStyle.Security => "security",
            RecordPhotoStyle.Psionics => "psionics",
            _ => "civilian",
        };
    }

    private static void DisposeTexture(Texture texture)
    {
        if (texture is OwnedTexture owned)
            owned.Dispose();
    }

    private sealed record RenderJob(
        IRenderTexture Target,
        Action<DrawingHandleScreen> Draw,
        TaskCompletionSource<byte[]?> Result,
        Action? Cleanup);

    /// <summary>
    /// Render targets are drawn during the UI draw pass, so the jobs wait here for the next frame.
    /// Sits on the UI root: no clipping, and its transform is reset before drawing anyway.
    /// </summary>
    private sealed class ComposerControl : Control
    {
        public readonly Queue<RenderJob> Queue = new();

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            while (Queue.TryDequeue(out var job))
            {
                try
                {
                    handle.RenderInRenderTarget(job.Target, () =>
                    {
                        var oldTransform = handle.GetTransform();
                        handle.SetTransform(Matrix3x2.Identity);
                        job.Draw(handle);
                        handle.SetTransform(oldTransform);
                    }, Color.Transparent);

                    job.Target.CopyPixelsToMemory<Rgba32>(image =>
                    {
                        try
                        {
                            using var png = new MemoryStream();
                            image.SaveAsPng(png);
                            job.Result.TrySetResult(png.ToArray());
                        }
                        catch (Exception e)
                        {
                            job.Result.TrySetException(e);
                        }
                        finally
                        {
                            job.Target.Dispose();
                        }
                    });
                }
                catch (Exception e)
                {
                    job.Target.Dispose();
                    job.Result.TrySetException(e);
                }
                finally
                {
                    // The draw is flushed by the end of RenderInRenderTarget.
                    job.Cleanup?.Invoke();
                }
            }
        }
    }

    private static void DrawMugshot(DrawingHandleScreen handle, Texture background, Texture character, Font font, float fontScale, MugshotLabel label)
    {
        handle.DrawTextureRect(background, UIBox2.FromDimensions(0, 0, PhotoSize, PhotoSize));

        // Feet on the bottom edge. Renders are normally 32x32; anything bigger is scaled down to fit.
        var size = character.Size;
        var scale = Math.Clamp(PhotoSize / Math.Max(size.X, size.Y), 1, CharacterScale);
        var width = size.X * scale;
        var height = size.Y * scale;
        handle.DrawTextureRect(character,
            UIBox2.FromDimensions((PhotoSize - width) / 2, PhotoSize - height, width, height));

        DrawPlacard(handle, font, fontScale, label);
    }

    /// <summary>
    /// Name over "#number ██.██.birth-year", held at knee height. The birth year is the in-game year minus the
    /// record's age; nobody enters a birthday, so day and month are redacted.
    /// </summary>
    private static void DrawPlacard(DrawingHandleScreen handle, Font font, float scale, MugshotLabel label)
    {
        const char block = '\u2588';
        const int padX = 8;
        const int padY = 4;
        const int lineGap = 2;
        const int kneeHeight = 192;

        // Second line: number and date of birth one space apart, centred. The plate is sized as if they were three
        // characters apart, so the pair has some room to either side; it is the same width for everyone, so every
        // placard is the same compact size.
        var number = $"#{label.Number:D6}";
        var year = label.BirthYear > 0
            ? label.BirthYear.ToString()
            : new string(block, 4);
        var birth = $"{block}{block}.{block}{block}.{year}";

        var numberWidth = handle.GetDimensions(font, number, scale).X;
        var birthWidth = handle.GetDimensions(font, birth, scale).X;
        var plateGap = handle.GetDimensions(font, "   ", scale).X;
        var gap = handle.GetDimensions(font, " ", scale).X;
        var textWidth = MathF.Ceiling(numberWidth + plateGap + birthWidth);
        var lineHeight = MathF.Round(font.GetLineHeight(scale));

        // Longer names are cut to fit; the full name is next to the photo and in the printed photo's title.
        var name = label.Name.ToUpperInvariant();
        while (name.Length > 1 && handle.GetDimensions(font, name, scale).X > textWidth)
        {
            name = name[..^2] + '\u2026';
        }

        var nameWidth = handle.GetDimensions(font, name, scale).X;
        var width = textWidth + padX * 2;
        var height = padY * 2 + lineHeight * 2 + lineGap;
        var left = MathF.Round((PhotoSize - width) / 2);
        var top = MathF.Round(kneeHeight - height / 2);

        var box = UIBox2.FromDimensions(left, top, width, height);
        handle.DrawRect(box, Color.FromHex("#121216"));
        handle.DrawRect(box, Color.FromHex("#E1E1E1"), filled: false);
        handle.DrawRect(new UIBox2(box.Left + 1, box.Top + 1, box.Right - 1, box.Bottom - 1), Color.FromHex("#E1E1E1"), filled: false);

        // Whole pixels, or the bitmap glyphs get resampled.
        var secondLine = top + padY + lineHeight + lineGap;
        handle.DrawString(font, new Vector2(MathF.Round((PhotoSize - nameWidth) / 2), top + padY), name, scale, Color.FromHex("#F0F0F0"));
        var numberLeft = MathF.Round((PhotoSize - (numberWidth + gap + birthWidth)) / 2);
        handle.DrawString(font, new Vector2(numberLeft, secondLine), number, scale, Color.FromHex("#C3C3C3"));
        handle.DrawString(font, new Vector2(MathF.Round(numberLeft + numberWidth + gap), secondLine), birth, scale, Color.FromHex("#C3C3C3"));
    }
}
