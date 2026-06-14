/// <summary>
/// First prototype scrap shotgun. It fires several raycast pellets from the camera.
/// Keep this simple for now; upgrade synergies can layer on top later.
/// </summary>
public sealed class PipeBlaster : Component
{
	[Property] public CameraComponent Camera { get; set; }

	[Property] public float DamagePerPellet { get; set; } = 10.0f;
	[Property] public int PelletCount { get; set; } = 8;
	[Property] public float SpreadDegrees { get; set; } = 5.0f;
	[Property] public float Range { get; set; } = 900.0f;
	[Property] public float FireRate { get; set; } = 1.2f;
	[Property] public float KnockbackForce { get; set; } = 250.0f;
	[Property] public float RecoilAmount { get; set; } = 3.0f;
	[Property] public float RecoilRecoverySpeed { get; set; } = 14.0f;
	[Property] public int AmmoInClip { get; set; } = 2;
	[Property] public int ClipSize { get; set; } = 2;
	[Property] public float ReloadTime { get; set; } = 1.6f;
	[Property] public float ShellKickerRadius { get; set; } = 140.0f;
	[Property] public float ShellKickerForce { get; set; } = 90.0f;
	[Property] public float RustBurstRadius { get; set; } = 140.0f;
	[Property] public float RustBurstDamage { get; set; } = 20.0f;

	[Property] public GameObject MuzzleFlashPrefab { get; set; }
	[Property] public GameObject ImpactEffectPrefab { get; set; }
	[Property] public float MuzzleFlashLifetime { get; set; } = 0.25f;
	[Property] public float ImpactEffectLifetime { get; set; } = 2.0f;
	[Property] public string LeftMuzzleBone { get; set; } = "MuzzleLeft";
	[Property] public string RightMuzzleBone { get; set; } = "MuzzleRight";
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public SoundEvent ReloadSound { get; set; }

	[Property] public SkinnedModelRenderer ViewModelRenderer { get; set; }
	[Property] public ScrapspellFirstPersonController PlayerController { get; set; }
	[Property] public string BindAnimation { get; set; } = "ShotgunBind";
	[Property] public string ShootAnimation { get; set; } = "ShotgunShoot";
	[Property] public string ReloadAnimation { get; set; } = "ShotgunReload";
	[Property] public string WalkAnimation { get; set; } = "ShotgunWalk";
	[Property] public float WalkAnimationSpeedThreshold { get; set; } = 5.0f;

	bool isReloading;
	float reloadFinishTime;
	float lastFireTime = -999.0f;
	float currentRecoil;
	string currentViewModelAnimation;
	bool fireLeftBarrelNext = true;
	int shellKickerStacks;
	int rustBurstStacks;
	float startingDamagePerPellet;
	int startingPelletCount;
	float startingSpreadDegrees;
	float startingFireRate;
	int startingAmmoInClip;

	public bool IsReloading => isReloading;
	public bool InputEnabled { get; set; } = true;

	protected override void OnStart()
	{
		base.OnStart();

		AmmoInClip = AmmoInClip.Clamp( 0, ClipSize );
		startingDamagePerPellet = DamagePerPellet;
		startingPelletCount = PelletCount;
		startingSpreadDegrees = SpreadDegrees;
		startingFireRate = FireRate;
		startingAmmoInClip = AmmoInClip;
		FindCameraIfNeeded();
		FindViewModelComponentsIfNeeded();
		PlayViewModelAnimation( BindAnimation, false, true );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		FinishReloadIfReady();

		if ( !InputEnabled )
		{
			PlayViewModelAnimation( BindAnimation, false );
			UpdateCameraRecoil();
			return;
		}

		if ( Input.Pressed( "Reload" ) )
			TryStartReload();

		if ( Input.Pressed( "Slot1" ) )
			ApplyExtraBoltsUpgrade( 2 );

		if ( Input.Down( "Attack1" ) )
			TryFire();

		UpdateViewModelAnimation();
		UpdateCameraRecoil();
	}

