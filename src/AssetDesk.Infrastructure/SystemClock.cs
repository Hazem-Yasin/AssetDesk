using AssetDesk.Application.Abstractions;

namespace AssetDesk.Infrastructure;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
