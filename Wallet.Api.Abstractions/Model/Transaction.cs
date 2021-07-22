namespace Wallet.Api.Abstractions.Model
{
	public record Transaction
	{
		public string IdTransaction { get; init; }
		public string IdRef { get; init; }
		public string RefType { get; init; }
		public decimal Amount { get; init; }
		public string TransactionType { get; init; }
	}
}
