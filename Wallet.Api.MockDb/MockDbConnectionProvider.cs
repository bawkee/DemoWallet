namespace Wallet.Api.MockDb
{
	using System;
	using System.Data.Common;
	using System.Data.SQLite;
	using System.Threading;
	using System.Threading.Tasks;
	using Abstractions;
	using Dapper;

	public class MockDbConnectionProvider : IAsyncDisposable
	{
		private SQLiteConnection _connection;
		private readonly TaskCompletionSource<DbConnection> _initConnectionTcs = new ();

		public async Task InitializeAsync(CancellationToken ct)
		{
			if (_connection != null)
				return;

			_connection = new ("FullUri=file:demo?mode=memory&cache=shared;");

			await _connection.OpenAsync(ct);

			foreach (var pragma in new[]
			{
				"temp_store=2", // In-memory temp tables
				"synchronous=0", // No sync needed here
				"journal_mode=memory" // In-memory journaling
			})
			{
				await _connection.ExecuteAsync($"pragma {pragma}");
			}

			await _connection.ExecuteAsync("CREATE TABLE PLAYER (" +
										   "	IDPLAYER TEXT PRIMARY KEY, " +
										   "	USERNAME TEXT UNIQUE NOT NULL," +
										   "	FOREIGN KEY (IDPLAYER) REFERENCES PLAYERWALLET (IDPLAYER) " +
										   "		ON DELETE CASCADE " +
										   "		ON UPDATE NO ACTION)");
			await _connection.ExecuteAsync("CREATE UNIQUE INDEX IDX_PLAYER_IDPLAYER ON PLAYER (IDPLAYER ASC)");

			await _connection.ExecuteAsync("CREATE TABLE PLAYERWALLET (" +
										   "	IDPLAYER TEXT PRIMARY KEY," +
										   "	BALANCE DOUBLE NOT NULL DEFAULT 0)");
			await _connection.ExecuteAsync("CREATE UNIQUE INDEX IDX_PLAYERWALLET_IDPLAYER ON PLAYERWALLET (IDPLAYER ASC)");

			await _connection.ExecuteAsync("CREATE TABLE TRANS (" +
										   "	IDTRANSACTION TEXT PRIMARY KEY, " +
										   "	IDREF TEXT, " +
										   "	REFTYPE TEXT, " +
										   "	AMOUNT DOUBLE, " +
										   "	TRANSACTIONTYPE TEXT)");
			await _connection.ExecuteAsync("CREATE UNIQUE INDEX IDX_TRANS_IDTRANSACTION ON TRANS (IDTRANSACTION ASC)");
			await _connection.ExecuteAsync("CREATE INDEX IDX_TRANS_IDREF ON TRANS (IDREF ASC)");
			
			_initConnectionTcs.SetResult(_connection);
		}

		public async Task<DbConnection> GetDbConnectionAsync()
		{
			await _initConnectionTcs.Task; // The primary connection which serves the db
			var connection = new SQLiteConnection("FullUri=file:demo?mode=memory&cache=shared;");
			await connection.OpenAsync();
			return connection;
		}

		public ValueTask DisposeAsync() => _connection.DisposeAsync();
	}
}