public sealed class ScrapspellFirstPersonController : Component
{
	[RequireComponent] public CharacterController Controller { get; set; }

	[Property] public CameraComponent Camera { get; set; }

	[Property] public float WalkSpeed { get; set; } = 180.0f;
	[Property] public float RunSpeed { get; set; } = 320.0f;
	[Property] public float SlowWalkSpeed { get; set; } = 95.0f;
	[Property] public float CrouchSpeed { get; set; } = 85.0f;
	[Property] public float JumpSpeed { get; set; } = 300.0f;
	[Property] public float AirControl { get; set; } = 55.0f;

	[Property] public float StandingHeight { get; set; } = 72.0f;
	[Property] public float CrouchingHeight { get; set; } = 44.0f;
	[Property] public float StandingEyeHeight { get; set; } = 64.0f;
	[Property] public float CrouchingEyeHeight { get; set; } = 38.0f;
	[Property] public float CrouchBlendSpeed { get; set; } = 12.0f;

	[Property] public float Radius { get; set; } = 16.0f;
	[Property] public float StepHeight { get; set; } = 18.0f;
	[Property] public float GroundAngle { get; set; } = 46.0f;
	[Property] public float GroundFriction { get; set; } = 6.0f;
	[Property] public float AirFriction { get; set; } = 0.15f;
	[Property] public Vector3 Gravity { get; set; } = new( 0, 0, 800 );

	[Property] public float LookSensitivity { get; set; } = 1.0f;
	[Property] public float MaxPitch { get; set; } = 89.0f;
	[Property] public float FieldOfView { get; set; } = 75.0f;

	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public Vector3 WishVelocity { get; set; }
	[Sync] public bool IsCrouching { get; set; }
	[Sync] public bool IsRunning { get; set; }

	float currentEyeHeight;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		currentEyeHeight = StandingEyeHeight;
		FindCamera();

		if ( IsProxy )
			return;

		EyeAngles = WorldRotation.Angles();
		EyeAngles = EyeAngles.WithPitch( 0 );
		EyeAngles = EyeAngles.WithRoll( 0 );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		UpdateLook();
		UpdateCrouch();
		UpdateCamera();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		UpdateControllerSettings();
		BuildWishVelocity();

		if ( Controller.IsOnGround && Input.Pressed( "Jump" ) && !IsCrouching )
		{
			Controller.Punch( Vector3.Up * JumpSpeed );
		}

		if ( Controller.IsOnGround )
		{
			Controller.Velocity = Controller.Velocity.WithZ( 0 );
			Controller.Accelerate( WishVelocity );
			Controller.ApplyFriction( GroundFriction );
		}
		else
		{
			Controller.Velocity -= Gravity * Time.Delta * 0.5f;
			Controller.Accelerate( WishVelocity.ClampLength( AirControl ) );
			Controller.ApplyFriction( AirFriction );
		}

		Controller.Move();

		if ( Controller.IsOnGround )
		{
			Controller.Velocity = Controller.Velocity.WithZ( 0 );
		}
		else
		{
			Controller.Velocity -= Gravity * Time.Delta * 0.5f;
		}
	}

	void UpdateLook()
	{
		EyeAngles += Input.AnalogLook * LookSensitivity;
		EyeAngles = EyeAngles.WithPitch( EyeAngles.pitch.Clamp( -MaxPitch, MaxPitch ) );
		EyeAngles = EyeAngles.WithRoll( 0 );
	}

	void UpdateCrouch()
	{
		IsCrouching = Input.Down( "Duck" );
		IsRunning = Input.Down( "Run" ) && !IsCrouching && !Input.Down( "Walk" );
	}

	void UpdateCamera()
	{
		if ( !Camera.IsValid() )
			FindCamera();

		if ( !Camera.IsValid() )
			return;

		var targetEyeHeight = IsCrouching ? CrouchingEyeHeight : StandingEyeHeight;
		var blend = MathX.Clamp( Time.Delta * CrouchBlendSpeed, 0.0f, 1.0f );
		currentEyeHeight = MathX.Lerp( currentEyeHeight, targetEyeHeight, blend );

		Camera.WorldPosition = WorldPosition + Vector3.Up * currentEyeHeight;
		Camera.WorldRotation = EyeAngles.ToRotation();
		Camera.FieldOfView = FieldOfView;
	}

	void UpdateControllerSettings()
	{
		Controller.Radius = Radius;
		Controller.Height = IsCrouching ? CrouchingHeight : StandingHeight;
		Controller.StepHeight = StepHeight;
		Controller.GroundAngle = GroundAngle;
	}

	void BuildWishVelocity()
	{
		var moveInput = Input.AnalogMove.WithZ( 0 );
		var wishDirection = Rotation.FromYaw( EyeAngles.yaw ) * moveInput;
		wishDirection = wishDirection.WithZ( 0 );

		if ( !wishDirection.IsNearZeroLength )
			wishDirection = wishDirection.Normal;

		WishVelocity = wishDirection * GetWishSpeed();
	}

	float GetWishSpeed()
	{
		if ( IsCrouching )
			return CrouchSpeed;

		if ( Input.Down( "Walk" ) )
			return SlowWalkSpeed;

		if ( Input.Down( "Run" ) )
			return RunSpeed;

		return WalkSpeed;
	}

	void FindCamera()
	{
		Camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( x => x.IsMainCamera );
		Camera ??= Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
	}
}
