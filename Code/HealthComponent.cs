/// <summary>
/// Tiny prototype health component for anything the PipeBlaster can damage.
/// Attach this to enemies, props, or test targets.
/// </summary>
public sealed class HealthComponent : Component, Component.IDamageable
{
	[Property] public float MaxHealth { get; set; } = 100.0f;
	[Property] public float CurrentHealth { get; set; } = 100.0f;
	[Property] public bool DestroyOnDeath { get; set; } = true;

	public bool IsAlive => CurrentHealth > 0.0f;
	public event System.Action<HealthComponent> Died;

	protected override void OnStart()
	{
		base.OnStart();

		if ( CurrentHealth <= 0.0f )
			CurrentHealth = MaxHealth;
	}

	/// <summary>
	/// Simple damage entry point for beginner-friendly gameplay code.
	/// </summary>
	public void TakeDamage( float amount )
	{
		if ( amount <= 0.0f || !IsAlive )
			return;

		CurrentHealth = (CurrentHealth - amount).Clamp( 0.0f, MaxHealth );
		Log.Info( $"{GameObject.Name} took {amount} damage. Health: {CurrentHealth}/{MaxHealth}" );

		if ( CurrentHealth <= 0.0f )
			Die();
	}

	public void Heal( float amount )
	{
		if ( amount <= 0.0f || !IsAlive )
			return;

		var healthBefore = CurrentHealth;
		CurrentHealth = (CurrentHealth + amount).Clamp( 0.0f, MaxHealth );
		Log.Info( $"{GameObject.Name} healed for {CurrentHealth - healthBefore:0.##}. Health: {CurrentHealth}/{MaxHealth}" );
	}

	public void ResetHealth()
	{
		CurrentHealth = MaxHealth;
		Log.Info( $"{GameObject.Name} health reset to {CurrentHealth}." );
	}

	/// <summary>
	/// Sandbox damage interface support, so later systems can also damage this object.
	/// </summary>
	public void OnDamage( in DamageInfo damage )
	{
		TakeDamage( damage.Damage );
	}

	void Die()
	{
		Log.Info( $"{GameObject.Name} died." );
		Died?.Invoke( this );

		if ( DestroyOnDeath )
			GameObject.Destroy();
	}
}
