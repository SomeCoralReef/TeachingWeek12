using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public class playerScript : MonoBehaviour
{
    private float horizontal;
    private float speed = 8f;
    private float jumpingPower = 16f;

    private bool isFacingToRight = true;

    private bool isWallSliding;
    private float wallSlidingSpeed = 2f;

    private TrailRenderer _trailRenderer;
    private float acceleration = 10f;
    private float decceleration = 5f;


    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask wallLayer;

    [Header("Coyote Time")]
    private float coyoteTime = 0.2f;
    private float coyoteTimeCounter;

    [Header("Jump Buffer")]
    private float jumpBufferTime = 0.2f;
    private float jumpBufferCounter;

    [Header("Dashing")]
    [SerializeField] private float dashPower = 24f;
    [SerializeField] private float dashTime = 0.4f;
    private float dashCooldown = 1.0f;
    private bool _isDashing;
    private bool  _canDash = true;

    private bool isWallJumping;
    private float wallJumpingDirection;
    private float wallJumpingTime = 0.2f;
    private float wallJumpingCounter;
    private float wallJumpingDuration = 0.4f;
    private Vector2 wallJumpingPower = new Vector2(10f, 10f);

    [Header("Grapple")]
    [SerializeField] private Transform grapplePoint;
    [SerializeField] private LayerMask grappleLayer;
    [SerializeField] private float grappleDistance = 50f;
    [SerializeField] private float grappleTime = 1.0f;
    [SerializeField] private float grapplePower = 10f;
    private bool _isGrappling;

    GameObject UIindicator;

    [Header("Squash & Stretch")]
    [SerializeField] private Transform visualTransform; // assign your visual sprite/mesh here
    [SerializeField] private float squashAmount = 0.2f;
    [SerializeField] private float stretchAmount = 0.2f;
    [SerializeField] private float stretchSpeed = 10f;

    private Vector3 defaultScale;
    private Vector3 velocityLastFrame;

    private int lastMoveDir = 1;


    private GameObject grappleUIIndicator;
    private GameObject currentGrappleIndicator;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        grappleUIIndicator = Resources.Load<GameObject>("GrappleUIIndicator");
    }

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField] private float attackPower = 10f;
    [SerializeField] private float attackRadius = 2f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask enemyLayer;


    void Start()
    {
        defaultScale = visualTransform.localScale;
        defaultScale.x = Mathf.Abs(defaultScale.x); // make sure it's positive
        visualTransform.localScale = defaultScale;
        Debug.Log(rb.gravityScale);
        _trailRenderer = GetComponent<TrailRenderer>();
        Shader.SetGlobalFloat("_shockAmount", 0f);  
        Shader.SetGlobalFloat("_change", 0f);
    }
    // Update is called once per frame
    void Update()
    {
        HandleGrappleIndicator(); 
        if(_isDashing) return;
        horizontal = Input.GetAxisRaw("Horizontal");

        if(IsGrounded())
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if(Input.GetButtonDown("Jump"))
        {
            StartCoroutine(PopScale(new Vector3(1.2f, 0.8f, 1f), 0.1f));
            jumpBufferCounter = jumpBufferTime;
        } 
        else 
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if(jumpBufferCounter > 0 && coyoteTimeCounter > 0f)
        {
            rb.AddForce(Vector2.up * jumpingPower, ForceMode2D.Impulse);
            jumpBufferCounter = 0f;
        }
        if(Input.GetButtonUp("Jump") && rb.velocity.y > 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * 0.5f);
            coyoteTimeCounter = 0f;
        }

        if(!isWallJumping)
        {
            Flip();
        }
        WallSlide();
        WallJump();
        Grapple();
        Attack();
        if(_canDash && Input.GetKeyDown(KeyCode.LeftShift))
        {
            StartCoroutine(Dash());
        } 

        Vector2 velocity = rb.velocity;
        Vector3 targetScale = defaultScale;

        // Apply horizontal stretch when moving fast
        if (Mathf.Abs(velocity.x) > 1f && IsGrounded())
        {
            targetScale.x += stretchAmount;
            targetScale.y -= squashAmount;
        }
        // Apply vertical stretch when falling fast
        else if (velocity.y < -5f)
        {
            targetScale.x -= squashAmount;
            targetScale.y += stretchAmount;
        }
        // Squash on landing
        else if (velocityLastFrame.y < -5f && IsGrounded() && velocity.y == 0)
        {
            targetScale.x += squashAmount;
            targetScale.y -= squashAmount;
        }

        // Always apply flip using isFacingToRight
        targetScale.x *= isFacingToRight ? 1 : -1;
        visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, targetScale, Time.deltaTime * stretchSpeed);

        velocityLastFrame = velocity;

        horizontal = Input.GetAxisRaw("Horizontal");

        if (horizontal != 0)
        {
            lastMoveDir = (int)Mathf.Sign(horizontal);
        }
    }

    void Attack()
    {
        if(Input.GetKeyDown(KeyCode.Mouse0))
        {
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, enemyLayer);
            foreach(Collider2D enemy in hitEnemies)
            {
                // Apply damage to the enemy
                Debug.Log("Hit: " + enemy.name);
                // Example: enemy.GetComponent<Enemy>().TakeDamage(attackPower);
            }
            // Start Attack Animation
            StartCoroutine(AttackCoroutine());
        }
    }

    IEnumerator AttackCoroutine()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = Color.blue; 
        float OriginalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
        rb.AddForce(Vector2.right * lastMoveDir * attackPower, ForceMode2D.Impulse);
        // Play attack animation here
        // Example: animator.SetTrigger("Attack");

        // Wait for the attack duration
        yield return new WaitForSeconds(attackDuration);
        rb.gravityScale = OriginalGravity;
        if(rb.gravityScale == 0f)
        {
            rb.gravityScale = 4f;
        }
        rb.velocity = Vector2.zero; // Reset velocity after attack
        spriteRenderer.color = Color.white; // Reset color after attack
        // Reset attack animation here if needed
        // Example: animator.ResetTrigger("Attack");
    }

    IEnumerator PopScale(Vector3 popTo, float time)
    {
        visualTransform.localScale = popTo;
        yield return new WaitForSeconds(time);
        visualTransform.localScale = defaultScale;
    }

    private void HandleGrappleIndicator()
    {
        Collider2D target = GetGrappleTarget();

        if (target != null)
        {
            // If indicator hasn't been created yet, instantiate it and assign
            if (currentGrappleIndicator == null)
            {
                currentGrappleIndicator = Instantiate(grappleUIIndicator);
            }

            // Set its position each frame while target exists
            Vector3 pos = target.transform.position;
            pos.z = -8f;
            currentGrappleIndicator.transform.position = pos;
        }
        else
        {
            // Target not in range — destroy and clear reference
            if (currentGrappleIndicator != null)
            {
                Destroy(currentGrappleIndicator);
                currentGrappleIndicator = null;
            }
        }
    }



    private void WallJump()
    {
        if(isWallSliding)
        {
            isWallJumping = false;
            wallJumpingDirection = -transform.localScale.x;
            wallJumpingCounter = wallJumpingTime;

            CancelInvoke(nameof(StopWallJumping));
        }
        else 
        {
            wallJumpingCounter -= Time.deltaTime;
        }

        if(Input.GetButtonDown("Jump") && wallJumpingCounter > 0)
        {
            rb.velocity = new Vector2(wallJumpingDirection * wallJumpingPower.x, wallJumpingPower.y);
            isWallJumping = true;
            wallJumpingCounter = 0f;

            if(transform.localScale.x != wallJumpingDirection)
            {
                isFacingToRight = !isFacingToRight;
                Vector3 localScale = transform.localScale;
                localScale.x *= -1;
                transform.localScale = localScale;
            }

            Invoke(nameof(StopWallJumping), wallJumpingDuration);
        }
    }

    private void StopWallJumping()
    {
        isWallJumping = false;
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, 0.1f);
        Gizmos.DrawWireSphere(wallCheck.position, 0.1f);
        Gizmos.DrawWireSphere(grapplePoint.position, grappleDistance);
    }
    private Collider2D GetGrappleTarget()
    {
        return Physics2D.OverlapCircle(grapplePoint.position, grappleDistance, grappleLayer);
    }

    private void Grapple()
    {
        Collider2D grappleTarget = GetGrappleTarget();
        
        if(grappleTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            Vector3 worldPos = grappleTarget.transform.position;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            StartCoroutine(GrappleShock( screenPos));

            Debug.Log("FocalPoint: " + Shader.GetGlobalVector("_FocalPoint"));
            StartCoroutine(GrappleCoroutine(grappleTarget.transform));
        } 
    }


    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
    }

    void FixedUpdate()
    {
        if(!_isDashing && !isWallJumping && !_isGrappling)
        {
            float targetSpeed = horizontal * speed;
            float speedDif = targetSpeed - rb.velocity.x;
            float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : decceleration;
            float movement = speedDif * accelRate;
            rb.AddForce(movement * Vector2.right);
        }

    }

    private void Flip()
    {
        if (horizontal < 0 && isFacingToRight || horizontal > 0 && !isFacingToRight)
        {
            isFacingToRight = !isFacingToRight;

            // Instead of flipping visualTransform's scale directly, use a "direction" multiplier
            Vector3 scale = defaultScale;
            scale.x *= isFacingToRight ? 1 : -1;
            visualTransform.localScale = scale;
        }
    }



    private bool IsWalled()
    {
        return Physics2D.OverlapCircle(wallCheck.position, 0.1f, wallLayer);
    }

    private void WallSlide()
    {
        if(IsWalled() && !IsGrounded() && horizontal != 0)
        {
            isWallSliding = true;
            rb.velocity = new Vector2(rb.velocity.x, Mathf.Clamp(rb.velocity.y, -wallSlidingSpeed, float.MaxValue));
        } else
        {
            isWallSliding = false;
        }
    }

    private IEnumerator Dash()
    {
        _canDash = false;
        _isDashing = true;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.velocity = new Vector2(lastMoveDir * dashPower, 0f);
        Debug.Log(rb.velocity);
        _trailRenderer.emitting = true;
        yield return new WaitForSeconds(dashTime);
        _trailRenderer.emitting = false;
        rb.gravityScale = originalGravity;
        _isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        _canDash = true;
    }

    private IEnumerator GrappleShock(Vector3 screenPos)
    {
        float currentChange = Shader.GetGlobalFloat("_change");
        Shader.SetGlobalFloat("_shockAmount", 1f);
        float targetChange = 0f;
        if(Shader.GetGlobalFloat("_change") != targetChange)
        {
            currentChange = Mathf.Lerp(currentChange, targetChange, Time.deltaTime * 5f);
            Shader.SetGlobalFloat("_change", currentChange);
        }
        Vector2 focalPointUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
        Shader.SetGlobalVector("_FocalPoint", focalPointUV);
        yield return new WaitForSeconds(2f);
        Shader.SetGlobalFloat("_shockAmount", 0f);
    }

    private IEnumerator GrappleCoroutine(Transform target)
    {
        if(_isGrappling) yield break; // Prevent multiple grapples at once
        if(target == null) yield break; // Ensure target is not null
        rb.velocity = Vector2.zero; // Reset velocity
        rb.angularVelocity = 0f; // Reset angular velocity
        _isGrappling = true;
        Vector2 direction = (target.position - transform.position).normalized;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        Debug.DrawRay(transform.position, direction * grappleDistance, Color.cyan, 2f);
        
        rb.AddForce(direction * grapplePower, ForceMode2D.Impulse);
        _trailRenderer.emitting = true;
        
        yield return new WaitForSeconds(grappleTime);

        rb.gravityScale = originalGravity;
        _trailRenderer.emitting = false;
        _isGrappling = false;
    }
}