	void TryFire()
	{
		if ( isReloading )
			return;

		if ( AmmoInClip <= 0 )
		{
			TryStartReload();
			return;
		}

		var secondsBetweenShots = 1.0f / FireRate.Clamp( 0.01f, 100.0f );
		if ( Time.Now - lastFireTime < secondsBetweenShots )
			return;

		Fire();
	}

	public void ApplyExtraBoltsUpgrade( int amount )
	{
		if ( amount <= 0 )
			return;

		PelletCount += amount;
		Log.Info( $"Extra Bolts upgrade applied. PelletCount is now {PelletCount}." );
	}

	public void ApplyReinforcedPipeUpgrade()
	{
		DamagePerPellet *= 1.25f;
		Log.Info( $"Reinforced Pipe applied. DamagePerPellet is now {DamagePerPellet:0.##}." );
	}

	public void ApplyGreasedTriggerUpgrade()
	{
		FireRate *= 1.15f;
		Log.Info( $"Greased Trigger applied. FireRate is now {FireRate:0.##}." );
	}

	public void ApplyWideMouthUpgrade()
	{
		PelletCount += 4;
		SpreadDegrees *= 1.2f;
		Log.Info( $"Wide Mouth applied. Pellets: {PelletCount}, spread: {SpreadDegrees:0.##}." );
	}

	public void ApplyHeavySlugMixUpgrade()
	{
		PelletCount = (PelletCount * 0.7f).CeilToInt().Clamp( 1, 1000 );
		DamagePerPellet *= 1.6f;
		Log.Info( $"Heavy Slug Mix applied. Pellets: {PelletCount}, damage: {DamagePerPellet:0.##}." );
	}

	public void ApplyShellKickerUpgrade()
	{
		shellKickerStacks++;
		Log.Info( $"Shell Kicker applied. Stack count: {shellKickerStacks}." );
	}

	public void ApplyRustBurstUpgrade()
	{
		rustBurstStacks++;
		Log.Info( $"Rust Burst applied. Stack count: {rustBurstStacks}." );
	}

	public void ResetForNewRun()
	{
		DamagePerPellet = startingDamagePerPellet;
		PelletCount = startingPelletCount;
		SpreadDegrees = startingSpreadDegrees;
		FireRate = startingFireRate;
		AmmoInClip = startingAmmoInClip;
		shellKickerStacks = 0;
		rustBurstStacks = 0;
		isReloading = false;
		currentRecoil = 0.0f;
		fireLeftBarrelNext = true;
		PlayViewModelAnimation( BindAnimation, false, true );

		Log.Info( "Pipe Blaster reset for a new run." );
	}

	void Fire()
	{
		FindCameraIfNeeded();

		if ( !Camera.IsValid() )
		{
			Log.Warning( "PipeBlaster could not fire because no camera was assigned or found." );
			return;
		}

		lastFireTime = Time.Now;
		AmmoInClip--;
		var firedMuzzleBone = fireLeftBarrelNext ? LeftMuzzleBone : RightMuzzleBone;
		fireLeftBarrelNext = !fireLeftBarrelNext;

		Log.Info( $"PipeBlaster fired. Ammo: {AmmoInClip}/{ClipSize}" );

		currentRecoil += RecoilAmount;
		PlayViewModelAnimation( ShootAnimation, false, true );
		PlayFireEffects( firedMuzzleBone );

		for ( var i = 0; i < PelletCount; i++ )
		{
			FirePellet();
		}
	}

	void FirePellet()
	{
		var start = Camera.WorldPosition;
		var direction = GetRandomSpreadDirection();
		var end = start + direction * Range;

		// Ignore the player/weapon hierarchy so we do not shoot ourselves.
		var trace = Scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		if ( !trace.Hit || trace.GameObject is null )
			return;

		Log.Info( $"PipeBlaster hit {trace.GameObject.Name}." );

		SpawnImpactEffect( trace );
		ApplyDamage( trace );
		ApplyKnockback( trace, direction );
	}

