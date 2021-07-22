namespace Wallet.Api.IntegrationTests
{
	using System;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Abstractions;
	using Abstractions.Model;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using MockDb;
	using Dapper;

	/// <summary>
	/// Some very basic int tests...
	/// </summary>
	[TestClass]
	public class PlayerTests
	{
		private MockDbConnectionProvider _connectionProvider;
		private IWalletApi _api;

		[TestInitialize]
		public async Task TestInitialize()
		{
			var services = new ServiceCollection();
			services.AddSingleton<MockDbConnectionProvider>()
					.AddTransient<IWalletApi, MockWalletApi>();
			var container = services.BuildServiceProvider();
			_connectionProvider = container.GetRequiredService<MockDbConnectionProvider>();
			await _connectionProvider.InitializeAsync(CancellationToken.None);
			_api = container.GetRequiredService<IWalletApi>();
		}

		[TestCleanup]
		public Task TestCleanup()
		{
			return _connectionProvider.DisposeAsync().AsTask();
		}

		[TestMethod]
		public async Task RegisterPlayers()
		{
			await RegisterPlayers(100);

			await using var c = await _connectionProvider.GetDbConnectionAsync();

			var playerCnt = await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM PLAYER");

			Assert.AreEqual(100, playerCnt);
		}

		[TestMethod]
		public async Task RegisterDepositTransaction()
		{
			var idPlayer = await _api.RegisterPlayerAsync("player1");

			// Register 1000 units across 100 transactions in parallel
			await RegisterTransactions(idPlayer, TransactionTypes.Deposit, 100);

			// Verify that the player's wallet is correct
			await using var c = await _connectionProvider.GetDbConnectionAsync();

			// Verify that the balance is correct
			var actualBalance = await c.ExecuteScalarAsync<decimal>(
				"SELECT BALANCE FROM PLAYERWALLET WHERE IDPLAYER = @IdPlayer", new { IdPlayer = idPlayer });

			var expectedBalance = Convert.ToDecimal((Math.Pow(100, 2) + 100) / 2);

			Assert.AreEqual(expectedBalance, actualBalance, "Player balance.");

			// Verify that the transactions sum to a correct amount
			var actualAmount = await c.ExecuteScalarAsync<decimal>(
				"SELECT SUM(AMOUNT) FROM TRANS WHERE IDREF = @IdPlayer", new { IdPlayer = idPlayer });

			Assert.AreEqual(expectedBalance, actualAmount, "Transactions amount.");
		}

		[TestMethod]
		public async Task RegisterWithdrawalTransactions()
		{
			// Register 50 players to start with
			var players = await RegisterPlayers(50);

			// Deposit 15 credits across 5 transactions to all 50 players in parallel
			await Task.WhenAll(
				players.Select(idPlayer => RegisterTransactions(idPlayer, TransactionTypes.Deposit, 5)));

			// Now have all of them stake some of it
			await Task.WhenAll(
				players.Select(idPlayer => _api.RegisterPlayerTransactionAsync(idPlayer, TransactionTypes.Stake, 10)));

			// Now have only 20 players win some
			var winners = players.Take(20).ToArray();

			await Task.WhenAll(
				winners.Select(idPlayer => _api.RegisterPlayerTransactionAsync(idPlayer, TransactionTypes.Win, 20)));

			// So, we have 50 players total, each with $15 deposit, then each with $10 stake but the first 20 of them
			// have won $20.
			await using var c = await _connectionProvider.GetDbConnectionAsync();

			// Asserts -->

			// Verify that all the losers have only $5 left
			var losers = players.TakeLast(30).ToArray();

			var losersSumBalance = await c.ExecuteScalarAsync<decimal>(
				"SELECT SUM(BALANCE) FROM PLAYERWALLET WHERE IDPLAYER IN @Ids", new { Ids = losers });

			Assert.AreEqual(30 * 5, losersSumBalance, "The sum balance of losers.");

			// Verify that all the winners now have $25
			var winnersSumBalance = await c.ExecuteScalarAsync<decimal>(
				"SELECT SUM(BALANCE) FROM PLAYERWALLET WHERE IDPLAYER IN @Ids", new { Ids = winners });

			Assert.AreEqual(20 * 25, winnersSumBalance, "The sum balance of winners.");

			// Verify the correct number of transactions and their amounts. There should be 50*5 deposits, 50 stakes
			// and 20 wins.
			var transCount = await c.ExecuteScalarAsync<decimal>("SELECT COUNT(*) FROM TRANS");

			Assert.AreEqual(320, transCount, "Total number of transactions.");

			// Verify that the total sum from all those transactions is correct. 
			var transTotalSum = await c.ExecuteScalarAsync<decimal>("SELECT SUM(ACTUALAMOUNT) FROM " +
																	" (SELECT CASE WHEN TRANSACTIONTYPE = 'Stake'" +
																	"	THEN AMOUNT * -1 " +
																	"	ELSE AMOUNT END AS ACTUALAMOUNT FROM TRANS)");

			Assert.AreEqual(30 * 5 + 20 * 25, transTotalSum, "Total sum amount of all transactions.");
		}

		[TestMethod]
		public async Task GetPlayerTransactions()
		{
			// Create 10 players and for each player (in parallel) create 3 transactions sequentially
			var players = await RegisterPlayers(10);

			var playerTasks = players.Select(async idPlayer =>
			{
				await _api.RegisterPlayerTransactionAsync(idPlayer, TransactionTypes.Deposit, 1);
				await _api.RegisterPlayerTransactionAsync(idPlayer, TransactionTypes.Stake, 1);
				await _api.RegisterPlayerTransactionAsync(idPlayer, TransactionTypes.Win, 2);
			});

			await Task.WhenAll(playerTasks);

			// Asserts -->
			foreach (var idPlayer in players)
			{
				var actualTransactions = await _api.GetPlayerTransactionsAsync(idPlayer);

				Assert.AreEqual(3, actualTransactions.Length, "Number of transactions per player");

				Assert.IsTrue(actualTransactions.All(t => t.IdRef == idPlayer), "Transaction reference ID.");
				Assert.IsTrue(actualTransactions.All(t => t.RefType == "Player"), "Transaction reference type.");

				Assert.AreEqual(TransactionTypes.Deposit, actualTransactions[0].TransactionType, "Transaction type");
				Assert.AreEqual(TransactionTypes.Stake, actualTransactions[1].TransactionType, "Transaction type");
				Assert.AreEqual(TransactionTypes.Win, actualTransactions[2].TransactionType, "Transaction type");

				Assert.AreEqual(1, actualTransactions[0].Amount, "Transaction amount");
				Assert.AreEqual(1, actualTransactions[1].Amount, "Transaction amount");
				Assert.AreEqual(2, actualTransactions[2].Amount, "Transaction amount");
			}
		}

		[TestMethod]
		public async Task GetPlayerWallet()
		{
			// Create 10 players and add different amounts across different number of transactions to each player, in parallel
			var players = await RegisterPlayers(10);

			// So, player 1 has 1 trans of $1, player 2 has 2 trans of $3, player 3 has 3 trans of $6 etc.
			var playerTasks = players.Select((idPlayer, i) => RegisterTransactions(idPlayer, TransactionTypes.Deposit, i + 1));

			await Task.WhenAll(playerTasks);

			foreach(var item in players.Select((p, i) => new { IdPlayer = p, Index = i + 1 }))
			{
				var expectedBalance = Convert.ToDecimal((Math.Pow(item.Index, 2d) + item.Index) / 2d);
				var actualWallet = await _api.GetPlayerWalletAsync(item.IdPlayer);

				Assert.AreEqual(expectedBalance, actualWallet.Balance, "Player balance.");
			}
		}

		private Task RegisterTransactions(string idPlayer, string type, int transCount)
		{
			// This will register transactions all with different sequential amounts (i.e. amounts are 1, 2, 3, 4... etc.)
			var t = Enumerable.Range(1, transCount)
							  .Select(i => _api.RegisterPlayerTransactionAsync(idPlayer, type, i));
			return Task.WhenAll(t);
		}


		private Task<string[]> RegisterPlayers(int count)
		{
			var t = Enumerable.Range(1, count)
							  .Select(i => _api.RegisterPlayerAsync($"player{i}"));
			return Task.WhenAll(t);
		}
	}
}
