using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;
    [SerializeField] private float rotationSpeed = 720f; // Degrees per second
    
    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.5f;
    
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    // Private variables
    private Vector2 moveInput;
    private Vector2 currentVelocity;
    private bool isMoving;
    
    // Dash variables
    private bool isDashing;
    private bool canDash = true;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    
    // Input system
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    
    void Awake()
    {
        // Get or add required components
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
            
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
            
        // Setup Rigidbody2D for top-down movement
        SetupRigidbody();
        
        // Setup input system
        SetupInput();
    }
    
    void SetupRigidbody()
    {
        rb.gravityScale = 0f; // No gravity for top-down
        rb.linearDamping = 0f; // We'll handle deceleration manually
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Prevent rotation from physics
    }
    
    void SetupInput()
    {
        // Try to get PlayerInput component
        playerInput = GetComponent<PlayerInput>();
        
        // If no PlayerInput component exists, we'll use legacy input
        if (playerInput == null)
        {
            Debug.Log("No PlayerInput component found. Using legacy input system.");
        }
        else
        {
            // Setup input actions
            moveAction = playerInput.actions["Move"];
            jumpAction = playerInput.actions["Jump"];
        }
    }
    
    void Update()
    {
        // Handle input
        HandleInput();
        
        // Handle dash input
        HandleDashInput();
        
        // Handle rotation towards movement direction
        HandleRotation();
        
        // Update dash timers
        UpdateDashTimers();
    }
    
    void FixedUpdate()
    {
        // Handle movement in FixedUpdate for consistent physics
        HandleMovement();
    }
    
    void HandleInput()
    {
        if (playerInput != null && moveAction != null)
        {
            // Use new input system
            moveInput = moveAction.ReadValue<Vector2>();
        }
        else
        {
            // Use legacy input system
            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");
        }
        
        // Normalize input to prevent faster diagonal movement
        if (moveInput.magnitude > 1f)
        {
            moveInput.Normalize();
        }
        
        // Check if player is moving
        isMoving = moveInput.magnitude > 0.1f;
    }
    
    void HandleMovement()
    {
        Vector2 targetVelocity = Vector2.zero;
        
        if (isDashing)
        {
            // During dash, maintain dash velocity
            currentVelocity = dashDirection * dashSpeed;
        }
        else if (isMoving)
        {
            // Calculate target velocity based on input
            targetVelocity = moveInput * moveSpeed;
            
            // Smoothly accelerate towards target velocity
            currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            // Smoothly decelerate when not moving
            currentVelocity = Vector2.MoveTowards(currentVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
        }
        
        // Apply velocity to rigidbody
        rb.linearVelocity = currentVelocity;
    }
    
    void HandleRotation()
    {
        if (isMoving)
        {
            // Calculate the angle to rotate towards
            float targetAngle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
            
            // Smoothly rotate towards the target angle
            float currentAngle = transform.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
            
            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }
    }
    
    void HandleDashInput()
    {
        bool dashPressed = false;
        
        if (playerInput != null && jumpAction != null)
        {
            // Use new input system - use Jump action for dash
            dashPressed = jumpAction.WasPressedThisFrame();
        }
        else
        {
            // Fallback to legacy input system
            dashPressed = Input.GetKeyDown(KeyCode.Space);
        }
        
        if (dashPressed)
        {
            TryDash();
        }
    }
    
    void TryDash()
    {
        // Only dash if we can dash, are moving, and not already dashing
        if (canDash && isMoving && !isDashing)
        {
            StartDash();
        }
    }
    
    void StartDash()
    {
        isDashing = true;
        canDash = false;
        dashTimer = dashDuration;
        
        // Set dash direction to the direction the player is facing (based on movement input)
        dashDirection = moveInput.normalized;
        
        Debug.Log("Dash started! Direction: " + dashDirection);
    }
    
    void EndDash()
    {
        isDashing = false;
        dashCooldownTimer = dashCooldown;
        
        Debug.Log("Dash ended!");
    }
    
    void UpdateDashTimers()
    {
        // Update dash duration timer
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                EndDash();
            }
        }
        
        // Update dash cooldown timer
        if (!canDash && !isDashing)
        {
            dashCooldownTimer -= Time.deltaTime;
            if (dashCooldownTimer <= 0f)
            {
                canDash = true;
            }
        }
    }
    
    // Public methods for external access
    public Vector2 GetMoveInput() => moveInput;
    public Vector2 GetVelocity() => currentVelocity;
    public bool IsMoving() => isMoving;
    public bool IsDashing() => isDashing;
    public bool CanDash() => canDash;
    public float GetDashCooldownRemaining() => Mathf.Max(0f, dashCooldownTimer);
    
    // Optional: Visual feedback methods
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }
    
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
    
    // Optional: Animation support
    public Vector2 GetNormalizedInput()
    {
        return moveInput.normalized;
    }
}
