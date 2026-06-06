/// <summary>
/// First simple enemy for the prototype.
/// It chases the player directly, attacks in melee range, and drops scrap on death.
/// </summary>
public sealed class ScraplingEnemy : Component, Component.IDamageable
{
	[Property] public GameObject PlayerTarget { get; set; }
	[Property] public GameObject ScrapPickupPrefab { get; set; }

	[Property] public float MaxHealth { get; set; } = 50.0f;
	[Property] public float MoveSpeed { get; set; } = 90.0f;
	[Property] public float AttackDamage { get; set; } = 10.0f;
	[Property] public float AttackRange { get; set; } = 48.0f;
	[Property] public float AttackCooldown { get; set; } = 1.0f;
	[Property] public int ScrapDropAmount { get; set; } = 3;

	float currentHealth;
	float nextAttackTime;
	bool isDead;

	public bool IsAlive => !isDead;

	protected override void OnStart()
	{
		base.OnStart();

		currentHealth = MaxHealth;
		FindPlayerIfNeeded();

		Log.Info( $"Scrapling spawned with {currentHealth} health." );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || isDead )
			return;

		FindPlayerIfNeeded();

		if ( !PlayerTarget.IsValid() )
			return;

		var toPlayer = PlayerTarget.WorldPosition - WorldPosition;
		var flatToPlayer = toPlayer.WithZ( 0 );
		var distance = flatToPlayer.Length;

		if ( distance > AttackRange )
		{
			MoveTowardPlayer( flatToPlayer );
			return;
		}

		TryAttackPlayer();
	}

	/// <summary>
	/// PipeBlaster and future weapons can call this directly.
	/// </summary>
	public void TakeDamage( float amount )
	{
		if ( amount <= 0.0f || isDead )
			return;

		currentHealth = (currentHealth - amount).Clamp( 0.0f, MaxHealth );
		Log.Info( $"Scrapling took {amount} damage. Health: {currentHealth}/{MaxHealth}" );

		if ( currentHealth <= 0.0f )
			Die();
	}

	/// <summary>
	/// This lets Sandbox damage messages also hurt the Scrapling.
	/// PipeBlaster uses this path when there is no HealthComponent on the hit object.
	/// </summary>
	public void OnDamage( in DamageInfo damage )
	{
		TakeDamage( damage.Damage );
	}

	void MoveTowardPlayer( Vector3 flatToPlayer )
	{
		if ( flatToPlayer.IsNearZeroLength )
			return;

		var direction = flatToPlayer.Normal;

		// This is intentionally simple direct movement. No navmesh or pathfinding yet.
		WorldPosition += direction * MoveSpeed * Time.Delta;
		WorldRotation = Rotation.LookAt( direction, Vector3.Up );
	}

	void TryAttackPlayer()
	{
		if ( Time.Now < nextAttackTime )
			return;

		nextAttackTime = Time.Now + AttackCooldown;
		Log.Info( $"Scrapling attacked player for {AttackDamage} damage." );

		var playerHealth = PlayerTarget.Components.Get<HealthComponent>();
		playerHealth?.TakeDamage( AttackDamage );
	}

	void Die()
	{
		if ( isDead )
			return;

		isDead = true;
		Log.Info( "Scrapling died." );

		DropScrap();
		GameObject.Destroy();
	}

	void DropScrap()
	{
		for ( var i = 0; i < ScrapDropAmount; i++ )
		{
			if ( !ScrapPickupPrefab.IsValid() )
			{
				Log.Info( "Scrapling would drop scrap, but no ScrapPickupPrefab is assigned yet." );
				continue;
			}

			var offset = new Vector3(
				Game.Random.Float( -24.0f, 24.0f ),
				Game.Random.Float( -24.0f, 24.0f ),
				12.0f
			);

			ScrapPickupPrefab.Clone( new Transform( WorldPosition + offset, Rotation.Identity ) );
		}
	}

	void FindPlayerIfNeeded()
	{
		if ( PlayerTarget.IsValid() )
			return;

		PlayerTarget = Scene.Directory.FindByName( "Player" ).FirstOrDefault();
	}
}
