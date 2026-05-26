using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("AI Movement Settings")]
    [Tooltip("The Target player to chase. If empty, the script will automatically find the Player in the scene.")]
    public Transform playerTarget;
    [Tooltip("How close the player has to get (in meters) to trigger the enemy chasing.")]
    public float detectionRange = 15f;
    [Tooltip("How close the enemy stops from the player (to prevent overlapping).")]
    public float stopDistance = 1.5f;
    [Tooltip("Movement speed of the enemy.")]
    public float moveSpeed = 3.5f;
    [Tooltip("Rotation speed of the enemy when facing the player.")]
    public float rotationSpeed = 6f;

    [Header("Enemy Health Settings")]
    [Tooltip("Starting health of the enemy.")]
    public float maxHealth = 100f;
    [Tooltip("Optional particle effect to spawn when the enemy takes damage.")]
    public GameObject hitEffectPrefab;
    [Tooltip("Optional particle effect to spawn when the enemy dies (like an explosion or dust burst).")]
    public GameObject deathEffectPrefab;

    [Header("Enemy Attack Settings")]
    [Tooltip("How often the enemy attacks the player (in seconds).")]
    public float attackRate = 1.5f;

    // Internal state variables
    private float currentHealth;
    private bool isDead = false;
    private bool isChasing = false;
    private Animator anim;
    private float nextTimeToAttack = 0f;

    private void Start()
    {
        currentHealth = maxHealth;

        // Try to find the Player in the scene automatically if not assigned
        if (playerTarget == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }
            else
            {
                // Fallback: search by controller type
                HaloPlayerController controller = FindFirstObjectByType<HaloPlayerController>();
                if (controller != null)
                {
                    playerTarget = controller.transform;
                }
            }
        }

        // Try to find an Animator component in children or self (ideal for Mixamo characters!)
        anim = GetComponentInChildren<Animator>();
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        // Don't run AI logic if dead, or if there is no player target
        if (isDead || playerTarget == null) return;

        // Calculate distance to the player
        float distance = Vector3.Distance(transform.position, playerTarget.position);

        if (distance <= detectionRange)
        {
            isChasing = true;

            // 1. Rotate to face the player smoothly
            Vector3 targetDir = playerTarget.position - transform.position;
            targetDir.y = 0; // Keep rotation vertical (no looking up/down)
            
            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }

            // 2. Chase the player if outside the stopping distance
            if (distance > stopDistance)
            {
                // Move forward towards the player
                transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);

                // Auto-snap to terrain/ground height to prevent floating or clipping
                SnapToGround();

                // If using a Mixamo animator, set walking/running speed parameter
                if (anim != null)
                {
                    anim.SetFloat("Speed", moveSpeed);
                }
            }
            else
            {
                // Stopped at the player (attack distance)
                if (anim != null)
                {
                    anim.SetFloat("Speed", 0f);
                }

                // Periodically trigger the attack animation
                if (Time.time >= nextTimeToAttack)
                {
                    nextTimeToAttack = Time.time + attackRate;
                    if (anim != null)
                    {
                        anim.SetTrigger("Attack");
                        Debug.Log(gameObject.name + " attacked the player!");
                    }
                }
            }
        }
        else
        {
            // Player is outside range; stand still
            isChasing = false;
            if (anim != null)
            {
                anim.SetFloat("Speed", 0f);
            }
        }
    }

    /// <summary>
    /// Reduces the enemy's health. Called by the player's GunManager raycast.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log(gameObject.name + " took " + amount + " damage! Health remaining: " + currentHealth);

        // Spawn hit effect at the enemy's center
        if (hitEffectPrefab != null)
        {
            GameObject hitObj = Instantiate(hitEffectPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
            Destroy(hitObj, 1f);
        }

        // Trigger an animator Hit parameter if it exists
        if (anim != null)
        {
            anim.SetTrigger("Hit");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Triggers the enemy's death sequence.
    /// </summary>
    private void Die()
    {
        isDead = true;
        Debug.Log(gameObject.name + " has died!");

        // 1. Play death trigger on Animator if it exists
        if (anim != null)
        {
            anim.SetTrigger("Die");
            // If they are using an Animator, let it handle the fall over.
            // Disable the animator's root motion or colliders if needed.
        }
        else
        {
            // 2. Fallback: Collapsing fall-over effect (tipping by 90 degrees) if no animator
            transform.Rotate(Vector3.right * 90f, Space.Self);
        }

        // 3. Spawn death explosion/dust effect
        if (deathEffectPrefab != null)
        {
            GameObject deathObj = Instantiate(deathEffectPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            Destroy(deathObj, 2f);
        }

        // 4. Disable collision so player doesn't bump into the corpse
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 5. Destroy the enemy corpse after a short delay
        Destroy(gameObject, 2f);
    }

    /// <summary>
    /// Snaps the enemy to the ground level so they walk smoothly on hills and uneven terrain.
    /// </summary>
    private void SnapToGround()
    {
        // Temporarily disable our own colliders so the raycast doesn't hit ourselves and make us fly into the sky!
        Collider[] myColliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in myColliders)
        {
            col.enabled = false;
        }

        // Cast a ray from slightly above the enemy downwards
        Vector3 origin = transform.position + Vector3.up * 2f;
        RaycastHit hit;

        // Mask to ignore triggers
        if (Physics.Raycast(origin, Vector3.down, out hit, 15f, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }

        // Re-enable our colliders so the player can still shoot us
        foreach (Collider col in myColliders)
        {
            col.enabled = true;
        }
    }

    // Draw the detection range in the editor for easy debugging!
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
