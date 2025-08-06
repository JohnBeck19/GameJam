using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;
    [SerializeField] private float rotationSpeed = 720f; // Degrees per second
    
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    // Private variables
    private Vector2 moveInput;
    private Vector2 currentVelocity;
    private bool isMoving;
    
    // Input system
    private PlayerInput playerInput;
    private InputAction moveAction;
    
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
        }
    }
    
    void Update()
    {
        // Handle input
        HandleInput();
        
        // Handle rotation towards movement direction
        HandleRotation();
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
        
        if (isMoving)
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
    
    // Public methods for external access
    public Vector2 GetMoveInput() => moveInput;
    public Vector2 GetVelocity() => currentVelocity;
    public bool IsMoving() => isMoving;
    
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
