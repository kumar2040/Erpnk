using Microsoft.AspNetCore.Components;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Components;

public partial class StyleComponent : ComponentBase
{
    [Parameter]
    public StyleDetailsDto? Details { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }
}