	Vector3 GetRandomSpreadDirection()
	{
		// Random pitch/yaw offsets create a simple shotgun cone around the crosshair.
		var yaw = Game.Random.Float( -SpreadDegrees, SpreadDegrees );
		var pitch = Game.Random.Float( -SpreadDegrees, SpreadDegrees );
		var spreadRotation = Rotation.FromYaw( yaw ) * Rotation.FromPitch( pitch );

		return (Camera.WorldRotation * spreadRotation).Forward;
	}

	void ApplyDamage( SceneTraceResult trace )
	{
		var scrapling = trace.GameObject.Components.Get<ScraplingEnemy>( FindMode.EverythingInSelfAndAncestors );
		if ( scrapling.IsValid() )
		{
			var deathPosition = scrapling.WorldPosition;
			var wasAlive = scrapling.IsAlive;
			scrapling.TakeDamage( DamagePerPellet );

			if ( wasAlive && !scrapling.IsAlive )
				TriggerRustBurst( deathPosition, scrapling.GameObject );

			return;
		}

		var boomcan = trace.GameObject.Components.Get<BoomcanEnemy>( FindMode.EverythingInSelfAndAncestors );
		if ( boomcan.IsValid() )
		{
			var deathPosition = boomcan.WorldPosition;
			var wasAlive = boomcan.IsAlive;
			boomcan.TakeDamage( DamagePerPellet );

			if ( wasAlive && !boomcan.IsAlive )
				TriggerRustBurst( deathPosition, boomcan.GameObject );

			return;
		}

		var boss = trace.GameObject.Components.Get<JunkBruteBoss>( FindMode.EverythingInSelfAndAncestors );
		if ( boss.IsValid() )
		{
			var deathPosition = boss.WorldPosition;
			var wasAlive = boss.IsAlive;
			boss.TakeDamage( DamagePerPellet );

			if ( wasAlive && !boss.IsAlive )
				TriggerRustBurst( deathPosition, boss.GameObject );

			return;
		}

		var healthComponents = trace.GameObject.Components
			.GetAll<HealthComponent>( FindMode.EverythingInSelfAndAncestors )
			.ToList();

		if ( healthComponents.Count > 0 )
		{
			foreach ( var health in healthComponents )
			{
				health.TakeDamage( DamagePerPellet );
			}

			return;
		}

		var damage = new DamageInfo( DamagePerPellet, GameObject, GameObject, trace.Hitbox )
		{
			Position = trace.HitPosition,
			Shape = trace.Shape
		};

		foreach ( var damageable in trace.GameObject.Components.GetAll<Component.IDamageable>( FindMode.EverythingInSelfAndAncestors ) )
		{
			damageable.OnDamage( damage );
		}
	}

	void TriggerRustBurst( Vector3 position, GameObject killedEnemy )
	{
		if ( rustBurstStacks <= 0 )
			return;

		var burstDamage = RustBurstDamage * rustBurstStacks;
		Log.Info( $"Rust Burst triggered for {burstDamage} damage." );

		foreach ( var scrapling in Scene.GetAllComponents<ScraplingEnemy>().ToList() )
		{
			if ( !scrapling.IsValid() || scrapling.GameObject == killedEnemy )
				continue;

			if ( scrapling.WorldPosition.Distance( position ) <= RustBurstRadius )
				scrapling.TakeDamage( burstDamage );
		}

		foreach ( var boomcan in Scene.GetAllComponents<BoomcanEnemy>().ToList() )
		{
			if ( !boomcan.IsValid() || boomcan.GameObject == killedEnemy )
				continue;

			if ( boomcan.WorldPosition.Distance( position ) <= RustBurstRadius )
				boomcan.TakeDamage( burstDamage );
		}

		foreach ( var boss in Scene.GetAllComponents<JunkBruteBoss>().ToList() )
		{
			if ( !boss.IsValid() || boss.GameObject == killedEnemy )
				continue;

			if ( boss.WorldPosition.Distance( position ) <= RustBurstRadius )
				boss.TakeDamage( burstDamage );
		}
	}

