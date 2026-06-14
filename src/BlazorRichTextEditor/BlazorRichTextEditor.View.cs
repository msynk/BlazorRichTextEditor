using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Phase 4: full-screen mode, text direction, theming.
public partial class BlazorRichTextEditor
{
    /// <summary>Editor color theme.</summary>
    [Parameter] public RichTextTheme Theme { get; set; } = RichTextTheme.System;

    /// <summary>Overall text direction for the editor. Null follows the document direction.</summary>
    [Parameter] public TextDirection? Direction { get; set; }

    /// <summary>Localized labels/tooltips provider. Null uses built-in English labels.</summary>
    [Parameter] public IRichTextLocalizer? Localizer { get; set; }

    private bool _fullScreen;

    private string? RootDir => Direction switch
    {
        TextDirection.Rtl => "rtl",
        TextDirection.Ltr => "ltr",
        _ => null
    };

    private string ThemeAttr => Theme switch
    {
        RichTextTheme.Dark => "dark",
        RichTextTheme.Light => "light",
        _ => ""
    };

    private async Task ToggleFullScreen()
    {
        _fullScreen = !_fullScreen;
        StateHasChanged();
        if (_module is not null)
            await _module.InvokeVoidAsync("setFullScreen", _editor, _fullScreen);
    }

    private async Task SetDirectionAsync(string dir)
    {
        if (ReadOnly || _module is null) return;
        await _module.InvokeVoidAsync("setBlockDirection", _editor, dir);
    }

    private string Label(string key, string fallback)
        => Localizer is null ? fallback : (Localizer[key] ?? fallback);
}

/// <summary>Provides localized labels and tooltips for the editor's controls.</summary>
public interface IRichTextLocalizer
{
    /// <summary>Returns the localized string for the given key, or null to use the default.</summary>
    string? this[string key] { get; }
}
