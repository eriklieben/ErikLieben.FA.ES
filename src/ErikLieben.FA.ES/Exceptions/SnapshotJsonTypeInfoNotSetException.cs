namespace ErikLieben.FA.ES.Exceptions;

public class SnapshotJsonTypeInfoNotSetException : Exception
{
    public SnapshotJsonTypeInfoNotSetException() 
        : base("Snapshot JsonInfo type should be set to deserialize the snapshot type")
    {
        
    }
}
