/// <summary>
/// Simple mini-boss with chase, close-range attacks, and a straight-line charge.
/// Attach this component to the root GameObject of the Junk Brute prefab.
/// </summary>
public sealed class JunkBruteBoss : Component, Component.IDamageable
{
	[Property] public GameObject PlayerTarget { get; set; }
	[Property] public GameObject ScrapPickupPrefab { get; set; }

	[Property] public float MaxHealth { get; set; } = 500.0f;
	[Property] public float MoveSpeed { get; set; } = 65.0f;
	[Property] public float AttackDamage { get; set; } = 20.0f;
	[Property] public float AttackRange { get; set; } = 64.0f;
	[Property] public float AttackCooldown { get; set; } = 1.25f;
	[Property] public float SlamRadius { get; set; } = 130.0f;
	[Property] public float SlamDamage { get; set; } = 30.0f;
	[Property] public float ChargeSpeed { get; set; } = 260.0f;
	[Property] public float ChargeDuration { get; set; } = 1.1f;
	[Property] public float ChargeCooldown { get; set; } = 6.0f;
	[Property] public int ScrapDropAmount { get; set; } = 12;

	public event System.Action<JunkBruteBoss> BossDied;

	float currentHealth;
	float nextAttackTime;
	float nextChargeTime;
	float chargeFinishTime;
	Vector3 chargeDirection;
	bool isCharging;
	bool useSlamNext;
	bool isDead;

	public bool IsAlive => !isDead;

	protected override void OnStart()
	{
		base.OnStart();

		currentHealth = MaxHealth;
		nextChargeTime = Time.Now + ChargeCooldown;
		FindPlayerIfNeeded();

		Log.Info( $"Junk Brute spawned with {currentHealth} health." );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || isDead )
			return;

		FindPlayerIfNeeded();

		if ( !PlayerTarget.IsValid() )
			return;

		if ( isCharging )
		{
			UpdateCharge();
			return;
		}

		var toPlayer = PlayerTarget.WorldPosition - WorldPosition;
		var flatToPlayer = toPlayer.WithZ( 0 );

		if ( Time.Now >= nextChargeTime && !flatToPlayer.IsNearZeroLength )
		{
			StartCharge( flatToPlayer.Normal );
			return;
		}

		if ( flatToPlayer.Length <= AttackRange )
		{
			TryCloseAttack();
			return;
		}

		MoveTowardPlayer( flatToPlayer );
	}

	public void TakeDamage( float amount )
	{
		if ( amount <= 0.0f || isDead )
			return;

		currentHealth = (currentHealth - amount).Clamp( 0.0f, MaxHealth );
		Log.Info( $"Junk Brute took {amount} damage. Health: {currentHealth}/{MaxHealth}" );

		if ( currentHealth <= 0.0f )
			Die();
	}

	public void OnDamage( in DamageInfo damage )
	{
		TakeDamage( damage.Damage );
	}

	void MoveTowardPlayer( Vector3 flatToPlayer )
	{
		if ( flatToPlayer.IsNearZeroLength )
			return;

		var direction = flatToPlayer.Normal;
		WorldPosition += direction * MoveSpeed * Time.Delta;
		WorldRotation = Rotation.LookAt( direction, Vector3.Up );
	}

	void TryCloseAttack()
	{
		if ( Time.Now < nextAttackTime )
			return;

		nextAttackTime = Time.Now + AttackCooldown;

		// Alternate attacks so both behaviors are easy to see and tune.
		if ( useSlamNext )
			PerformSlam();
		else
			PerformMelee();

		useSlamNext = !useSlamNext;
	}

	void PerformMelee()
	{
		var distance = PlayerTarget.WorldPosition.Distance( WorldPosition );
		if ( distance > AttackRange )
			return;

		DamagePlayer( AttackDamage );
		Log.Info( $"Junk Brute melee hit player for {AttackDamage} damage." );
	}

	void PerformSlam()
	{
		var distance = PlayerTarget.WorldPosition.Distance( WorldPosition );
		Log.Info( $"Junk Brute slammed with radius {SlamRadius}." );

		if ( distance <= SlamRadius )
		{
			DamagePlayer( SlamDamage );
			Log.Info( $"Junk Brute slam hit player for {SlamDamage} damage." );
		}
	}

	void StartCharge( Vector3 direction )
	{
		isCharging = true;
		chargeDirection = direction;
		chargeFinishTime = Time.Now + ChargeDuration.Clamp( 0.0f, 10.0f );
		nextChargeTime = Time.Now + ChargeCooldown.Clamp( 0.0f, 60.0f );
		WorldRotation = Rotation.LookAt( chargeDirection, Vector3.Up );

		Log.Info( $"Junk Brute started charging for {ChargeDuration:0.##} seconds." );
	}

	void UpdateCharge()
	{
		WorldPosition += chargeDirection * ChargeSpeed * Time.Delta;

		if ( PlayerTarget.WorldPosition.Distance( WorldPosition ) <= AttackRange )
		{
			DamagePlayer( AttackDamage );
			isCharging = false;
			Log.Info( $"Junk Brute charge hit player for {AttackDamage} damage." );
			return;
		}

		if ( Time.Now >= chargeFinishTime )
		{
			isCharging = false;
			Log.Info( "Junk Brute charge finished." );
		}
	}

	void DamagePlayer( float damage )
	{
		var health = PlayerTarget.Components.Get<HealthComponent>();
		health?.TakeDamage( damage );
	}

	void Die()
	{
		if ( isDead )
			return;

		isDead = true;
		Log.Info( "Junk Brute boss died." );

		DropScrap();
		BossDied?.Invoke( this );

		var spawner = Scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
		spawner?.NotifyBossDied( GameObject );

		GameObject.Destroy();
	}

	void DropScrap()
	{
		for ( var i = 0; i < ScrapDropAmount; i++ )
		{
			if ( !ScrapPickupPrefab.IsValid() )
			{
				Log.Info( "Junk Brute would drop scrap, but no ScrapPickupPrefab is assigned." );
				return;
			}

			var offset = new Vector3(
				Game.Random.Float( -48.0f, 48.0f ),
				Game.Random.Float( -48.0f, 48.0f ),
				Game.Random.Float( 12.0f, 32.0f )
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
