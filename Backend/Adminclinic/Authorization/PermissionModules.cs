namespace AuthDemo.Authorization;

/// <summary>
/// Central list of modules controlled by Doctor/Receptionist permissions.
/// When a permission row does not yet exist, access defaults to TRUE.
/// An Admin can still explicitly save false values for any module/action.
/// </summary>
public static class PermissionModules
{
    public static readonly string[] All =
    {
        "Dashboard",
        "Appointments",
        "Patients",
        "Billing",
        "Reports",
        "Schedule",
        "Prescriptions",
        "Vitals",
        "NursingNotes"
    };
}
