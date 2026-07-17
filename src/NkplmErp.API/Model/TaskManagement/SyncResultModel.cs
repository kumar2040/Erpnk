namespace NkplmErp.API.Model.TaskManagement
{
    // Result of sp_SyncKnitterRecords: how many new rows were pulled from MySQL.
    public class SyncResultModel
    {
        public int InsertedData { get; set; }    // tbl_knitter_record_data
        public int InsertedRecord { get; set; }  // tbl_knitter_record
        public bool Ran { get; set; }             // false => a sync was already running (skipped)
        public string Message { get; set; } = "";
    }
}
