using UnityEngine;

/// <summary>
/// Defines a visual mask group.
///
/// Objects without a MaskGroup belong to the shared Default Group.
///
/// independentOutline:
///     Allows this group to generate an independent outline
///     over lower-sorting groups.
///
/// independentShadow:
///     Allows this group to cast its offset shadow
///     over lower-sorting groups.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MaskGroup : MonoBehaviour
{
    // ============================================================
    // Flags
    // ============================================================

    public const byte IndependentOutlineFlag =
        1 << 0;

    public const byte IndependentShadowFlag =
        1 << 1;


    // ============================================================
    // Global Change Version
    //
    // PostProcessFeature uses this value to know when renderer
    // ownership may need to be rebuilt.
    // ============================================================

    private static int globalVersion = 0;

    public static int GlobalVersion =>
        globalVersion;


    // ============================================================
    // Runtime Group ID
    // ============================================================

    private static uint nextRuntimeGroupID =
        2u;


    [System.NonSerialized]
    private uint runtimeGroupID;


    // ============================================================
    // Settings
    // ============================================================

    [Header("Outline")]

    [Tooltip(
        "Generate an independent outer outline for this group.\n\n" +
        "The outline may cover another object only when this group " +
        "has a higher Sorting Layer / Order in Layer."
    )]
    public bool independentOutline =
        true;


    [Header("Shadow")]

    [Tooltip(
        "Allow this group's shadow to fall onto another MaskGroup.\n\n" +
        "The shadow may cover another object only when this group " +
        "has a higher Sorting Layer / Order in Layer."
    )]
    public bool independentShadow =
        true;


    // ============================================================
    // Public Properties
    // ============================================================

    public uint GroupID
    {
        get
        {
            EnsureRuntimeGroupID();

            return runtimeGroupID;
        }
    }


    public byte GroupFlags
    {
        get
        {
            byte flags =
                0;


            if (independentOutline)
            {
                flags |=
                    IndependentOutlineFlag;
            }


            if (independentShadow)
            {
                flags |=
                    IndependentShadowFlag;
            }


            return flags;
        }
    }


    // ============================================================
    // Lifecycle
    // ============================================================

    private void OnEnable()
    {
        EnsureRuntimeGroupID();

        MarkHierarchyDirty();
    }


    private void OnDisable()
    {
        MarkHierarchyDirty();
    }


    private void OnTransformChildrenChanged()
    {
        MarkHierarchyDirty();
    }


    private void OnTransformParentChanged()
    {
        MarkHierarchyDirty();
    }


#if UNITY_EDITOR

    private void OnValidate()
    {
        EnsureRuntimeGroupID();

        MarkHierarchyDirty();
    }

#endif


    // ============================================================
    // Runtime ID
    // ============================================================

    private void EnsureRuntimeGroupID()
    {
        if (runtimeGroupID >=
            2u)
        {
            return;
        }


        runtimeGroupID =
            nextRuntimeGroupID++;


        if (nextRuntimeGroupID >
            PostProcessFeature.MaxMaskGroupID)
        {
            nextRuntimeGroupID =
                2u;
        }
    }


    // ============================================================
    // Dirty
    // ============================================================

    private static void MarkHierarchyDirty()
    {
        globalVersion++;
    }


    // ============================================================
    // Group Resolution
    // ============================================================

    /// <summary>
    /// Finds the nearest enabled MaskGroup in the parent hierarchy.
    /// </summary>
    public static MaskGroup ResolveNearestActive(
        Transform target)
    {
        Transform current =
            target;


        while (current != null)
        {
            MaskGroup group =
                current.GetComponent<MaskGroup>();


            if (group != null &&
                group.isActiveAndEnabled)
            {
                return group;
            }


            current =
                current.parent;
        }


        return null;
    }
}