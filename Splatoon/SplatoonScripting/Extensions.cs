using ECommons.GameFunctions;
using System.Diagnostics.CodeAnalysis;
#nullable enable
namespace Splatoon.SplatoonScripting;

public static unsafe class Extensions
{
    /// <summary>
    /// Gets object by it's object ID.
    /// </summary>
    /// <param name="objectID">Object ID to search.</param>
    /// <returns>GameObject if found; null otherwise.</returns>
    public static IGameObject? GetObject(this uint objectID)
    {
        return Svc.Objects.FirstOrDefault(x => x.EntityId == objectID);
    }

    /// <summary>
    /// Attempts to get object by it's object ID.
    /// </summary>
    /// <param name="objectID">Object ID to search.</param>
    /// <param name="obj">Resulting GameObject if found; null otherwise.</param>
    /// <returns>Whether object was found.</returns>
    public static bool TryGetObject(this uint objectID, [NotNullWhen(true)] out IGameObject? obj)
    {
        obj = objectID.GetObject();
        return obj != null;
    }

    /// <summary>
    /// Sets reference position for Element from Vector3, accounting for Y and Z swap.
    /// </summary>
    /// <param name="e">Element to set position of</param>
    /// <param name="Position">Position</param>
    public static void SetRefPosition(this Element e, Vector3 Position)
    {
        e.refX = Position.X;
        e.refY = Position.Z;
        e.refZ = Position.Y;
    }

    /// <summary>
    /// Sets offset position for Element from Vector3, accounting for Y and Z swap.
    /// </summary>
    /// <param name="e">Element to set position of</param>
    /// <param name="Position">Position</param>
    public static void SetOffPosition(this Element e, Vector3 Position)
    {
        e.offX = Position.X;
        e.offY = Position.Z;
        e.offZ = Position.Y;
    }

    /// <summary>
    /// Get a unique ID for this element.
    /// </summary>
    /// <param name="e">Element to get unqiue ID for</param>
    /// <param name="maybeGameObject">A GameObject that is only specified if the element is attached to one</param>
    /// <returns></returns>
    public static string GetUniqueId(this Element e, IGameObject? maybeGameObject = null)
    {
        if(maybeGameObject == null) return e.GUID + maybeGameObject?.GameObjectId;
        // Same string as e.GUID + GameObjectId, kept per element for the last few objects it matched: an attached element asks every frame.
        var guid = e.GUID;
        var objectId = maybeGameObject.GameObjectId;
        var slots = e.UniqueIdCache ??= new UniqueIdEntry[UniqueIdSlots];
        for(var i = 0; i < slots.Length; i++)
        {
            var entry = slots[i];
            if(entry != null && entry.ObjectId == objectId && ReferenceEquals(entry.Guid, guid)) return entry.Value;
        }
        var value = guid + objectId;
        var next = (uint)e.UniqueIdCacheNext % (uint)slots.Length;
        slots[next] = new UniqueIdEntry(guid, objectId, value);
        e.UniqueIdCacheNext = (int)next + 1;
        return value;
    }

    private const int UniqueIdSlots = 4;
}

internal sealed record UniqueIdEntry(string? Guid, ulong ObjectId, string Value);
