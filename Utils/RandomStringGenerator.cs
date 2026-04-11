using System.Security.Cryptography;

namespace WhalesExchangeBackend.Utils;

/// <summary>
/// Generator of random strings.
/// </summary>
public static class RandomStringGenerator
{
    /// <summary>Charset used by the random string generator.</summary>
    private static readonly char[] alphanumericChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    /// <summary>
    /// Generates a random alphanumeric string of specified length.
    /// </summary>
    /// <param name="length">Length of the random string to generate.</param>
    /// <returns>Random alphanumeric string of specified length.</returns>
    public static string Generate(int length)
    {
        char[] result = new char[length];

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[length];
            rng.GetBytes(randomBytes);

            for (int i = 0; i < length; i++)
            {
                int randomIndex = randomBytes[i] % alphanumericChars.Length;
                result[i] = alphanumericChars[randomIndex];
            }
        }

        return new string(result);
    }
}