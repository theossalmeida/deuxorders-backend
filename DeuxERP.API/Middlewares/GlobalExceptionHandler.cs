using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.API.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception: {ExceptionType} — {Message}", exception.GetType().Name, exception.Message);
            if (exception is InvalidOperationException or ArgumentException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Erro de Validação de Negócio",
                    Detail = exception.Message
                }, cancellationToken);

                return true;
            }

            if (exception is DbUpdateConcurrencyException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflito de Concorrência",
                    Detail = "Este registro foi modificado por outra operação. Recarregue e tente novamente."
                }, cancellationToken);

                return true;
            }

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro Interno do Servidor",
                Detail = "Ocorreu um erro inesperado. Tente novamente mais tarde."
            }, cancellationToken);

            return true;
        }
    }
}