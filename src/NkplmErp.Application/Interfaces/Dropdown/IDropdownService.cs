using NkplmErp.Shared.DTOs.Dropdown;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Application.Interfaces.Dropdown
{
    public interface IDropdownService
    {
        // Real options for one named list. dropDownType is the spDropdown @Type;
        // the filters are optional cascade keys whose meaning depends on it.
        // No all/select flag: the leading "All" or "Select" row is added by the
        // control, so it never travels over the wire.
        Task<IResponse<List<DropDownListModel>>> GetDropDownListAsync(
            string dropDownType, string? filter1, string? filter2);
    }
}
