namespace Wallet.Api.MockDb.AspNetCore
{
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Hosting;
	using MockDb;

	// This service runs our in-memory db
	public class MockDbService : IHostedService
	{
		private readonly MockDbConnectionProvider _provider;

		public MockDbService(MockDbConnectionProvider provider) => _provider = provider;

		public Task StartAsync(CancellationToken cancellationToken) => _provider.InitializeAsync(cancellationToken);
		
		public Task StopAsync(CancellationToken cancellationToken) => _provider.DisposeAsync().AsTask();
	}
}
