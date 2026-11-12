using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Role change form for one account. The actor never travels in the form; the service reads it
/// from <see cref="ASI.Basecode.Services.Interfaces.ICurrentActor"/>.
/// </summary>
public sealed class ChangeAccountRoleViewModel
{
    private string _reason;

    public int AccountId { get; set; }

    public string FullName { get; set; }

    public string Email { get; set; }

    public string CurrentRoleCode { get; set; }

    public string CurrentRoleLabel { get; set; }

    /// <summary>Lowercase role code from the fixed <c>roles</c> lookup.</summary>
    [Required(ErrorMessage = "Select a role.")]
    public string RoleCode { get; set; }

    /// <summary>
    /// Trimmed before validation so the 1–1000 character boundary applies to the trimmed value,
    /// matching the service rule. Both attributes keep their client-side validation rules.
    /// </summary>
    [Required(ErrorMessage = "A reason is required.")]
    [StringLength(1000, ErrorMessage = "The reason must be 1000 characters or fewer.")]
    public string Reason
    {
        get => _reason;
        set => _reason = value?.Trim();
    }

    /// <summary>Role choices for the select; never posted back.</summary>
    public IReadOnlyList<RoleOption> Roles { get; set; } = Array.Empty<RoleOption>();
}
