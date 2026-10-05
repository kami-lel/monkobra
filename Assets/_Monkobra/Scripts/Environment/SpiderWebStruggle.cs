using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sits on the monkey root beside <see cref="MonkeyPrefabRoot"/> and owns the
/// spider web trap. A <see cref="SpiderWebTrap"/> hands itself over on
/// contact, the body is held in place, and each escape press adds progress
/// that drains back while idle. A full bar frees the monkey, removes the web
/// and grants a short protection from any web.
/// <para>
/// The escape key, Space, is also bound to interact, the grab key, so the
/// interact action from <see cref="MonkeyConfig"/> is disabled on trap.
/// Disabling cancels it if held, so the arms end any reach and the camera
/// swings back behind. It stays off until protection ends, so leftover
/// presses after breaking free do not fire a grab.
/// </para>
/// <para>
/// The trap count lives on this instance, never a static, so the first-trap
/// hint returns on every scene load.
/// </para>
/// </summary>
[RequireComponent(typeof(MonkeyPrefabRoot))]
public class SpiderWebStruggle: MonoBehaviour {
    // Public Members  #########################################################
    /// <summary>
    /// Raised on being trapped, with whether this is the first trap since the
    /// scene loaded.
    /// </summary>
    public event Action<bool> Trapped;

    /// <summary>Raised whenever escape progress changes, with it, 0~1.</summary>
    public event Action<float> ProgressChanged;

    /// <summary>Raised once the monkey breaks free.</summary>
    public event Action Escaped;

    /// <returns>if a web holds the monkey right now</returns>
    public bool IsTrapped => isTrapped;

    /// <returns>escape progress, 0~1, 1 breaks free</returns>
    public float EscapeProgress => escapeProgress;

    /// <returns>if a recent escape still shields the monkey from webs
    /// </returns>
    public bool IsProtected => Time.time < protectionEndTime;

