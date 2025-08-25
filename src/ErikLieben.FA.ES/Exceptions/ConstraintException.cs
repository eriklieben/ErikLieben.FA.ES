namespace ErikLieben.FA.ES.Exceptions;

[Serializable]
public class ConstraintException : Exception
{

    public Constraint Constraint { get; protected set; }

    public ConstraintException(string message, Constraint constraint) : base(message)
    {
        Constraint = constraint;
    }
}
