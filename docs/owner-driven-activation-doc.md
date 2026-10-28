# Owner-Driven Activation

A Unity design pattern report: keep a GameObject inactive until an outside owner activates it, instead of leaving it active and letting it decide when to work.

## Summary

- Pattern: an object is inert by default; a separate owner (controller, manager, spawner) calls `SetActive(true)` when the object is needed and `SetActive(false)` when it is done
- Opposite: a self-governing object stays active, runs its own `Update`, and checks a flag to decide whether to act
- Verdict: a good default for objects with a discrete on/off life; the wrong tool for anything that must listen, initialize, or own the toggling
- Rule of thumb: toggle the GameObject for whole-object presence; toggle a component or a child visual when the object must keep its identity or keep listening; never deactivate the owner of the toggling

## Names

No single official term exists. These all describe it:

- Owner-driven activation: the owner decides when the object lives
- Passive object under an orchestrator: the object is inert, a controller or mediator drives it
- State-driven enabling: a game state change maps to a `SetActive` call
- Enable-on-demand: the object is created or placed in advance, then switched on when needed

The opposite style is called self-governing, or polling, for an always-active object checking its own condition.

## How It Works in Unity

Deactivating a GameObject (`activeInHierarchy` becomes false) has these effects:

- Message methods stop: `Update`, `FixedUpdate`, `LateUpdate`, coroutines, and physics callbacks do not run
- Rendering, colliders, and audio sources on it stop contributing
- `Awake` does not run until the first activation; `OnEnable` and `OnDisable` run on every toggle
- Animators stop; state resets or resumes depending on the Animator's settings
- Finders skip it: `FindObjectsByType` and `GetComponentInChildren` ignore inactive objects unless asked to include them
- A child is inactive when any parent is inactive, whatever its own `activeSelf` says

Activation order matters: the owner must hold a reference to the inactive object, because it cannot find it by search while it is off.

## Comparison

| Aspect | Owner-driven | Self-governing |
| --- | --- | --- |
| Who decides | the owner, in one place | each object, scattered |
| Idle cost | none | `Update` runs every frame |
| Guard clauses | not needed | `if (!active) return;` everywhere |
| Setup and teardown | `OnEnable` and `OnDisable` | custom flags |
| Finding the object | needs a stored reference | can search |
| Listening while off | no | yes |
| Fade or animate out | needs a hide state first | works naturally |
| Coupling | owner knows its objects | object knows the rules |

## Advantages

- Single decision point: the state lives in the owner, so a bug is traced by reading one script
- No idle cost: inactive objects consume no `Update`, physics, or draw calls
- Fewer guards: scripts drop flag checks and half-initialized states cannot occur
- Natural lifecycle: `OnEnable` subscribes and resets, `OnDisable` unsubscribes, so event leaks are rare
- Pool friendly: the same toggle serves object pools and spawners
- Merge friendly: in a team project, an inactive prefab does nothing until a system turns it on, so authors can edit it in isolation
- Easy testing: put the object in a scene, activate it by hand, and see the behavior in isolation

## Disadvantages

- Awake gap: `Awake` and singleton registration do not run while inactive, so a lookup before first activation returns nothing
- Missed events: an inactive subscriber never sees an event fired while it is off
- Reference duty: the owner must be wired to every object it toggles, and a missing reference fails silently
- Hidden state: an object left inactive in a saved scene is easy to forget while editing, and easy to leave inactive by accident
- Cut-off transitions: deactivation stops coroutines, tweens, and animations mid-way
- Stale state: each re-enable runs `OnEnable` on state left from the last use, so reset must be explicit
- Prefab limits: an owner in one prefab asset cannot hold a reference to an object in another prefab asset; the link must be made in the scene or at runtime

## Where to Use It

Good fits, objects with a discrete on/off life:

