using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Model.Po_Tasks
{
    /// <summary>
    /// Where a task card goes when its title is clicked: which page, and which row to open
    /// there. sp_GetPoTask derives the id per stage — the yarn order for a BOM task, the
    /// plan line for a Planning task — and hands it over as LinkId, so this only has to map
    /// stage to route. One place to add the next stage.
    /// </summary>
    public class PoTaskLinkModel
    {
        public const byte BomStage      = 2;
        public const byte PlanningStage = 3;

        public byte Stage { get; set; }

        /// <summary>The real record id on the destination page (yarn order / plan line).</summary>
        public int? TargetId { get; set; }

        public string? Route { get; set; }
        public string? Tooltip { get; set; }

        /// <summary>
        /// False for stages with no page of their own, and for older rows whose RefId was
        /// never filled in — those cards keep their plain title and status-drawer click.
        /// </summary>
        public bool CanNavigate => TargetId is > 0 && !string.IsNullOrWhiteSpace(Route);

        public static PoTaskLinkModel For(PoTaskCardDto card) => card.Stage switch
        {
            BomStage => new PoTaskLinkModel
            {
                Stage    = card.Stage,
                TargetId = card.LinkId,
                Route    = $"/yarn-orders/{card.LinkId}",
                Tooltip  = $"Open the yarn order for {card.OrderNo}"
            },

            // PlanningStage: LinkId already carries the MasterPlanChildId, but /order-planning
            // has no route parameter or "select this plan line" entry point yet, so linking
            // here would just 404. Add the case once that page can take an id.

            _ => new PoTaskLinkModel { Stage = card.Stage }
        };
    }
}
