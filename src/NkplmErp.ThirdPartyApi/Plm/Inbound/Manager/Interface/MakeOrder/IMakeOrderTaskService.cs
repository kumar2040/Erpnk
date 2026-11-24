using NkplmErp.Shared.DTOs.MakeOrder;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.ThirdPartyApi.Plm.Inbound.Manager.Interface.MakeOrder;

/// <summary>
/// New order placed in PLM ("Make order") -> one task on the /tasks board plus a notification for the
/// Production Manager(s). All-or-nothing: sp_PoTask_MakeOrder (@Flag = MAKE_ORDER) runs it in one transaction.
/// </summary>
public interface IMakeOrderTaskService
{
    Task<IResponse<MakeOrderTaskResultDto>> CreateMakeOrderTaskAsync(MakeOrderTaskRequest request);
}
