namespace Moongazing.OrionKey;

/// <summary>Thrown when the system clock moves backwards beyond the Snowflake tolerance.</summary>
public sealed class OrionKeyClockException : Exception
{
    /// <summary>Initializes the exception with a message.</summary>
    public OrionKeyClockException(string message) : base(message) { }
}
