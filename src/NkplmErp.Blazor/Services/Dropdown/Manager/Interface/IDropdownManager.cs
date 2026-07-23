using NkplmErp.Shared.DTOs.Dropdown;

namespace NkplmErp.Blazor.Services.Dropdown.Manager.Interface
{
    public interface IDropdownManager
    {
        // Real options for one named list, without any leading "All" / "Select"
        // row -- the control adds that. Returns an empty list rather than throwing,
        // so a dropdown whose call fails renders empty instead of breaking its page.
        Task<List<DropDownListModel>> GetDropDownListAsync(
            string dropDownType, string? filter1 = null, string? filter2 = null);
    }
}
