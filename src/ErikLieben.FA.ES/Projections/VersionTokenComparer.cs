namespace ErikLieben.FA.ES.Projections;

public class VersionTokenComparer : IVersionTokenComparer
{
    public int Compare(string? x, string? y)
    {
        x = x?.ToLowerInvariant();
        y = y?.ToLowerInvariant();

        if (x == y)
        {
            return 0;
        }

        if (x == null && y != null)
        {
            return -1;
        }

        if (x != null && y == null)
        {
            return 1;
        }

        var xIdentifier = new VersionToken(x!);
        var yIdentifier = new VersionToken(y!);

        if (xIdentifier.ObjectIdentifier != yIdentifier.ObjectIdentifier)
        {
            throw new Exceptions.VersionTokenStreamMismatchException(xIdentifier.ObjectIdentifier.Value, yIdentifier.ObjectIdentifier.Value);
        }

        return string.Compare(xIdentifier.VersionIdentifier.Value, yIdentifier.VersionIdentifier.Value, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsNewer(string @new, string? existing)
    {
        if (existing == null)
        {
            return true;
        }

        //   Greater than zero, x is greater than y.
        return Compare(@new, existing) > 0;
    }

    public bool IsOlder(string @new, string? existing)
    {
        //   Greater than zero, x is greater than y.
        return Compare(@new, existing) < 0;
    }
}
