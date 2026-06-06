/// <summary>
/// A simple scrap collectible that magnetizes toward the player.
/// Attach this to a small pickup GameObject or scrap pickup prefab.
/// </summary>
public sealed class ScrapPickup : Component
{
	[Property] public int ScrapValue { get; set; } = 1;
	[Property] public float PickupRadius { get; set; } = 24.0f;
	[Property] public float MagnetRadius { get; set; } = 160.0f;
	[Property] public float MagnetSpeed { get; set; } = 240.0f;

	GameObject player;
	ScrapWallet playerWallet;
	bool hasBeenCollected;

	protected override void OnStart()
	{
		base.OnStart();
		FindPlayerIfNeeded();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || hasBeenCollected )
			return;

		FindPlayerIfNeeded();

		if ( !player.IsValid() || !playerWallet.IsValid() )
			return;

		var toPlayer = player.WorldPosition - WorldPosition;
		var distance = toPlayer.Length;
		var effectiveMagnetRadius = MagnetRadius * playerWallet.ScrapMagnetMultiplier;

		if ( distance <= PickupRadius )
		{
			Collect();
			return;
		}

		if ( distance <= effectiveMagnetRadius && !toPlayer.IsNearZeroLength )
		{
			// Move directly toward the player. No Rigidbody is required.
			var moveDistance = MagnetSpeed * Time.Delta;
			var distanceThisFrame = moveDistance < distance ? moveDistance : distance;
			WorldPosition += toPlayer.Normal * distanceThisFrame;
		}
	}

	void Collect()
	{
		if ( hasBeenCollected )
			return;

		hasBeenCollected = true;
		playerWallet.AddScrap( ScrapValue );
		Log.Info( $"Scrap pickup collected for {ScrapValue} scrap." );
		GameObject.Destroy();
	}

	void FindPlayerIfNeeded()
	{
		if ( player.IsValid() && playerWallet.IsValid() )
			return;

		player = Scene.Directory.FindByName( "Player" ).FirstOrDefault();
		playerWallet = player?.Components.Get<ScrapWallet>();
	}
}
