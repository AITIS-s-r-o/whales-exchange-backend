using System;

namespace WhalesExchangeBackend.SharedLib.Exceptions;

/// <summary>
/// Exception for cases when an Electrum RPC call fails.
/// </summary>
public class ElectrumRpcException : Exception
{
    /// <summary>Error code.</summary>
    public long Code { get; }

    /// <summary>Error message reported by Electrum RPC, or <c>null</c> if no error message is provided.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Error details, or <c>null</c> if no details are provided.</summary>
    public object? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ElectrumRpcException" /> class.
    /// </summary>
    public ElectrumRpcException() :
        base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ElectrumRpcException" /> class with a specified error message.
    /// </summary>
    /// <inheritdoc/>
    public ElectrumRpcException(string message) :
        base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ElectrumRpcException" /> class with a specified error message.
    /// </summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="details">Error details, or <c>null</c> if no details are provided.</param>
    public ElectrumRpcException(long code, string message, object? details) :
        base($"Electrum RPC Error [{code}]: {message}")
    {
        this.Code = code;
        this.ErrorMessage = message;
        this.Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ElectrumRpcException" /> class with a specified error message and a reference to the inner exception that is the cause of
    /// this exception.
    /// </summary>
    /// <inheritdoc/>
    public ElectrumRpcException(string message, Exception? innerException) :
        base(message, innerException)
    {
    }
}