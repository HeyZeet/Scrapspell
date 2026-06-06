/// <summary>
/// Slow explosive enemy that chases the player and starts a fuse at close range.
/// Shooting it to zero health causes an immediate explosion.
/// </summary>
public sealed class BoomcanEnemy : Component, Component.IDamageable
{
	[Property] public GameObject PlayerTarget { get; set; }
	[Property] public GameObject ScrapPickupPrefab { get; set; }

	[Property] public float MaxHealth { get; set; } = 40.0f;
	[Property] public float MoveSpeed { get; set; } = 55.0f;
	[Property] public float ExplosionRadius { get; set; } = 160.0f;
	[Property] public float ExplosionDamage { get; set; } = 30.0f;
	[Property] public float ExplosionKnockback { get; set; } = 500.0f;
	[Property] public float FuseRange { get; set; } = 90.0f;
	[Property] public float FuseTime { get; set; } = 1.25f;
	[Property] public int ScrapDropAmount { get; set; } = 4;
	[Property] public GameObject ExplosionEffectPrefab { get; set; }
	[Property] public SoundEvent ExplosionSound { get; set; }

	float currentHealth;
	float fuseFinishTime;
	bool fuseStarted;
	bool hasExploded;

	public bool IsAlive => !hasExploded;

	protected override void OnStart()
	{
		base.OnStart();

		currentHealth = MaxHealth;
		FindPlayerIfNeeded();

		Log.Info( $"Boomcan spawned with {currentHealth} health." );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || hasExploded )
			return;

		if ( fuseStarted )
		{
			if ( Time.Now >= fuseFinishTime )
				Explode();

			return;
		}

		FindPlayerIfNeeded();

		if ( !PlayerTarget.IsValid() )
			return;

		var toPlayer = PlayerTarget.WorldPosition - WorldPosition;
		var flatToPlayer = toPlayer.WithZ( 0 );

		if ( flatToPlayer.Length <= FuseRange )
		{
			StartFuse();
			return;
		}

		MoveTowardPlayer( flatToPlayer );
	}

	public void TakeDamage( float amount )
	{
		if ( amount <= 0.0f || hasExploded )
			return;

		currentHealth = (currentHealth - amount).Clamp( 0.0f, MaxHealth );
		Log.Info( $"Boomcan took {amount} damage. Health: {currentHealth}/{MaxHealth}" );

		if ( currentHealth <= 0.0f )
			Explode();
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

		// Direct movement is enough for this first prototype enemy.
		WorldPosition += direction * MoveSpeed * Time.Delta;
		WorldRotation = Rotation.LookAt( direction, Vector3.Up );
	}

	void StartFuse()
	{
		if ( fuseStarted || hasExploded )
			return;

		fuseStarted = true;
		fuseFinishTime = Time.Now + FuseTime.Clamp( 0.0f, 30.0f );

		Log.Info( $"Boomcan fuse started. Exploding in {FuseTime:0.##} seconds." );
	}

	void Explode()
	{
		if ( hasExploded )
			return;

		hasExploded = true;
		var explosionPosition = WorldPosition;

		Log.Info( $"Boomcan exploded with radius {ExplosionRadius}." );

		PlayExplosionEffects( explosionPosition );
		DamagePlayer( explosionPosition );
		DamageScraplings( explosionPosition );
		DamageBoomcans( explosionPosition );
		ApplyExplosionKnockback( explosionPosition );
		DropScrap( explosionPosition );

		GameObject.Destroy();
	}

	void DamagePlayer( Vector3 explosionPosition )
	{
		FindPlayerIfNeeded();

		if ( !PlayerTarget.IsValid() )
			return;

		var health = PlayerTarget.Components.Get<HealthComponent>();
		if ( !health.IsValid() || PlayerTarget.WorldPosition.Distance( explosionPosition ) > ExplosionRadius )
			return;

		health.TakeDamage( ExplosionDamage );
		Log.Info( $"Boomcan explosion hit player for {ExplosionDamage} damage." );
	}

	void DamageScraplings( Vector3 explosionPosition )
	{
		foreach ( var scrapling in Scene.GetAllComponents<ScraplingEnemy>().ToList() )
		{
			if ( !scrapling.IsValid() || scrapling.WorldPosition.Distance( explosionPosition ) > ExplosionRadius )
				continue;

			scrapling.TakeDamage( ExplosionDamage );
			Log.Info( $"Boomcan explosion hit {scrapling.GameObject.Name} for {ExplosionDamage} damage." );
		}
	}

	void DamageBoomcans( Vector3 explosionPosition )
	{
		foreach ( var boomcan in Scene.GetAllComponents<BoomcanEnemy>().ToList() )
		{
			if ( !boomcan.IsValid() || boomcan == this )
				continue;

			if ( boomcan.WorldPosition.Distance( explosionPosition ) > ExplosionRadius )
				continue;

			boomcan.TakeDamage( ExplosionDamage );
			Log.Info( $"Boomcan explosion hit {boomcan.GameObject.Name} for {ExplosionDamage} damage." );
		}
	}

	void ApplyExplosionKnockback( Vector3 explosionPosition )
	{
		foreach ( var rigidbody in Scene.GetAllComponents<Rigidbody>().ToList() )
		{
			if ( !rigidbody.IsValid() || !rigidbody.MotionEnabled )
				continue;

			var awayFromExplosion = rigidbody.WorldPosition - explosionPosition;
			var distance = awayFromExplosion.Length;

			if ( distance > ExplosionRadius || awayFromExplosion.IsNearZeroLength )
				continue;

			// Objects nearer the center receive more of the configured impulse.
			var strength = 1.0f - (distance / ExplosionRadius);
			rigidbody.ApplyImpulse( awayFromExplosion.Normal * ExplosionKnockback * strength );

			Log.Info( $"Boomcan explosion knocked back {rigidbody.GameObject.Name}." );
		}
	}

	void PlayExplosionEffects( Vector3 explosionPosition )
	{
		if ( ExplosionSound is not null )
			Sound.Play( ExplosionSound, explosionPosition );

		if ( ExplosionEffectPrefab.IsValid() )
			ExplosionEffectPrefab.Clone( new Transform( explosionPosition, Rotation.Identity ) );
	}

	void DropScrap( Vector3 explosionPosition )
	{
		for ( var i = 0; i < ScrapDropAmount; i++ )
		{
			if ( !ScrapPickupPrefab.IsValid() )
			{
				Log.Info( "Boomcan would drop scrap, but no ScrapPickupPrefab is assigned." );
				return;
			}

			var offset = new Vector3(
				Game.Random.Float( -32.0f, 32.0f ),
				Game.Random.Float( -32.0f, 32.0f ),
				Game.Random.Float( 8.0f, 24.0f )
			);

			ScrapPickupPrefab.Clone( new Transform( explosionPosition + offset, Rotation.Identity ) );
		}
	}

	void FindPlayerIfNeeded()
	{
		if ( PlayerTarget.IsValid() )
			return;

		PlayerTarget = Scene.Directory.FindByName( "Player" ).FirstOrDefault();
	}
}
