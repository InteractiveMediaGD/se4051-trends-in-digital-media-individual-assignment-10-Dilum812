using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GunPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("The tag assigned to the Player GameObject.")]
    public string playerTag = "Player";

    [Header("Visual Effects (Optional)")]
    [Tooltip("An optional particle effect to spawn when picked up.")]
    public GameObject pickupEffect;

    private void Start()
    {
        // Make sure the collider is set as a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger is the player
        if (other.CompareTag(playerTag) || other.GetComponent<HaloPlayerController>() != null)
        {
            // Find the GunManager component on the player (or parent if attached to children)
            GunManager gunManager = other.GetComponentInParent<GunManager>();
            if (gunManager == null)
            {
                gunManager = other.GetComponentInChildren<GunManager>();
            }

            if (gunManager != null)
            {
                // Equip the gun
                gunManager.EquipGun();

                // Spawn pickup effect if assigned
                if (pickupEffect != null)
                {
                    Instantiate(pickupEffect, transform.position, Quaternion.identity);
                }

                // Destroy the pickup from the ground
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("Player walked into gun pickup, but Player is missing the GunManager script!");
            }
        }
    }
}
