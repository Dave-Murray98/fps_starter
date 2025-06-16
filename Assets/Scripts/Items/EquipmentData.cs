using UnityEngine;

[System.Serializable]
public class EquipmentData
{
    [Header("Equipment Properties")]
    [Tooltip("What types of objects this equipment can interact with")]
    public string[] compatibleInteractionTags;
    [Tooltip("Range at which this equipment can be used")]
    public float useRange = 2f;
    [Tooltip("Time it takes to use this equipment")]
    public float useTime = 1f;
    [Tooltip("Does this equipment have limited uses?")]
    public bool hasLimitedUses = false;
    public int maxUses = 10;
}