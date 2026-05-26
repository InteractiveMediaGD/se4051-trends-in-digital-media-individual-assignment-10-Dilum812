using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GunManager : MonoBehaviour
{
    [Header("Weapon Settings")]
    [Tooltip("The gun GameObject attached to the Player's camera. Keep this disabled in the Inspector at start.")]
    public GameObject playerGun;

    [Header("Shooting Settings")]
    [Tooltip("The Muzzle Flash prefab to spawn when shooting (e.g. MuzzleFlashEffect).")]
    public GameObject muzzleFlashPrefab;
    [Tooltip("An empty GameObject placed at the tip of the gun barrel where the flash should spawn.")]
    public Transform muzzleSpawnPoint;
    [Tooltip("How fast the gun fires (seconds between shots).")]
    public float fireRate = 0.15f;
    [Tooltip("Sound effect to play when firing the gun.")]
    public AudioClip gunFireSound;
    [Tooltip("How much damage each shot deals to the enemy.")]
    public float damagePerShot = 25f;
    [Tooltip("How far the bullets can travel.")]
    public float weaponRange = 100f;
    [Tooltip("The spark/burst prefab to spawn when a bullet hits an object (e.g. BulletImpactConcreteEffect).")]
    public GameObject bulletImpactPrefab;

    [Header("UI & Aiming Settings")]
    [Tooltip("The Crosshair UI GameObject in the center of your screen. Will be enabled only after picking up the gun.")]
    public GameObject crosshairUI;

    [Header("Aim Down Sights (ADS) Settings")]
    [Tooltip("Check this if you want the player to be able to right-click to aim down sights.")]
    public bool enableADS = true;
    [Tooltip("The local position of the gun relative to the camera when aiming (centered).")]
    public Vector3 aimPosition = new Vector3(0f, -0.15f, 0.4f);
    [Tooltip("The speed of transitioning into aiming mode.")]
    public float adsSpeed = 12f;
    [Tooltip("Field of View (FOV) when aiming down sights.")]
    public float aimFOV = 40f;

    [Header("Audio Settings")]
    [Tooltip("Optional sound effect to play when the gun is picked up.")]
    public AudioClip pickupSound;

    // Internal state tracking
    private bool hasGun = false;
    private Vector3 hipPosition;
    private float normalFOV;
    private Camera playerCam;
    private float nextTimeToFire = 0f;

    private void Start()
    {
        // Ensure the gun and crosshair are hidden at the start of the game
        if (playerGun != null)
        {
            playerGun.SetActive(false);
            hipPosition = playerGun.transform.localPosition; // Store the starting (hip) position
        }
        else
        {
            Debug.LogWarning("Player Gun GameObject is not assigned in the GunManager!");
        }

        if (crosshairUI != null)
        {
            crosshairUI.SetActive(false);
        }

        // Try to find the Camera
        playerCam = GetComponentInChildren<Camera>();
        if (playerCam != null)
        {
            normalFOV = playerCam.fieldOfView;
        }
    }

    private void Update()
    {
        // Only run gun logic if the player actually has the gun
        if (!hasGun || playerGun == null) return;

        // Handle Aiming (ADS)
        if (enableADS)
        {
            HandleADS();
        }

        // Handle Shooting (Left Click)
        if (GetFireInput() && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + fireRate;
            FireWeapon();
        }
    }

    /// <summary>
    /// Activates the gun in the player's hands. Called by the GunPickup script.
    /// </summary>
    public void EquipGun()
    {
        if (playerGun != null)
        {
            playerGun.SetActive(true);
            hasGun = true;

            // Show crosshair UI
            if (crosshairUI != null)
            {
                crosshairUI.SetActive(true);
            }
            
            // Optional: Play pickup sound
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }
            
            Debug.Log("Gun picked up, equipped, and crosshair activated!");
        }
    }

    /// <summary>
    /// Instantiates the muzzle flash and plays the gunshot audio.
    /// </summary>
    private void FireWeapon()
    {
        // 1. Spawn the Muzzle Flash Effect at the barrel tip
        if (muzzleFlashPrefab != null && muzzleSpawnPoint != null)
        {
            // Spawn in world space (without parenting) so that if your gun's Scale Y is 0 or distorted, it does NOT shrink or squish the fire particle effect!
            GameObject flash = Instantiate(muzzleFlashPrefab, muzzleSpawnPoint.position, muzzleSpawnPoint.rotation);
            
            // Keep the prefab's original scale (e.g. 2.5) perfectly intact in world space
            flash.transform.localScale = muzzleFlashPrefab.transform.localScale;

            // Explicitly force the particle system to play immediately
            ParticleSystem ps = flash.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                ps = flash.GetComponentInChildren<ParticleSystem>();
            }

            if (ps != null)
            {
                ps.Play();
            }

            // Destroy the flash instance after 0.4 seconds so it doesn't clutter the scene
            Destroy(flash, 0.4f);
            
            Debug.Log("Muzzle flash spawned and played successfully!");
        }
        else
        {
            Debug.LogWarning("Muzzle Flash Prefab or Muzzle Spawn Point is not assigned in the GunManager!");
        }

        // 2. Perform Raycast Shooting
        if (playerCam != null)
        {
            // Cast ray from the center of the screen
            Ray ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, weaponRange))
            {
                Debug.Log("Hit object: " + hit.collider.name);

                // Check if the object we hit is an Enemy
                EnemyAI enemy = hit.collider.GetComponentInParent<EnemyAI>();
                if (enemy == null)
                {
                    enemy = hit.collider.GetComponentInChildren<EnemyAI>();
                }

                if (enemy != null)
                {
                    enemy.TakeDamage(damagePerShot);
                    Debug.Log("Dealt " + damagePerShot + " damage to Enemy!");
                }

                // Spawn bullet impact particle effect
                if (bulletImpactPrefab != null)
                {
                    GameObject impact = Instantiate(bulletImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    
                    // Maintain standard size for impact particles
                    impact.transform.localScale = bulletImpactPrefab.transform.localScale;
                    
                    Destroy(impact, 1.5f);
                }
            }
        }
        else
        {
            Debug.LogWarning("Player Camera is missing in GunManager, unable to perform Raycast!");
        }

        // 3. Play gun fire sound effect
        if (gunFireSound != null)
        {
            AudioSource.PlayClipAtPoint(gunFireSound, transform.position);
        }

        Debug.Log("Weapon fired!");
    }

    /// <summary>
    /// Handles transitioning between Hip-fire and Aim Down Sights (ADS)
    /// </summary>
    private void HandleADS()
    {
        bool isAiming = GetAimInput();

        // Smoothly transition gun position
        Vector3 targetPosition = isAiming ? aimPosition : hipPosition;
        playerGun.transform.localPosition = Vector3.Lerp(playerGun.transform.localPosition, targetPosition, Time.deltaTime * adsSpeed);

        // Smoothly transition Camera FOV (zoom)
        if (playerCam != null)
        {
            float targetFOV = isAiming ? aimFOV : normalFOV;
            playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, targetFOV, Time.deltaTime * adsSpeed);
        }

        // Optional: Hide crosshair while aiming down sights for a cleaner look
        if (crosshairUI != null)
        {
            crosshairUI.SetActive(!isAiming);
        }
    }

    /// <summary>
    /// Checks input to determine if the player is pressing the Shoot button (Right Click).
    /// Supports both Legacy Input and New Input System.
    /// </summary>
    private bool GetFireInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.rightButton.isPressed;
        }
        return false;
#else
        return Input.GetMouseButton(1); // 1 is Right Click
#endif
    }

    /// <summary>
    /// Checks input to determine if the player is holding the Aim button (Left Click).
    /// Supports both Legacy Input and New Input System.
    /// </summary>
    private bool GetAimInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
        return false;
#else
        return Input.GetMouseButton(0); // 0 is Left Click
#endif
    }
}


