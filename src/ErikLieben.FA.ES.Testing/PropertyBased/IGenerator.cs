namespace ErikLieben.FA.ES.Testing.PropertyBased;

/// <summary>
/// Defines a generator for producing random values of a specific type.
/// </summary>
/// <typeparam name="T">The type of value to generate.</typeparam>
public interface IGenerator<out T>
{
    /// <summary>
    /// Generates a random value of type T.
    /// </summary>
    /// <param name="random">The random number generator to use.</param>
    /// <returns>A randomly generated value.</returns>
    T Generate(Random random);
}
