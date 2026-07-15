namespace NkplmErp.Shared.DTOs;

/// <summary>A knitter with their assigned gauges and active state.</summary>
public class KnitterManagementDto
{
    public int CardNo { get; set; }
    public string KnitterName { get; set; } = string.Empty;
    public decimal? PRSalary { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Gauges this knitter can run (e.g. "7GG", "12GG", "handknit").</summary>
    public List<string> Gauges { get; set; } = new();

    /// <summary>Number of plans this knitter is currently assigned to (blocks delete).</summary>
    public int ActiveAssignments { get; set; }
}

/// <summary>Insert / update / delete / set-active request for a knitter.</summary>
public class SaveKnitterRequest
{
    public int CardNo { get; set; }
    public string KnitterName { get; set; } = string.Empty;
    public decimal? PRSalary { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Gauges { get; set; } = new();

    /// <summary>1=Insert, 2=Update, 3=Delete, 5=SetActive.</summary>
    public int Flag { get; set; }
}

/// <summary>Standard write-operation result returned by sp_ManageKnitter.</summary>
public class KnitterOperationResult
{
    public int Result { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? CardNo { get; set; }
    public bool IsSuccess => Result > 0;
}
