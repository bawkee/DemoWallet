namespace Wallet.Api.Abstractions
{
	using System.Threading.Tasks;
	using Model;

	public interface IWalletApi // The 'repository'
	{
		Task<string> RegisterPlayerAsync(string username);
		Task<PlayerWallet> GetPlayerWalletAsync(string idPlayer);
		Task<Transaction[]> GetPlayerTransactionsAsync(string idPlayer);
		Task RegisterPlayerTransactionAsync(string idPlayer, string type, decimal amount);
	}
}
