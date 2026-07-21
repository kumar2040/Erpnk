namespace NkplmErp.API.Model.TaskManagement
{
    // One point of a knitter's return series: how many pieces (item_no) were received
    // at a given date+time. Returned by spTaskManagement @Flag='KD' for the In Progress
    // return-detail modal chart.
    public class KnitterReturnPointResponseModel
    {
        public DateTime ReturnAt { get; set; }
        public int ReturnCount { get; set; }
    }
}
