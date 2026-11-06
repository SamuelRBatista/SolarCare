using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;
using SolarCare.Domain.Operations;

namespace SolarCare.Api;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Não autenticado", "A autenticação é necessária."),
            RelatedResourceNotFoundException =>
                (StatusCodes.Status404NotFound, "Registro não encontrado", "O registro relacionado não foi encontrado nesta empresa."),
            DuplicateCustomerTaxIdentifierException =>
                (StatusCodes.Status409Conflict, "Registro duplicado", "Já existe um cliente com este CPF/CNPJ nesta empresa."),
            AccountAlreadyExistsException =>
                (StatusCodes.Status409Conflict, "Conta não disponível", "Não foi possível criar uma conta com os dados informados."),
            DomainRuleViolationException =>
                (StatusCodes.Status409Conflict, "Transição inválida", exception.Message),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Entrada inválida", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Não foi possível concluir a solicitação.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Request failed with status {StatusCode}", statusCode);
        }
        else
        {
            logger.LogWarning(exception, "Request rejected with status {StatusCode}", statusCode);
        }

        httpContext.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
