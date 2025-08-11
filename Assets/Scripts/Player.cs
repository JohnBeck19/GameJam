using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }
    [Header("Health")]
    [SerializeField] private float maxHealth { set; get; } = 100f;
    [SerializeField] private float currentHealth = 100f;
    private float dmgReduction = 1f;
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
    [SerializeField] private Collider2D[] playerColliders; // Optional override; if null, auto-populate
    
    // Private variables
    private Vector2 moveInput;
    private Vector2 currentVelocity;
    private bool isMoving;
    private Vector2 facingDirection = Vector2.right; // Updated each frame to point toward cursor
    
    // Dash variables
    private bool isDashing;
    private bool canDash = true;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private bool[] originalColliderIsTriggerStates;
    
    // Transient input
    private bool ownerDashPressedThisFrame;
    
    // Input system
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;


    //Items
    [SerializeField] Animator animator;

    [Header("Severity Visuals")]
    [SerializeField] private bool useSeverityTint = true;
    [Tooltip("Color over severity (0..1). If not set, defaults to white->green.")]
    [SerializeField] private Gradient severityTintGradient;
    [SerializeField] private bool useSeveritySpriteSwap = false;
    [Tooltip("Sprites ordered from low severity (index 0) to high severity (last).")]
    [SerializeField] private List<Sprite> severitySprites = new List<Sprite>();
    [Tooltip("If true, disables Animator while sprite swapping is active to prevent it from overriding sprites.")]
    [SerializeField] private bool disableAnimatorWhenSwapping = false;

    [Header("Severity Animator Drive")]
    [Tooltip("If true, drives Animator parameters based on severity instead of swapping sprites.")]
    [SerializeField] private bool useAnimatorSeverity = false;
    [Tooltip("Float parameter name to receive severity 0..1. Leave empty to skip setting float.")]
    [SerializeField] private string animatorSeverityFloatParam = "Severity01";
    [Tooltip("If true, also set an integer severity level param computed from 0..(levels-1).")]
    [SerializeField] private bool useAnimatorIntLevels = false;
    [Tooltip("Integer parameter name to receive the severity level index.")]
    [SerializeField] private string animatorSeverityIntParam = "SeverityLevel";
    [Tooltip("Number of discrete levels for the int param. 2=low/high, 3=low/med/high, etc.")]
    [SerializeField, Range(2, 12)] private int animatorLevels = 3;

    [Header("Severity Controller Swap")]
    [Tooltip("If true, swaps the entire Animator Controller based on severity.")]
    [SerializeField] private bool useAnimatorControllerSwap = false;
    [Tooltip("Animator Controllers ordered from low severity (index 0) to high severity (last).")]
    [SerializeField] private List<RuntimeAnimatorController> severityControllers = new List<RuntimeAnimatorController>();
    [Tooltip("If true, Rebind/Update the Animator on each controller swap to immediately reflect the first state.")]
    [SerializeField] private bool rebindAnimatorOnControllerSwap = true;

    private Color baseSpriteColor = Color.white;
    private Sprite baseSprite;
    private RuntimeAnimatorController baseController;
    private int lastControllerIndex = -1;

    //Audio Source and Clips
    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip playerHitSound;
    [SerializeField] AudioClip playerAttackSound;
    [SerializeField] AudioClip playerRangeAttackSound;


    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize health
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // Get or add required components
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
            
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseSpriteColor = spriteRenderer.color;
            baseSprite = spriteRenderer.sprite;
        }
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator != null)
        {
            baseController = animator.runtimeAnimatorController;
        }
            
        // Setup Rigidbody2D for top-down movement
        SetupRigidbody();
        SetupColliders();
        
        // Setup input system
        SetupInput();

        audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    void SetupRigidbody()
    {
        rb.gravityScale = 0f; // No gravity for top-down
        rb.linearDamping = 0f; // We'll handle deceleration manually
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Prevent rotation from physics
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Smooth visuals when physics runs in FixedUpdate
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

    void SetupColliders()
    {
        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponents<Collider2D>();
        }

        if (playerColliders != null && playerColliders.Length > 0)
        {
            originalColliderIsTriggerStates = new bool[playerColliders.Length];
            for (int i = 0; i < playerColliders.Length; i++)
            {
                originalColliderIsTriggerStates[i] = playerColliders[i] != null && playerColliders[i].isTrigger;
            }
        }
    }
    
    void Update()
    {
        // Local-only: read input, aim, and dash
        HandleInput();
        HandleDashInput();
        UpdateFacingDirection();

        if (ownerDashPressedThisFrame)
        {
            ownerDashPressedThisFrame = false;
            TryDash();
        }

        UpdateDashTimers();
        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
    }

    void LateUpdate()
    {
        // Apply severity visuals after Animator updates so sprite swaps stick
        UpdateSeverityVisuals();
    }

    
    void FixedUpdate()
    {
        // Local-only: apply movement and rotation based on current input
        isMoving = moveInput.magnitude > 0.1f;
        HandleMovement();
        HandleRotation();      
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
            // WASD movement in world space, independent of aim
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
        if (facingDirection.sqrMagnitude < 0.0001f)
            return;
        
        // Calculate the angle to rotate towards (point toward cursor)
        float targetAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
        
        // Smoothly rotate towards the target angle
        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
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
        
        ownerDashPressedThisFrame = dashPressed;
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
        
        // Dash in the current movement direction; if no input, dash toward cursor (aim)
        Vector2 worldMove = isMoving ? moveInput : facingDirection;
        dashDirection = worldMove.sqrMagnitude > 0.0001f ? worldMove.normalized : Vector2.right;
        
        Debug.Log("Dash started! Direction: " + dashDirection);

        // Allow phasing through walls during dash
        SetDashPhasing(true);
    }
    
    void EndDash()
    {
        isDashing = false;
        dashCooldownTimer = dashCooldown;
        
        Debug.Log("Dash ended!");

        // Restore normal collisions after dash
        SetDashPhasing(false);
    }

    void SetDashPhasing(bool enable)
    {
        if (playerColliders == null || playerColliders.Length == 0)
            return;

        if (enable)
        {
            for (int i = 0; i < playerColliders.Length; i++)
            {
                var col = playerColliders[i];
                if (col == null) continue;
                col.isTrigger = true;
            }
        }
        else
        {
            // Restore original trigger states
            for (int i = 0; i < playerColliders.Length; i++)
            {
                var col = playerColliders[i];
                if (col == null) continue;
                bool restoreTrigger = originalColliderIsTriggerStates != null && i < originalColliderIsTriggerStates.Length && originalColliderIsTriggerStates[i];
                col.isTrigger = restoreTrigger;
            }
        }
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

    // --- Health accessors for UI ---
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    // --- Cursor/Facing helpers ---
    void UpdateFacingDirection()
    {
        if (!TryGetMouseWorldPosition(out Vector3 mouseWorld))
            return;
        
        Vector2 toMouse = ((Vector2)mouseWorld - (Vector2)transform.position);
        if (toMouse.sqrMagnitude > 0.0001f)
        {
            facingDirection = toMouse.normalized;
        }
    }
    
    bool TryGetMouseWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null)
            return false;
        
        Vector2 screenPos;
        #if ENABLE_INPUT_SYSTEM
        if (playerInput != null && UnityEngine.InputSystem.Mouse.current != null)
        {
            screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        else
        #endif
        {
            screenPos = Input.mousePosition;
        }
        
        float zDistance = transform.position.z - cam.transform.position.z;
        Vector3 sp = new Vector3(screenPos.x, screenPos.y, zDistance);
        worldPosition = cam.ScreenToWorldPoint(sp);
        worldPosition.z = transform.position.z;
        return true;
    }


    //called when player gets a new weapon
    public void NewWeapon(string weaponName)
    {

    }

    public void TakeDmg(float dmg)
    {
        currentHealth -= dmg * dmgReduction;
        audioSource.PlayOneShot(playerHitSound);
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    public void PlusDmgReduction(float reduction)
    {
        if(dmgReduction > .2f) dmgReduction -= reduction;
    }

    public void ResetState()
    {
        // Restore core gameplay state to starting values
        currentHealth = maxHealth;
        dmgReduction = 1f;

        moveInput = Vector2.zero;
        currentVelocity = Vector2.zero;
        isMoving = false;
        facingDirection = Vector2.right;

        isDashing = false;
        canDash = true;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        dashDirection = Vector2.zero;
        SetDashPhasing(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Reset rotation
        transform.rotation = Quaternion.identity;

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            if (disableAnimatorWhenSwapping)
            {
                animator.enabled = true;
            }
            if (useAnimatorControllerSwap && baseController != null)
            {
                animator.runtimeAnimatorController = baseController;
                lastControllerIndex = -1;
            }
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseSpriteColor;
            if (useSeveritySpriteSwap && baseSprite != null)
            {
                spriteRenderer.sprite = baseSprite;
            }
        }
        _isDead = false;
    }

    private bool _isDead;
    public void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Optionally disable input/collisions immediately
        SetDashPhasing(false);
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Notify GameManager to handle reset and scene transition
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnPlayerDied();
        }
        else
        {
            // Fallback: just reload current scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    private void UpdateSeverityVisuals()
    {
        var gm = GameManager.Instance;
        // Use base severity for sprite visuals
        float severity = gm != null ? gm.Severity01 : 0f;
        // Use visual severity (includes time-based component) for Animator-driven forms
        float visualSeverity = gm != null ? gm.VisualSeverity01 : severity;

        // Drive Animator based on visual severity if configured
        UpdateSeverityAnimator(visualSeverity);

        if (spriteRenderer != null)
        {
            if (useSeverityTint)
            {
                Color target = severityTintGradient != null ? severityTintGradient.Evaluate(severity) : Color.Lerp(Color.white, Color.green, severity);
                spriteRenderer.color = Color.Lerp(baseSpriteColor, target, severity);
            }

            if (useSeveritySpriteSwap && severitySprites != null && severitySprites.Count > 0)
            {
                if (disableAnimatorWhenSwapping && animator != null && animator.enabled)
                {
                    animator.enabled = false;
                }
                int count = severitySprites.Count;
                int idx = Mathf.Clamp(Mathf.FloorToInt(severity * (count - 1 + 0.0001f)), 0, count - 1);
                var s = severitySprites[idx];
                if (s != null && spriteRenderer.sprite != s)
                {
                    spriteRenderer.sprite = s;
                }
            }
        }
    }

    private void UpdateSeverityAnimator(float t)
    {
        if (animator == null)
            return;

        // Optional float 0..1 parameter
        if (useAnimatorSeverity && !string.IsNullOrEmpty(animatorSeverityFloatParam))
        {
            animator.SetFloat(animatorSeverityFloatParam, t);
        }

        // Optional integer level 0..(levels-1) — works even if useAnimatorSeverity is false
        if (useAnimatorIntLevels && !string.IsNullOrEmpty(animatorSeverityIntParam))
        {
            int levels = Mathf.Max(2, animatorLevels);
            int levelIndex = Mathf.Clamp(Mathf.FloorToInt(t * (levels - 1 + 0.0001f)), 0, levels - 1);
            animator.SetInteger(animatorSeverityIntParam, levelIndex);
        }

        // Optional controller swap
        if (useAnimatorControllerSwap && severityControllers != null && severityControllers.Count > 0)
        {
            SwapAnimatorControllerIfNeeded(t);
        }
    }

    private void SwapAnimatorControllerIfNeeded(float t)
    {
        if (animator == null) return;
        int count = severityControllers.Count;
        int idx = Mathf.Clamp(Mathf.FloorToInt(t * (count - 1 + 0.0001f)), 0, count - 1);
        if (idx == lastControllerIndex) return;
        var ctrl = severityControllers[idx];
        if (ctrl == null) return;
        animator.runtimeAnimatorController = ctrl;
        lastControllerIndex = idx;
        if (rebindAnimatorOnControllerSwap)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }
}
