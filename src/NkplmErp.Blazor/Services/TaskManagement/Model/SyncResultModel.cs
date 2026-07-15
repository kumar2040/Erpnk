namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Result of the MySQL -> SQL Server sync (sp_SyncKnitterRecords).
    public class SyncResultModel
    {
        public int InsertedData { get; set; }    // tbl_knitter_record_data
        public int InsertedRecord { get; set; }  // tbl_knitter_record
        public bool Ran { get; set; }             // false => a sync was already running
        public string Message { get; set; } = "";

        public int Total => InsertedData + InsertedRecord;
    }
}