	void ApplyKnockback( SceneTraceResult trace, Vector3 direction )
	{
		// For this prototype, only objects with a Rigidbody component get pushed.
		var rigidbody = trace.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndAncestors );
		if ( !rigidbody.IsValid() )
			return;

		if ( !trace.Body.IsValid() || trace.Body.BodyType == PhysicsBodyType.Static )
			return;

		// Push away from the shooter along the pellet direction.
		trace.Body.ApplyImpulseAt( trace.HitPosition, direction * KnockbackForce );
	}

	void UpdateCameraRecoil()
	{
		if ( !Camera.IsValid() )
			return;

		if ( currentRecoil <= 0.001f )
		{
			currentRecoil = 0.0f;
			return;
		}

		// Negative pitch tips the view upward for a punchy shotgun kick.
		Camera.WorldRotation *= Rotation.FromPitch( -currentRecoil );
		currentRecoil = MathX.Lerp( currentRecoil, 0.0f, Time.Delta * RecoilRecoverySpeed );
	}

	void PlayFireEffects( string muzzleBone )
	{
		if ( FireSound is not null )
			Sound.Play( FireSound, Camera.WorldPosition );

		if ( !MuzzleFlashPrefab.IsValid() )
			return;

		FindViewModelComponentsIfNeeded();

		Transform muzzleTransform = default;
		var hasMuzzleBone = ViewModelRenderer.IsValid()
			&& ViewModelRenderer.TryGetBoneTransform( muzzleBone, out muzzleTransform );

		if ( !hasMuzzleBone )
		{
			Log.Warning( $"PipeBlaster could not find muzzle bone '{muzzleBone}'. Using camera fallback." );
			var muzzlePosition = Camera.WorldPosition + Camera.WorldRotation.Forward * 24.0f;
			muzzleTransform = new Transform( muzzlePosition, Camera.WorldRotation );
		}

		var muzzleFlash = MuzzleFlashPrefab.Clone( muzzleTransform );
		AddTemporaryLifetime( muzzleFlash, MuzzleFlashLifetime );
	}

	void SpawnImpactEffect( SceneTraceResult trace )
	{
		if ( !ImpactEffectPrefab.IsValid() )
			return;

		var impactRotation = Rotation.LookAt( trace.Normal );
		var impactTransform = new Transform( trace.HitPosition + trace.Normal * 2.0f, impactRotation );
		var impactEffect = ImpactEffectPrefab.Clone( impactTransform );
		AddTemporaryLifetime( impactEffect, ImpactEffectLifetime );
	}

	void AddTemporaryLifetime( GameObject effect, float lifetime )
	{
		if ( !effect.IsValid() )
			return;

		var temporaryEffect = effect.Components.GetOrCreate<TemporaryEffect>();
		temporaryEffect.DestroyAfterSeconds = lifetime < 0.0f ? 0.0f : lifetime;
		temporaryEffect.BecomeOrphan = false;
		temporaryEffect.WaitForChildEffects = true;
	}

	void TryStartReload()
	{
		if ( isReloading || AmmoInClip >= ClipSize )
			return;

		isReloading = true;
		reloadFinishTime = Time.Now + ReloadTime;
		PlayViewModelAnimation( ReloadAnimation, false, true, ReloadTime );

		Log.Info( "PipeBlaster reload started." );

		if ( ReloadSound is not null )
			Sound.Play( ReloadSound, WorldPosition );
	}

	void FinishReloadIfReady()
	{
		if ( !isReloading || Time.Now < reloadFinishTime )
			return;

		isReloading = false;
		AmmoInClip = ClipSize;
		fireLeftBarrelNext = true;

		Log.Info( $"PipeBlaster reload finished. Ammo: {AmmoInClip}/{ClipSize}" );
		TriggerShellKicker();
	}

	void TriggerShellKicker()
	{
		if ( shellKickerStacks <= 0 )
			return;

		var force = ShellKickerForce * shellKickerStacks;
		var origin = WorldPosition;

		foreach ( var scrapling in Scene.GetAllComponents<ScraplingEnemy>().ToList() )
			PushEnemyFrom( scrapling, origin, force );

		foreach ( var boomcan in Scene.GetAllComponents<BoomcanEnemy>().ToList() )
			PushEnemyFrom( boomcan, origin, force );

		foreach ( var boss in Scene.GetAllComponents<JunkBruteBoss>().ToList() )
			PushEnemyFrom( boss, origin, force );

		Log.Info( $"Shell Kicker pushed nearby enemies with {force} force." );
	}

	void PushEnemyFrom( Component enemy, Vector3 origin, float force )
	{
		if ( !enemy.IsValid() )
			return;

		var away = enemy.WorldPosition - origin;
		if ( away.Length > ShellKickerRadius || away.IsNearZeroLength )
			return;

		var rigidbody = enemy.Components.Get<Rigidbody>();
		if ( rigidbody.IsValid() && rigidbody.MotionEnabled )
			rigidbody.ApplyImpulse( away.Normal * force );
		else
			enemy.WorldPosition += away.Normal * force;
	}

	void FindCameraIfNeeded()
	{
		if ( Camera.IsValid() )
			return;

		Camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( x => x.IsMainCamera );
		Camera ??= Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
	}

	void FindViewModelComponentsIfNeeded()
	{
		if ( !ViewModelRenderer.IsValid() )
		{
			ViewModelRenderer = Scene.GetAllComponents<SkinnedModelRenderer>()
				.FirstOrDefault( x => x.GameObject.Name == "ViewModel" );
		}

		if ( !PlayerController.IsValid() )
			PlayerController = Scene.GetAllComponents<ScrapspellFirstPersonController>().FirstOrDefault();

		if ( ViewModelRenderer.IsValid() )
			ViewModelRenderer.UseAnimGraph = false;
	}

	void UpdateViewModelAnimation()
	{
		FindViewModelComponentsIfNeeded();

		if ( !ViewModelRenderer.IsValid() || isReloading )
			return;

		if ( currentViewModelAnimation == ShootAnimation && !ViewModelRenderer.Sequence.IsFinished )
			return;

		var horizontalSpeed = 0.0f;
		var isMoving = false;

		if ( PlayerController.IsValid() && PlayerController.Controller.IsValid() )
		{
			horizontalSpeed = PlayerController.Controller.Velocity.WithZ( 0 ).Length;
			isMoving = PlayerController.Controller.IsOnGround
				&& horizontalSpeed > WalkAnimationSpeedThreshold;
		}

		if ( !isMoving )
		{
			PlayViewModelAnimation( BindAnimation, false );
			return;
		}

		PlayViewModelAnimation( WalkAnimation, true );

		if ( ViewModelRenderer.IsValid() )
		{
			var baseWalkSpeed = PlayerController.WalkSpeed.Clamp( 1.0f, 10000.0f );
			ViewModelRenderer.PlaybackRate = (horizontalSpeed / baseWalkSpeed).Clamp( 0.65f, 1.6f );
		}
	}

	void PlayViewModelAnimation(
		string animationName,
		bool looping,
		bool restart = false,
		float desiredDuration = 0.0f )
	{
		FindViewModelComponentsIfNeeded();

		if ( !ViewModelRenderer.IsValid() || string.IsNullOrWhiteSpace( animationName ) )
			return;

		if ( !restart && currentViewModelAnimation == animationName )
			return;

		ViewModelRenderer.Sequence.Name = animationName;
		ViewModelRenderer.Sequence.Time = 0.0f;
		ViewModelRenderer.Sequence.Looping = looping;
		ViewModelRenderer.Sequence.Blending = true;
		ViewModelRenderer.PlaybackRate = 1.0f;
		currentViewModelAnimation = animationName;

		if ( desiredDuration > 0.0f && ViewModelRenderer.Sequence.Duration > 0.0f )
		{
			ViewModelRenderer.PlaybackRate =
				ViewModelRenderer.Sequence.Duration / desiredDuration;
		}
	}
}
