/// <summary>
/// One enemy type and its spawn settings inside a wave.
/// </summary>
public sealed class WaveEnemyConfig
{
	[Property] public GameObject EnemyPrefab { get; set; }
	[Property] public int Amount { get; set; } = 1;
	[Property] public float SpawnDelay { get; set; } = 0.5f;
}

/// <summary>
/// Inspector-authored collection of enemy types for one wave.
/// </summary>
public sealed class EnemyWaveConfig
{
	[Property, InlineEditor] public List<WaveEnemyConfig> Enemies { get; set; } = new();
}

/// <summary>
/// Simple wave spawner for one combat room.
/// Waves are authored by hand in the inspector; there is no procedural generation yet.
/// </summary>
public sealed class EnemySpawner : Component
{
	[Property, InlineEditor] public List<EnemyWaveConfig> Waves { get; set; } = new();
	[Property] public List<GameObject> SpawnPoints { get; set; } = new();
	[Property] public bool StartOnPlay { get; set; } = false;

	[Property] public int CurrentWave { get; private set; }

	/// <summary>
	/// UpgradeChoiceUI subscribes to this and opens after every completed wave.
	/// </summary>
	public event System.Action RoomCleared;

	readonly List<GameObject> spawnedEnemies = new();
	readonly List<WaveSpawnRequest> spawnQueue = new();

	int nextSpawnIndex;
	int nextSpawnPointIndex;
	int enemiesReportedDead;
	float nextSpawnTime;
	bool waveActive;
	bool waveFinishedSpawning;

	public bool IsPaused { get; private set; }
	public bool IsFinalWaveComplete => CurrentWave >= Waves.Count;

	protected override void OnStart()
	{
		base.OnStart();

		if ( StartOnPlay )
			StartWave();
	}

	protected override void OnUpdate()
	{
		if ( !waveActive || IsPaused )
			return;

		SpawnNextEnemyIfReady();
		CheckForDeadEnemies();
		CheckForWaveCleared();
	}

	/// <summary>
	/// Starts the next configured wave. CurrentWave is displayed as a one-based number.
	/// </summary>
	public void StartWave()
	{
		if ( waveActive )
			return;

		if ( SpawnPoints.Count == 0 )
		{
			Log.Warning( "EnemySpawner needs at least one spawn point." );
			return;
		}

		if ( CurrentWave >= Waves.Count )
		{
			Log.Info( "All configured enemy waves are complete." );
			return;
		}

		var config = Waves[CurrentWave];
		BuildSpawnQueue( config );

		CurrentWave++;
		spawnedEnemies.Clear();
		nextSpawnIndex = 0;
		nextSpawnPointIndex = 0;
		enemiesReportedDead = 0;
		nextSpawnTime = Time.Now;
		waveFinishedSpawning = spawnQueue.Count == 0;
		waveActive = true;

		Log.Info( $"Wave {CurrentWave} started with {spawnQueue.Count} enemies." );
	}

	public void SetPaused( bool paused )
	{
		IsPaused = paused;
		Log.Info( paused ? "Enemy spawning paused." : "Enemy spawning resumed." );
	}

	public void ResetRun()
	{
		foreach ( var enemy in spawnedEnemies.ToList() )
		{
			if ( enemy.IsValid() )
				enemy.Destroy();
		}

		spawnedEnemies.Clear();
		spawnQueue.Clear();
		CurrentWave = 0;
		nextSpawnIndex = 0;
		nextSpawnPointIndex = 0;
		enemiesReportedDead = 0;
		waveActive = false;
		waveFinishedSpawning = false;
		IsPaused = false;

		Log.Info( "EnemySpawner reset for a new run." );
	}

	void BuildSpawnQueue( EnemyWaveConfig wave )
	{
		spawnQueue.Clear();

		if ( wave is null )
			return;

		foreach ( var enemyConfig in wave.Enemies )
		{
			if ( enemyConfig is null || !enemyConfig.EnemyPrefab.IsValid() )
				continue;

			var amount = enemyConfig.Amount.Clamp( 0, 1000 );
			var delay = enemyConfig.SpawnDelay.Clamp( 0.0f, 60.0f );

			for ( var i = 0; i < amount; i++ )
			{
				spawnQueue.Add( new WaveSpawnRequest( enemyConfig.EnemyPrefab, delay ) );
			}
		}
	}

	void SpawnNextEnemyIfReady()
	{
		if ( waveFinishedSpawning || Time.Now < nextSpawnTime )
			return;

		var request = spawnQueue[nextSpawnIndex];
		var spawnPoint = GetNextValidSpawnPoint();

		if ( !spawnPoint.IsValid() )
		{
			Log.Warning( "EnemySpawner could not find a valid spawn point." );
			waveActive = false;
			return;
		}

		var enemy = request.Prefab.Clone( spawnPoint.WorldTransform );
		spawnedEnemies.Add( enemy );
		nextSpawnIndex++;

		Log.Info(
			$"Wave {CurrentWave}: spawned {request.Prefab.Name} " +
			$"({nextSpawnIndex}/{spawnQueue.Count}) at {spawnPoint.Name}."
		);

		if ( nextSpawnIndex >= spawnQueue.Count )
		{
			waveFinishedSpawning = true;
		}
		else
		{
			nextSpawnTime = Time.Now + request.Delay;
		}
	}

	void CheckForDeadEnemies()
	{
		var deadCount = spawnedEnemies.Count( enemy => !enemy.IsValid() );

		while ( enemiesReportedDead < deadCount )
		{
			enemiesReportedDead++;
			Log.Info( $"Wave {CurrentWave}: enemy died ({enemiesReportedDead}/{spawnQueue.Count})." );
		}
	}

	void CheckForWaveCleared()
	{
		if ( !waveFinishedSpawning )
			return;

		if ( spawnedEnemies.Any( enemy => enemy.IsValid() ) )
			return;

		waveActive = false;
		Log.Info( $"Wave {CurrentWave} cleared." );
		OnRoomCleared();
	}

	public void OnRoomCleared()
	{
		RoomCleared?.Invoke();
	}

	public void NotifyBossDied( GameObject boss )
	{
		var bossName = boss.IsValid() ? boss.Name : "Junk Brute";
		Log.Info( $"EnemySpawner was notified that boss {bossName} died." );
	}

	GameObject GetNextValidSpawnPoint()
	{
		for ( var i = 0; i < SpawnPoints.Count; i++ )
		{
			var spawnPoint = SpawnPoints[nextSpawnPointIndex % SpawnPoints.Count];
			nextSpawnPointIndex++;

			if ( spawnPoint.IsValid() )
				return spawnPoint;
		}

		return null;
	}

	sealed class WaveSpawnRequest
	{
		public GameObject Prefab { get; }
		public float Delay { get; }

		public WaveSpawnRequest( GameObject prefab, float delay )
		{
			Prefab = prefab;
			Delay = delay;
		}
	}
}
