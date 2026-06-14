using Microsoft.AspNetCore.Components;

namespace BlazorRichTextEditor;

// Sanitization plumbing. The foundation establishes the payload seam (null = the bridge's
// secure default allowlist). Phase 3 adds the SanitizationPolicy parameter and a full
// allowlist payload.
public partial class BlazorRichTextEditor
{
    /// <summary>
    /// Allowlist policy applied to all content. When null the bridge applies a secure
    /// default allowlist.
    /// </summary>
    [Parameter] public RichTextSanitizationPolicy? SanitizationPolicy { get; set; }

    /// <summary>Builds the policy object sent to the JS bridge, or null for the default.</summary>
    private object? BuildPolicyPayload()
    {
        if (SanitizationPolicy is null) return null;
        return new
        {
            allowedTags = SanitizationPolicy.AllowedTags.Select(t => t.ToLowerInvariant()).ToArray(),
            allowedAttributes = SanitizationPolicy.AllowedAttributes
                .ToDictionary(
                    kv => kv.Key.ToLowerInvariant(),
                    kv => kv.Value.Select(a => a.ToLowerInvariant()).ToArray()),
            allowedUriSchemes = SanitizationPolicy.AllowedUriSchemes.Select(s => s.ToLowerInvariant()).ToArray(),
            allowDataImageUris = SanitizationPolicy.AllowDataImageUris
        };
    }
}
