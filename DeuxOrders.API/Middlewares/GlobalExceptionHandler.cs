using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DeuxOrders.API.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is InvalidOperationException businessException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Erro de Validação de Negócio",
                    Detail = businessException.Message
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