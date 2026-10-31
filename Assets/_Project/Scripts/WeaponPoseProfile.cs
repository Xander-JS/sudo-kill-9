using System.Collections.Generic;
using UnityEngine;

/// <summary>One calibrated weapon pose per animation the player can be in.</summary>
public enum WeaponPoseKey
{
    Idle,
    RunForward,
    RunBackward,
    RunLeft,
    RunRight,
    Jump,
    Crouch
}

/// <summary>
/// Saved weapon calibration. Lives in the project (not the scene), so values edited in Play mode
/// are kept when you stop, and the data is shared by every scene that uses the player.
/// </summary>
[CreateAssetMenu(fileName = "WeaponPoseProfile", menuName = "The Last Root/Weapon Pose Profile")]
public class WeaponPoseProfile : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public WeaponPoseKey key;
        [Tooltip("Weapon position relative to the right-hand bone.")]
        public Vector3 weaponPosition;
        [Tooltip("Weapon rotation relative to the right-hand bone.")]
        public Vector3 weaponEuler;
        [Tooltip("Right-hand IK offset in character space: x right, y up, z forward (meters).")]
        public Vector3 handOffset;
        [Tooltip("Protects the entry from accidental edits in the wizard.")]
        public bool locked;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IList<Entry> Entries => entries;

    /// <summary>Returns the entry for the key, creating it with default values if it is missing.</summary>
    public Entry Get(WeaponPoseKey key)
    {
        foreach (Entry e in entries)
            if (e.key == key) return e;

        Entry created = DefaultFor(key);
        entries.Add(created);
        return created;
    }

    public void EnsureAllEntries()
    {
        foreach (WeaponPoseKey key in System.Enum.GetValues(typeof(WeaponPoseKey))) Get(key);
    }

    public static Entry DefaultFor(WeaponPoseKey key)
    {
        Entry e = new Entry { key = key };
        switch (key)
        {
            case WeaponPoseKey.RunForward:
            case WeaponPoseKey.RunBackward:
            case WeaponPoseKey.RunLeft:
            case WeaponPoseKey.RunRight:
                e.weaponPosition = new Vector3(-0.13f, 0.12f, 0.25f);
                e.weaponEuler = new Vector3(0f, -180f, -121f);
                e.handOffset = new Vector3(0f, 0f, 0.1f);
                break;
            default: // Idle, Jump and Crouch start from the calibrated Idle pose.
                e.weaponPosition = new Vector3(-0.13f, 0.1f, -0.02f);
                e.weaponEuler = new Vector3(10f, -180f, -121f);
                e.handOffset = new Vector3(0f, 0f, 0.25f);
                e.locked = key == WeaponPoseKey.Idle;
                break;
        }
        return e;
    }
}
