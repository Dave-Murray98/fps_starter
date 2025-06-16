using UnityEngine;

[System.Serializable]
public class WeaponData
{
    [Header("Weapon Stats")]
    public float damage = 10f;
    public float range = 5f;
    public float fireRate = 1f; // Attacks per second
    [Tooltip("Ammo type this weapon uses (for guns)")]
    public ItemData requiredAmmoType;
    [Tooltip("Maximum ammo capacity")]
    public int maxAmmo = 30;
    [Tooltip("Current ammo loaded (saved per weapon instance)")]
    public int currentAmmo = 0;
}