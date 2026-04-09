namespace DuneArrakis.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(decimal required, decimal available) 
        : base($"Saldo insuficiente. Necesita {required:N0} Solaris pero solo tiene {available:N0}.") { }
}

public class InvalidCreatureStateException : DomainException
{
    public InvalidCreatureStateException(string message) : base(message) { }
}

public class InvalidEntityStateException : DomainException
{
    public InvalidEntityStateException(string message) : base(message) { }
}

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object identifier) 
        : base($"No se encontró {entityName} con el identificador '{identifier}'.") { }
}
