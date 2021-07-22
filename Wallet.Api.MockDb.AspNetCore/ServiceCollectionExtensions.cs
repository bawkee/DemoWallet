namespace Wallet.Api.MockDb.AspNetCore
{
	using Abstractions;
	using Microsoft.Extensions.DependencyInjection;

	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddMockWalletApi(this IServiceCollection services) =>
			services.AddSingleton<MockDbConnectionProvider>()
					.AddHostedService<MockDbService>()
					.AddTransient<IWalletApi, MockWalletApi>();
	}
}
