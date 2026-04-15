using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Base class for REST API responses.
/// </summary>
internal abstract class RestResponseBase
{
    /// <summary><c>true</c> if the API call succeeded, <c>false</c> otherwise.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; }

    /// <summary>If <see cref="Success"/> is <c>true</c>, this contains the result of the API call; otherwise this is <c>null</c>.</summary>
    [JsonPropertyName("data")]
    public object? Data { get; }

    /// <summary>If <see cref="Success"/> is <c>false</c>, this is the error message; otherwise this is <c>null</c>.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="success"><c>true</c> if the API call succeeded, <c>false</c> otherwise.</param>
    /// <param name="data">If <see cref="Success"/> is <c>true</c>, this contains the result of the API call; otherwise this is <c>null</c>.</param>
    /// <param name="error">If <see cref="Success"/> is <c>false</c>, this is the error message; otherwise this is <c>null</c>.</param>
    protected RestResponseBase(bool success, object? data, string? error)
    {
        this.Success = success;
        this.Data = data;
        this.Error = error;
    }
}