using UnityEngine;

/// <summary>
/// Turns the GameManager's milestones (Ground, Atmosphere, Orbit...) into a simple scale called
/// "journey units": 0 = the first milestone, 1 = the second, 2 = the third, and 1.5 is halfway
/// between the second and third. Units past the last milestone keep going in the same way.
///
/// Anything that depends on height (the backdrop, the hazard spawner) uses this, so changing
/// the heights of the milestones in the GameManager moves everything together.
/// The milestones must be listed from lowest to highest.
/// </summary>
public static class JourneyScale
{
    public static bool IsValid(Milestone[] m)
    {
        return m != null && m.Length >= 2;
    }

    /// <summary>Finds a milestone's height by name (not case sensitive), or by position if the name is not found.</summary>
    public static bool Find(Milestone[] m, string milestoneName, int fallbackIndex, out float height)
    {
        height = 0f;
        if (m == null || m.Length == 0) return false;

        if (!string.IsNullOrEmpty(milestoneName))
        {
            for (int i = 0; i < m.Length; i++)
            {
                if (m[i] != null && string.Equals(m[i].name, milestoneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    height = m[i].height;
                    return true;
                }
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < m.Length && m[fallbackIndex] != null)
        {
            height = m[fallbackIndex].height;
            return true;
        }
        return false;
    }

    /// <summary>Height above the pad for a position on the journey scale.</summary>
    public static float ToHeight(Milestone[] m, float units)
    {
        int last = m.Length - 1;
        int i = Mathf.Clamp(Mathf.FloorToInt(units), 0, last - 1);
        float t = units - i;
        return Mathf.LerpUnclamped(m[i].height, m[i + 1].height, t);
    }

    /// <summary>Position on the journey scale for a height above the pad.</summary>
    public static float ToUnits(Milestone[] m, float height)
    {
        int last = m.Length - 1;
        int i = 0;
        while (i < last - 1 && height >= m[i + 1].height) i++;

        float span = m[i + 1].height - m[i].height;
        if (span < 0.0001f) span = 1f;
        return i + (height - m[i].height) / span;
    }
}