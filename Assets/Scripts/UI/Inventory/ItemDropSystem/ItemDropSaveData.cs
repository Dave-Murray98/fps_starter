using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Save data for the item drop system
/// </summary>
[System.Serializable]
public class ItemDropSaveData
{
    public List<DroppedItemData> droppedItems;
    public int nextDroppedItemId;
}