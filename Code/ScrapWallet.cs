/// <summary>
/// Stores the scrap collected by the player during the current run.
/// Attach this component to the Player GameObject.
/// </summary>
public sealed class ScrapWallet : Component
{
	[Property] public int CurrentScrap { get; set; }
	[Property] public float ScrapMagnetMultiplier { get; set; } = 1.0f;

	public void AddScrap( int amount )
	{
		if ( amount <= 0 )
			return;

		CurrentScrap += amount;
		Log.Info( $"Collected {amount} scrap. Current scrap total: {CurrentScrap}." );
	}

	public void ApplyScrapMagnetUpgrade()
	{
		ScrapMagnetMultiplier *= 1.5f;
		Log.Info( $"Scrap Magnet applied. Magnet radius multiplier is now {ScrapMagnetMultiplier:0.##}x." );
	}

	public void ResetWallet()
	{
		CurrentScrap = 0;
		ScrapMagnetMultiplier = 1.0f;
		Log.Info( "Scrap wallet reset." );
	}
}
