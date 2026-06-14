using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazorRichTextEditor;

// EditForm / EditContext integration. The foundation establishes the notify seam;
// Phase 3 wires up FieldIdentifier derivation and validation-message rendering.
public partial class BlazorRichTextEditor
{
    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// Identifies the bound model field, enabling <c>EditForm</c> validation. Set automatically
    /// when using <c>@bind-Value</c> on a model property.
    /// </summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    private FieldIdentifier _fieldIdentifier;
    private bool _hasField;

    private void EnsureField()
    {
        if (!_hasField && ValueExpression is not null)
        {
            _fieldIdentifier = FieldIdentifier.Create(ValueExpression);
            _hasField = true;
        }
    }

    /// <summary>Notifies the cascaded EditContext that the bound field changed.</summary>
    private void NotifyEditContextChanged()
    {
        if (CascadedEditContext is null) return;
        EnsureField();
        if (_hasField) CascadedEditContext.NotifyFieldChanged(_fieldIdentifier);
    }
}
