using System;

namespace WhalesExchangeBackend.SharedLib.Exceptions;

/// <summary>
/// Exception for cases when a database operation fails.
/// </summary>
public class DatabaseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseException" /> class.
    /// </summary>
    public DatabaseException() :
        base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseException" /> class with a specified error message.
    /// </summary>
    /// <inheritdoc/>
    public DatabaseException(string message) :
        base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseException" /> class with a specified error message and a reference to the inner exception that is the cause of
    /// this exception.
    /// </summary>
    /// <inheritdoc/>
    public DatabaseException(string message, Exception? innerException) :
        base(message, innerException)
    {
    }
}