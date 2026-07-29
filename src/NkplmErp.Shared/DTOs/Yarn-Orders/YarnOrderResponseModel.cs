namespace NkplmErp.Shared.DTOs.Yarn_Orders
{
    public class YarnOrderResponseModel
    {
        public int UpdatedCount { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Invoice save only: true when this invoice was the LAST one outstanding, so the
        /// whole yarn order is now received. Always false on the timeline (date) path.
        /// </summary>
        public bool HeaderCompleted { get; set; }

        /// <summary>Planning tasks raised by this save (one per production order behind the yarn order).</summary>
        public int TaskCount { get; set; }

        /// <summary>
        /// Open yarn-lifecycle tasks (Stage 12) this save closed — the "placed / departure /
        /// arriving" chases that the yarn actually arriving made obsolete.
        /// </summary>
        public int ClosedTaskCount { get; set; }
    }
}
