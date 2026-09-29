using System;
using UnityEngine;

/// <summary>
/// Identifies which side a detection zone belongs to, e.g. for two zones
/// flanking a character.
/// </summary>
public enum DetectionSide {
    Left,
    Right,
}

/// <summary>
/// Trigger volume that reports which <see cref="DetectionSide"/> it
/// belongs to whenever another collider enters or exits it. Attach to a
/// child GameObject with a trigger Collider, set its Side in the
/// Inspector, and subscribe to Entered/Exited to know which zone fired.
/// </summary>
public class DetectionZone: MonoBehaviour {
    // Public Members  #########################################################
    public DetectionSide Side => side;
    public event Action<DetectionSide, Collider> Entered;
    public event Action<DetectionSide, Collider> Exited;

    // Inspector Fields  #######################################################
    [SerializeField]
    private DetectionSide side;

    // Event Handlers  #########################################################
    private void OnTriggerEnter(Collider other) {
        Entered?.Invoke(side, other);
    }

    private void OnTriggerExit(Collider other) {
        Exited?.Invoke(side, other);
    }
}
