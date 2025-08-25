namespace ErikLieben.FA.ES.Projections;

public interface IVersionTokenComparer : IComparer<string>
{
    bool IsNewer(string @new, string? existing);

    bool IsOlder(string @new, string existing);
}