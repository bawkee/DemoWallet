namespace Wallet.Api.MockDb
{
	using System;
	using System.Collections.Generic;
	using System.Data.Common;
	using System.Linq;
	using System.Threading.Tasks;
	using Abstractions;
	using Abstractions.Model;
	using Dapper;

	public class MockWalletApi : IWalletApi
	{
		private readonly MockDbConnectionProvider _connectionProvider;

		public MockWalletApi(MockDbConnectionProvider connectionProvider)
		{
			_connectionProvider = connectionProvider;
		}

		public async Task<string> RegisterPlayerAsync(string username)
		{
			await using var connection = await _connectionProvider.GetDbConnectionAsync();

			if (await connection.ExecuteScalarAsync<bool>("SELECT COUNT(*) FROM PLAYER WHERE USERNAME = :username", new {username}))
				throw new ArgumentException("Player already exists", nameof(username));

			var player = new Player {IdPlayer = Guid.NewGuid().ToString(), Username = username};

			await connection.ExecuteAsync("INSERT INTO PLAYER (IDPLAYER, USERNAME) VALUES (@IdPlayer, @Username)", player);

			return player.IdPlayer;
		}

		public async Task<PlayerWallet> GetPlayerWalletAsync(string idPlayer)
		{
			await using var connection = await _connectionProvider.GetDbConnectionAsync();

			await VerifyPlayer(connection, idPlayer);

			var wallet = await connection.QueryFirstOrDefaultAsync<PlayerWallet>(
				"SELECT * FROM PLAYERWALLET WHERE IDPLAYER = @IdPlayer", new {idPlayer});

			return wallet == default ? new() {IdPlayer = idPlayer} : wallet;
		}

		public async Task<Transaction[]> GetPlayerTransactionsAsync(string idPlayer)
		{
			await using var connection = await _connectionProvider.GetDbConnectionAsync();

			await VerifyPlayer(connection, idPlayer);

			return (await connection.QueryAsync<Transaction>(
					"SELECT * FROM TRANS WHERE REFTYPE = 'Player' AND IDREF = @IdPlayer", new {idPlayer}))
				.ToArray();
		}

		public async Task RegisterPlayerTransactionAsync(string idPlayer, string type, decimal amount)
		{
			if (amount <= 0)
				throw new ArgumentException("Invalid amount.");

			await using var connection = await _connectionProvider.GetDbConnectionAsync();

			// Simulate a random wait, just for testing. In real-world mocks we would use Rx test schedulers (System.Reactive) in
			// which case there's no actual wait during tests, time itself is simulated so you can test against any anomaly or
			// parallelism bugs (sadly TPL does not have a good implementation of this).
			var rndWait = new Random().Next(1, 25);
			await Task.Delay(rndWait);
			
			await using var dbTrans = await connection.BeginTransactionAsync();

			try
			{
				await VerifyPlayer(connection, idPlayer);

				// Normally there would be a stored procedure for things like these, especially when dealing with money. Either way
				// we'd certainly use 'select ... for update' but in Sqlite that's redundant as its isolation level is serializable.
				var balance = await connection.ExecuteScalarAsync<decimal?>(
					"SELECT BALANCE FROM PLAYERWALLET WHERE IDPLAYER = @idPlayer", new {idPlayer}) ?? 0;

				var increment = amount;

				if (type is TransactionTypes.Stake)
				{
					if (balance < amount)
						throw new ArgumentException("Insufficient funds.", nameof(amount));

					increment = -amount;
				}
				else if (type is not (TransactionTypes.Deposit or TransactionTypes.Win))
				{
					throw new ArgumentException("Invalid transaction type.", nameof(type));
				}

				// Correct the balance
				var newBalance = balance + increment;

				await connection.ExecuteAsync(
					"INSERT INTO PLAYERWALLET (IDPLAYER, BALANCE) VALUES(@IdPlayer, @Balance) " +
					"ON CONFLICT (IDPLAYER) DO UPDATE SET BALANCE = @Balance",
					new
					{
						IdPlayer = idPlayer,
						Balance = newBalance
					});

				// Post the transaction
				var trans = new Transaction
				{
					IdTransaction = Guid.NewGuid().ToString(),
					IdRef = idPlayer,
					RefType = "Player",
					Amount = amount,
					TransactionType = type
				};

				await connection.ExecuteAsync("INSERT INTO TRANS (IDTRANSACTION, IDREF, REFTYPE, AMOUNT, TRANSACTIONTYPE) " +
											  "VALUES (@IdTransaction, @IdRef, @RefType, @Amount, @TransactionType)", trans);

				await dbTrans.CommitAsync();
			}
			catch
			{
				await dbTrans.RollbackAsync();
				throw;
			}
		}

		private static async Task VerifyPlayer(DbConnection connection, string idPlayer)
		{
			if (!await connection.ExecuteScalarAsync<bool>("SELECT COUNT(*) FROM PLAYER WHERE IDPLAYER = :idPlayer", new { idPlayer }))
				throw new ArgumentException("No such player.", nameof(idPlayer));
		}
	}
}
