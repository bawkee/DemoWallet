namespace Wallet.WebApp
{
	using System.Net;
	using System.Threading.Tasks;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Diagnostics;
	using Microsoft.AspNetCore.Http;

	public static class ExceptionReporter
	{
		public static void RunErrorApp(IApplicationBuilder app) => app.Run(HandleException);

		// Smallest app in the world, most basic exception reporting... All the exceptions are going to be logged by default,
		// but that can be customized as well through middleware.
		private static async Task HandleException(HttpContext context)
		{
			context.Response.ContentType = "application/text";
			context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

			var ex = context.Features.Get<IExceptionHandlerPathFeature>().Error;

			// In real world, there would be more information and exception filtering here
			await context.Response.WriteAsync(ex.Message);
		}
	}
}
