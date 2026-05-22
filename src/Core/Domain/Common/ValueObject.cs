namespace OS.Domain.Common;

/// <summary>
/// Base class for value objects in the domain layer.
/// Value objects are immutable and have no identity.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Compares two value objects for equality based on their atomic values.
    /// </summary>
    protected static bool EqualOperator(ValueObject? left, ValueObject? right)
    {
        if (ReferenceEquals(left, null) ^ ReferenceEquals(right, null))
            return false;

        return ReferenceEquals(left, null) || left.Equals(right);
    }

    /// <summary>
    /// Compares two value objects for inequality.
    /// </summary>
    protected static bool NotEqualOperator(ValueObject? left, ValueObject? right)
    {
        return !EqualOperator(left, right);
    }

    /// <summary>
    /// Gets the atomic values that define this value object's identity.
    /// </summary>
    protected abstract IEnumerable<object?> GetAtomicValues();

    /// <summary>
    /// Determines whether the specified value object is equal to the current value object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetAtomicValues().SequenceEqual(other.GetAtomicValues());
    }

    /// <summary>
    /// Serves as the default hash function for value objects.
    /// </summary>
    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Aggregate(1, (current, value) =>
            {
                return HashCode.Combine(current, value);
            });
    }
}
