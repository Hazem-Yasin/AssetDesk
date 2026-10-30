namespace AssetDesk.Domain.Enums;

public enum UserRole
{
    Employee = 0,
    Admin = 1
}

public enum AssetStatus
{
    Available = 0,
    CheckedOut = 1,
    InMaintenance = 2,
    Retired = 3
}

public enum AssetCategory
{
    Laptop = 0,
    Desktop = 1,
    Monitor = 2,
    Phone = 3,
    Peripheral = 4,
    Other = 5
}
