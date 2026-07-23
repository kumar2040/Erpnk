using System.Data;
using NkplmErp.Application.Interfaces.Dropdown;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.DTOs.Dropdown;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Infrastructure.Services.Dropdown
{
    public class DropdownService : IDropdownService
    {
        private readonly IGenericRepository _genericRepository;

        public DropdownService(IGenericRepository genericRepository)
        {
            _genericRepository = genericRepository;
        }

        // spDropdown owns which options exist, so there is nothing to decide here --
        // the type goes down, the rows come back. An unknown type returns an empty
        // list rather than an error, which the control renders as a dropdown holding
        // only its leading row.
        public async Task<IResponse<List<DropDownListModel>>> GetDropDownListAsync(
            string dropDownType, string? filter1, string? filter2)
        {
            try
            {
                var rows = await _genericRepository.GetQueryResultAsync<DropDownListModel>(
                    "spDropdown",
                    new
                    {
                        Type = dropDownType,
                        Filter1 = filter1,
                        Filter2 = filter2
                    },
                    CommandType.StoredProcedure);

                return Response<List<DropDownListModel>>.Success(rows ?? new());
            }
            catch (Exception ex)
            {
                return Response<List<DropDownListModel>>.Fail(ex.Message);
            }
        }
    }
}