- Screens and panels: pause, game over, settings, tutorial overlays
- Popups, toasts, and tooltips
- Transient hazards and enemies, especially pooled or spawned
- Pickups, projectiles, and effects (particles, decals, hit flashes)
- Cutscene props and scripted-event actors
- Mode-specific rigs: alternate cameras, debug panels, editor-only stand-ins
- Level sections streamed in and out by distance or progress

## Where Not to Use It

Poor fits, keep these active:

- Managers and singletons: they need `Awake` at load so a static `Instance` exists
- Event hubs and listeners: they must be subscribed before any event fires
- The owner that does the toggling: if it is inactive, nothing can wake the others
- Always-on systems: camera follow, resource drain, input reading, audio mixers, anything that reads state every frame
- Objects that must fade or animate out: use a hide state, and deactivate when the transition ends
- Objects other systems find by search at runtime: search skips inactive objects

## Alternatives to Whole-Object Toggling

Pick the narrowest switch that does the job:

- Component enable: set `enabled` on one script or collider; the object stays findable and its `Awake` already ran
- Child visual toggle: keep the parent active and toggle only a child (a label, a mesh); the parent keeps listening
- `CanvasGroup`: set alpha, interactable, and blocksRaycasts for UI that must fade and stay findable
- Renderer toggle: set `enabled` on the renderer for something that must run logic but not draw
- Layer or culling: for visibility only, with no effect on logic
- Pool: return to a pool object instead of destroying; activation is then the only cost

## Daily Practice

Scripting:

- Make the owner a serialized reference holder: assign inactive targets in the Inspector, never through a search
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`; do not subscribe in `Awake` or `Start` for toggled objects
- Reset state in `OnEnable` so each activation starts clean
- Expose one entry point per object (`Show`, `Hide`, `Open`, `Close`) so the owner never reaches into its fields
- Guard against double activation and activation during the owner's own `Awake`
- Never rely on `Start` order across objects that activate at different times

Design:

- Decide per object at design time: does it have a lifecycle, or must it always listen? Record the answer in the prefab name or a short note on the owner
- Save each object in its intended resting state: inactive for owner-driven, active for always-on
- Keep the owner and the always-on infrastructure in a separate root from the toggled objects
- Give a group root (a panel container) a single owner so it can toggle its children as a unit

Review checklist:

- [ ] Does anything search for this object by type or tag at runtime? It will not be found while inactive
- [ ] Does it hold a singleton registration in `Awake`? Keep it active at load
- [ ] Does it subscribe to events? Subscribe in `OnEnable`, and check what it misses while off
- [ ] Does it fade, tween, or play an exit animation? Add a hide state
- [ ] Is every inactive object reachable from a serialized reference on its owner?
- [ ] Is the saved scene state (active or inactive) intentional for each object?

## Prefabs and Team Workflow

- Save the prefab root in its resting state; an inactive root means the scene instance starts off until its owner enables it
- Cross-prefab references: an owner in one prefab cannot keep a serialized reference to an object inside another prefab; wire it as a scene override, a runtime registration, or an event
- Nested prefabs: the owner and the objects it toggles belong in the same prefab asset when possible, so the references survive
- One owner per toggled group: two owners toggling the same object cause flicker that no one can trace

## Example

```csharp ScreenOwner.cs
using UnityEngine;

public class ScreenOwner : MonoBehaviour {
    [SerializeField] private GameObject pauseScreen;  // inactive at rest

    public void ShowPause() {
        pauseScreen.SetActive(true);
    }

    public void HidePause() {
        pauseScreen.SetActive(false);
    }
}
```

```csharp PauseScreen.cs
using UnityEngine;

public class PauseScreen : MonoBehaviour {
    private void OnEnable() {
        // reset state and subscribe on each activation
        Time.timeScale = 0f;
    }

    private void OnDisable() {
        Time.timeScale = 1f;
    }
}
```

The owner stays active and holds the reference. The screen has no `Update` and no flag; its lifecycle is `OnEnable` to `OnDisable`.