    // Public Methods  #########################################################
    /// <summary>
    /// Traps the monkey in <paramref name="web"/>, unless it is already
    /// trapped or still protected after its last escape.
    /// </summary>
    /// <returns>if the monkey got trapped</returns>
    public bool TryTrap(SpiderWebTrap web) {
        if (
            web == null
            || !isActiveAndEnabled
            || isTrapped
            || IsProtected
            || monkeyRoot == null
        ) {
            return false;
        }

        isTrapped = true;
        trapWeb = web;
        escapeProgress = 0f;
        trapCount++;

        monkeyRoot.IsHeld = true;
        SuspendInteract();

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"SpiderWebStruggle:\ttrapped in {web.name}, "
                    + $"trap #{trapCount} this scene",
                this
            );
        }
        Trapped?.Invoke(trapCount == 1);
        ProgressChanged?.Invoke(escapeProgress);
        return true;
    }

    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip(
        "same asset the body and arms use, its interact action is paused "
            + "while trapped"
    )]
    private MonkeyConfig monkeyConfig;

    [SerializeField]
    [Tooltip("button mashed to break free, Player/Jump (Space)")]
    private InputActionReference escapeAction;

    [Header("Escape")]
    [SerializeField]
    [Range(0.01f, 1f)]
    [Tooltip("progress added per escape press; 0~1, 1 breaks free")]
    private float pressGain = 0.12f;

    [SerializeField]
    [Tooltip("progress lost per second while not pressing; 1/s")]
    private float decayPerS = 0.35f;

    [SerializeField]
    [Tooltip("immunity to every web after breaking free; s")]
    private float protectionDurationS = 1f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (monkeyConfig == null) {
            Debug.LogWarning(
                "SpiderWebStruggle:\tmust assign Inspector Field: "
                    + "monkeyConfig",
                this
            );
        } else if (monkeyConfig.InteractAction == null) {
            Debug.LogWarning(
                "SpiderWebStruggle:\tmust assign MonkeyConfig field: "
                    + "interactAction",
                monkeyConfig
            );
        }
        if (escapeAction == null) {
            Debug.LogWarning(
                "SpiderWebStruggle:\tmust assign Inspector Field: "
                    + "escapeAction",
                this
            );
        }

        monkeyRoot = GetComponent<MonkeyPrefabRoot>();
        if (monkeyRoot == null) {
            Debug.LogError(
                "SpiderWebStruggle:\tfail to get Component: MonkeyPrefabRoot",
                this
            );
        }
    }

    private void OnEnable() {
        if (escapeAction != null) {
            escapeAction.action.Enable();
            escapeAction.action.performed += OnEscapePerformed;
        }
    }

    private void OnDisable() {
        if (escapeAction != null) {
            escapeAction.action.performed -= OnEscapePerformed;
            escapeAction.action.Disable();
        }
        // never leave the body frozen or grabbing off behind a disabled script
        if (isTrapped) {
            BreakFree();
        }
        ResumeInteract();
    }

    private void Update() {
        // game over freezes time, the struggle freezes with it
        if (Time.timeScale <= 0f) {
            return;
        }

        if (isTrapped) {
            // the web may vanish under the monkey, free it rather than hang
            if (trapWeb == null) {
                BreakFree();
                return;
            }
            DecayProgress();
        } else if (isInteractSuspended && !IsProtected) {
            ResumeInteract();
        }
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        decayPerS = Mathf.Max(0f, decayPerS);
        protectionDurationS = Mathf.Max(0f, protectionDurationS);
    }

    // Event Handlers  #########################################################
    private void OnEscapePerformed(InputAction.CallbackContext context) {
        if (!isTrapped || Time.timeScale <= 0f) {
            return;
        }

        escapeProgress = Mathf.Min(1f, escapeProgress + pressGain);
        if (trapWeb != null) {
            trapWeb.Pulse();
        }
        ProgressChanged?.Invoke(escapeProgress);

        if (escapeProgress >= 1f) {
            BreakFree();
        }
    }

    // private members  ########################################################
    private bool isTrapped;
    private float escapeProgress; // 0~1
    private float protectionEndTime; // Time.time when webs catch again
    private int trapCount; // traps since scene load, 1 is the first
    private bool isInteractSuspended; // this script disabled interact

    private InputActionReference InteractAction =>
        monkeyConfig != null ? monkeyConfig.InteractAction : null;

    // cached references  ------------------------------------------------------
    private MonkeyPrefabRoot monkeyRoot;
    private SpiderWebTrap trapWeb; // web holding the monkey, null when free

    // private methods  ########################################################
    private void DecayProgress() {
        if (escapeProgress <= 0f) {
            return;
        }

        escapeProgress = Mathf.Max(
            0f,
            escapeProgress - decayPerS * Time.deltaTime
        );
        ProgressChanged?.Invoke(escapeProgress);
    }

    private void BreakFree() {
        SpiderWebTrap web = trapWeb;
        isTrapped = false;
        trapWeb = null;
        escapeProgress = 0f;
        // interact stays off until this ends, Update turns it back on
        protectionEndTime = Time.time + protectionDurationS;

        if (monkeyRoot != null) {
            monkeyRoot.IsHeld = false;
        }
        if (web != null) {
            web.Release();
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                "SpiderWebStruggle:\tbroke free, protected for "
                    + $"{protectionDurationS}s",
                this
            );
        }
        Escaped?.Invoke();
    }

    // disabling a held action cancels it: ArmRoot ends its reach, grabbing
    // only if the hand already overlaps the fruit as on any release, and
    // CameraRigManager swings back to the behind view
    private void SuspendInteract() {
        if (isInteractSuspended || InteractAction == null) {
            return;
        }

        isInteractSuspended = true;
        InteractAction.action.Disable();
    }

    private void ResumeInteract() {
        if (!isInteractSuspended) {
            return;
        }

        isInteractSuspended = false;
        if (InteractAction != null) {
            InteractAction.action.Enable();
        }
    }
}
