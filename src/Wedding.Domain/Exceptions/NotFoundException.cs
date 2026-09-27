namespace Wedding.Domain.Exceptions;

/// <summary>
/// Ngoại lệ được ném khi không tìm thấy đối tượng trong hệ thống.
/// Sẽ được ánh xạ sang HTTP 404 bởi GlobalExceptionHandlingMiddleware.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string entityName, Guid id)
        : base($"Không tìm thấy {entityName} với Id = '{id}'.")
    {
    }

    public NotFoundException(string entityName, string key)
        : base($"Không tìm thấy {entityName} với khóa = '{key}'.")
    {
    }
}
