namespace Wallet.WebApp.Controllers
{
	using System;
	using System.ComponentModel.DataAnnotations;
	using System.Threading.Tasks;
	using Api.Abstractions;
	using Api.Abstractions.Model;
	using Microsoft.AspNetCore.Mvc;
	using Microsoft.Extensions.Logging;

	[ApiController]
	[Route("player")]
	public class PlayerController : ControllerBase
	{
		private readonly IWalletApi _api;
		private readonly ILogger<PlayerController> _logger;

		public PlayerController(IWalletApi api, ILogger<PlayerController> logger)
		{
			_api = api;
			_logger = logger;
		}

		[HttpPut]
		public Task<string> RegisterPlayer([Required] string username) => 
			_api.RegisterPlayerAsync(username);

		[HttpPost("transaction")]
		public async Task<bool> RegisterTransaction([Required] string idPlayer, [Required] string type, [Required] decimal amount)
		{
			// I don't know what is the benefit of converting the original task to a bool but that was the requirement.
			try
			{
				await _api.RegisterPlayerTransactionAsync(idPlayer, type, amount);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Transaction failure");
				return false;
			}
		}

		[HttpGet("transaction")]
		public Task<Transaction[]> GetTransactions([Required] string idPLayer) => _api.GetPlayerTransactionsAsync(idPLayer);

		[HttpGet("wallet")]
		public Task<PlayerWallet> GetWallet([Required] string idPlayer) => _api.GetPlayerWalletAsync(idPlayer);
	}
}