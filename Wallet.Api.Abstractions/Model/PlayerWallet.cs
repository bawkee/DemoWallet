namespace Wallet.Api.Abstractions.Model
{
	public record PlayerWallet
	{
		public string IdPlayer { get; init; }
		public decimal Balance { get; init; }
	}
}