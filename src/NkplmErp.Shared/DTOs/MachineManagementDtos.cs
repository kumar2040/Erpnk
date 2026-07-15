namespace NkplmErp.Shared.DTOs;

/// <summary>A knit machine with its (single) gauge and active state.</summary>
public class MachineManagementDto
{
    public int MachineId { get; set; }
    public string MachineNo { get; set; } = string.Empty;
    public string? Gauge { get; set; }
    public string? Size { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Number of plans referencing this machine (blocks delete).</summary>
    public int ActivePlans { get; set; }
}

/// <summary>Insert / update / delete / set-active request for a machine.</summary>
public class SaveMachineRequest
{
    public int MachineId { get; set; }
    public string MachineNo { get; set; } = string.Empty;
    public string? Gauge { get; set; }
    public string? Size { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>1=Insert, 2=Update, 3=Delete, 5=SetActive.</summary>
    public int Flag { get; set; }
}

/// <summary>Standard write-operation result returned by sp_ManageMachine.</summary>
public class MachineOperationResult
{
    public int Result { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? MachineId { get; set; }
    public bool IsSuccess => Result > 0;
}
