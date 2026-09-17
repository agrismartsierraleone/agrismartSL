namespace AgrismartAPI.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();

    public override bool Equals(object? obj)
    {
        return obj is Entity other && other.GetType() == GetType() && other.Id == Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
