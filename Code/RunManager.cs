public enum RunState
{
	WaitingToStart,
	InWave,
	ChoosingUpgrade,
	Victory,
	PlayerDead
}

/// <summary>
/// Owns the high-level run flow. Other gameplay systems report events to this component.
/// </summary>
public sealed class RunManager : Component
{
	[Property] public EnemySpawner Spawner { get; set; }
	[Property] public UpgradeChoiceUI UpgradeUI { get; set; }
	[Property] public HealthComponent PlayerHealth { get; set; }
	[Property] public ScrapWallet PlayerWallet { get; set; }
	[Property] public PipeBlaster Weapon { get; set; }

	[Property] public RunState State { get; private set; } = RunState.WaitingToStart;

	EnemySpawner subscribedSpawner;
	UpgradeChoiceUI subscribedUpgradeUI;
	HealthComponent subscribedHealth;

	protected override void OnStart()
	{
		base.OnStart();

		FindReferencesIfNeeded();
		SubscribeToSystems();
		PrepareWaitingState();
	}

	protected override void OnUpdate()
	{
		FindReferencesIfNeeded();
		SubscribeToSystems();

		// Temporary debug control: press 0 to start or restart a run.
		if ( Input.Pressed( "Slot0" ) )
		{
			if ( State == RunState.WaitingToStart )
				StartRun();
			else if ( State is RunState.Victory or RunState.PlayerDead )
				RestartRun();
		}
	}

	protected override void OnDisabled()
	{
		UnsubscribeFromSystems();
		base.OnDisabled();
	}

	public void StartRun()
	{
		if ( State != RunState.WaitingToStart )
			return;

		Spawner?.ResetRun();
		PlayerHealth?.ResetHealth();
		PlayerWallet?.ResetWallet();
		Weapon?.ResetForNewRun();
		UpgradeUI?.Close();

		StartNextWave();
		Log.Info( "Scrapspell run started." );
	}

	public void RestartRun()
	{
		Spawner?.ResetRun();
		PlayerHealth?.ResetHealth();
		PlayerWallet?.ResetWallet();
		Weapon?.ResetForNewRun();
		UpgradeUI?.Close();

		State = RunState.WaitingToStart;
		StartRun();
		Log.Info( "Scrapspell run restarted." );
	}

	void StartNextWave()
	{
		State = RunState.InWave;
		Spawner?.SetPaused( false );
		if ( Weapon.IsValid() )
			Weapon.InputEnabled = true;

		Spawner?.StartWave();
	}

	void HandleRoomCleared()
	{
		if ( State != RunState.InWave )
			return;

		if ( Spawner.IsValid() && Spawner.IsFinalWaveComplete )
		{
			State = RunState.Victory;
			Spawner.SetPaused( true );
			if ( Weapon.IsValid() )
				Weapon.InputEnabled = false;

			Log.Info( "Run victory. Final boss wave cleared." );
			return;
		}

		State = RunState.ChoosingUpgrade;
		Spawner?.SetPaused( true );
		if ( Weapon.IsValid() )
			Weapon.InputEnabled = false;

		UpgradeUI?.Open();
		Log.Info( "Run state changed to ChoosingUpgrade." );
	}

	void HandleUpgradeChosen()
	{
		if ( State != RunState.ChoosingUpgrade )
			return;

		StartNextWave();
	}

	void HandlePlayerDied( HealthComponent health )
	{
		if ( State is RunState.PlayerDead or RunState.Victory )
			return;

		State = RunState.PlayerDead;
		Spawner?.SetPaused( true );
		UpgradeUI?.Close();
		if ( Weapon.IsValid() )
			Weapon.InputEnabled = false;

		Log.Info( "Run ended because the player died." );
	}

	void PrepareWaitingState()
	{
		State = RunState.WaitingToStart;
		Spawner?.ResetRun();
		Spawner?.SetPaused( true );
		UpgradeUI?.Close();
		if ( Weapon.IsValid() )
			Weapon.InputEnabled = false;
	}

	void FindReferencesIfNeeded()
	{
		Spawner ??= Scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
		if ( Spawner.IsValid() )
			Spawner.StartOnPlay = false;
		UpgradeUI ??= Scene.GetAllComponents<UpgradeChoiceUI>().FirstOrDefault();
		PlayerWallet ??= Scene.GetAllComponents<ScrapWallet>().FirstOrDefault();
		Weapon ??= Scene.GetAllComponents<PipeBlaster>().FirstOrDefault();

		if ( !PlayerHealth.IsValid() )
		{
			var player = Scene.Directory.FindByName( "Player" ).FirstOrDefault();
			PlayerHealth = player?.Components.Get<HealthComponent>();
		}
	}

	void SubscribeToSystems()
	{
		if ( Spawner.IsValid() && subscribedSpawner != Spawner )
		{
			if ( subscribedSpawner.IsValid() )
				subscribedSpawner.RoomCleared -= HandleRoomCleared;

			subscribedSpawner = Spawner;
			subscribedSpawner.RoomCleared += HandleRoomCleared;
		}

		if ( UpgradeUI.IsValid() && subscribedUpgradeUI != UpgradeUI )
		{
			if ( subscribedUpgradeUI.IsValid() )
				subscribedUpgradeUI.UpgradeChosen -= HandleUpgradeChosen;

			subscribedUpgradeUI = UpgradeUI;
			subscribedUpgradeUI.UpgradeChosen += HandleUpgradeChosen;
		}

		if ( PlayerHealth.IsValid() && subscribedHealth != PlayerHealth )
		{
			if ( subscribedHealth.IsValid() )
				subscribedHealth.Died -= HandlePlayerDied;

			subscribedHealth = PlayerHealth;
			subscribedHealth.Died += HandlePlayerDied;
		}
	}

	void UnsubscribeFromSystems()
	{
		if ( subscribedSpawner.IsValid() )
			subscribedSpawner.RoomCleared -= HandleRoomCleared;

		if ( subscribedUpgradeUI.IsValid() )
			subscribedUpgradeUI.UpgradeChosen -= HandleUpgradeChosen;

		if ( subscribedHealth.IsValid() )
			subscribedHealth.Died -= HandlePlayerDied;
	}
}
