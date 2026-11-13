using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the boss fight against the Cobra King. Once the monkey has climbed
/// <see cref="WorldBalanceConfig.BossTriggerHeightU"/> the screen flashes and
/// the run enters the boss arena: the king coils round the tree top above,
/// the great cobra stops chasing and guards below, and the spawners of webs,
/// falling cobras and wet bark stop. Banana branches grow on the trunk in
/// between; a grabbed banana still feeds the monkey and is also thrown in an
/// arc at the king's crown by itself, so the fight uses only the grab the
/// player already knows.
/// <para>
/// The fight loops: the king rests, then attacks; a banana hurts it at any
/// time. Before the first hit it only rests, and the first banana carries a
/// key prompt and a dotted line to the jewel. Its attacks grow with each lost third of its health: a bite
/// at a marked spot, a tail sweep through a marked ring (both fatal, both
/// warned), and minions dropping on marked spots (a knock-down). A golden
/// banana refills stamina and gives a gold shield that turns one fatal hit
/// into a knock-down.
/// </para>
/// <para>
/// The monkey can never climb close to the king: a height ceiling holds it
/// below the chin however long up is held, and a monkey at the ceiling, or
/// touching the king, is butted down often ("Stay away from me!"), never
/// fatally. The great cobra guards below without moving, peeking up now &
/// then; a monkey below the arena hears "Nowhere to run!".
/// The camera tilts up so the king stays in view. At zero health the king
/// shatters into cubes, its crown flies onto the monkey's head and the run
/// is won (<see cref="GameController.WinGame"/>).
/// </para>
/// </summary>
[RequireComponent(typeof(BossKingBody), typeof(BossFightHud))]
public class BossFightController: MonoBehaviour {
    // Public Members  #########################################################
    public static BossFightController I {
        get; private set;
    }

    /// <returns>if the monkey is in the boss arena, fighting or done; never
    /// in the endless run, which skips the fight</returns>
    public bool IsInArena => phase != Phase.Waiting && !isEndlessStart;

    /// <summary>
    /// Builds a small look-alike of the Cobra King for the trunk height
    /// marker at the fight's height, its face toward the parent's -z.
    /// </summary>
    /// <returns>the figure's root, unparented, or null</returns>
    public Transform BuildMarkerFigure() {
        return body != null ? body.BuildFigure(markerFigureScale) : null;
    }

    /// <returns>climbed height the fight starts at, eg for its trunk height
    /// marker; u, -1 when unknown</returns>
    public float TriggerHeightU => worldBalanceConfig != null
        ? worldBalanceConfig.BossTriggerHeightU
        : -1f;

    /// <summary>raised when the monkey enters the arena & the fight begins
    /// </summary>
    public event System.Action FightStarted;

    /// <summary>raised when the king is beaten, its reward taken and the
    /// climb goes on</summary>
    public event System.Action FightEnded;

    /// <summary>raised when the gold armor takes a great cobra catch</summary>
    public event System.Action ArmorAbsorbed;

    /// <summary>raised when a revive restarts the fight</summary>
    public event System.Action Revived;

    /// <returns>highest world y a pineapple leap may carry the monkey to:
    /// the fight's start height before the fight, so a leap stops at the
    /// arena instead of flying past it, the ceiling under the king during
    /// it, & no limit once it is over</returns>
    public float LeapTopY {
        get {
            if (!isActiveAndEnabled) {
                return float.PositiveInfinity;
            }
            if (phase == Phase.Waiting) {
                RampedDifficultyManager ramp = RampedDifficultyManager.I;
                // a hair over, so landing there surely starts the fight
                return ramp != null && worldBalanceConfig != null
                    ? ramp.PlayerGameStartYPosition
                        + worldBalanceConfig.BossTriggerHeightU + 0.05f
                    : float.PositiveInfinity;
            }
            bool isOver = phase == Phase.Defeated
                || phase == Phase.Rewarding
                || phase == Phase.Done
                || phase == Phase.Over;
            return isOver
                ? float.PositiveInfinity
                : body.LowestY - ceilingGapU;
        }
    }

    /// <summary>
    /// Called by <see cref="CobraSensor"/> when the great cobra catches the
    /// monkey: the gold armor, worn in the fight or kept as the win reward,
    /// takes the bite once, throwing the cobra back down the trunk.
    /// </summary>
    /// <returns>true if the catch was handled here, so the sensor must not
    /// end the run: absorbed by the armor, or in the fight turned into a
    /// boss fight loss that offers a revive</returns>
    /// <returns>if the Cobra King is beaten (or skipped) & the climb is on
    /// past it, eg so the Spider King may start</returns>
    public bool IsDone => phase == Phase.Done;

    /// <returns>if the monkey still wears the gold armor won from the Cobra
    /// King, not yet spent on the climb after</returns>
    public bool HasArmor => hasShield && phase == Phase.Done;

    /// <summary>
    /// Spends the kept gold armor on a fatal hit somewhere else, eg in the
    /// Spider King fight: the badge goes & "Saved!" pops at the monkey.
    /// </summary>
    /// <returns>if there was armor to spend</returns>
    public bool TrySpendArmor() {
        if (!HasArmor || monkey == null) {
            return false;
        }
        hasShield = false;
        ArmorAbsorbed?.Invoke();
        hud.ShowArmorBadge(false);
        hud.PopText("Saved!", monkey.position, goldenColor);
        hud.Flash(goldenColor);
        return true;
    }

    /// <summary>
    /// Test hook for the spider jump hack key: a fight not begun yet is
    /// skipped as if beaten, in the armor, like the endless start.
    /// </summary>
    public void SkipForTest() {
        if (phase == Phase.Waiting) {
            StartAsDone();
        }
    }

    public bool TryAbsorbCobraCatch() {
        if (monkey == null) {
            return false;
        }
        // contacts keep coming for a moment after a save
        if (Time.time < cobraSaveGraceEndTime) {
            return true;
        }
        // the finisher, revive offer & ad already handle the monkey's end
        if (
            phase == Phase.Finishing
            || phase == Phase.ReviveOffer
            || phase == Phase.WatchingAd
        ) {
            return true;
        }
        bool canSave = phase == Phase.Fighting
            || phase == Phase.Defeated
            || phase == Phase.Rewarding
            || phase == Phase.Done;
        if (!canSave) {
            return false;
        }
        if (!hasShield) {
            // caught by the guard in the fight: a boss fight loss, w/ its
            // slow motion & the revive offer, not a plain end of the run
            if (phase == Phase.Fighting) {
                StartFinisher(false);
                return true;
            }
            return false;
        }
        hasShield = false;
        ArmorAbsorbed?.Invoke();
        hud.ShowArmorBadge(false);
        cobraSaveGraceEndTime = Time.time + 1f;
        hud.PopText("Saved!", monkey.position, goldenColor);
        hud.Flash(goldenColor);
        // in the fight that is the guard below: its peek ends & the monkey
        // flies back up to where the fight began, never left under the guard,
        // where it could not climb back alive; no freeze, as in the fight
        // the armor only saves, the same "Saved!" as any fatal hit
        bool isGuardStill = phase == Phase.Fighting
            || phase == Phase.Defeated
            || phase == Phase.Rewarding;
        if (isGuardStill) {
            if (greatCobra != null) {
                greatCobra.KnockBack(0f);
            }
            if (phase == Phase.Fighting) {
                ReturnToStart();
            }
            return true;
        }
        // the kept reward armor on the climb after: the cobra is thrown far
        // down & frozen, more than a coconut does
        if (greatCobra != null) {
            greatCobra.KnockBack(armorKnockBackU);
            greatCobra.TryFreeze(armorCobraFreezeS);
        }
        return true;
    }

    /// <summary>
    /// Called by <see cref="ArmController"/> on every grab: in the arena a
    /// grabbed banana still feeds the monkey, & is thrown at the king too;
    /// the golden banana gives its shield instead.
    /// </summary>
    /// <returns>true if the grab was handled here, so the arm skips its own
    /// fly-to-the-bar effect</returns>
    public bool TryThrow(Collider fruit, Vector3 from) {
        if (phase != Phase.Fighting || fruit == null
            || !fruit.CompareTag(FRUIT_TAG)) {
            return false;
        }

        // the first throw picks the mode: plain banana easy, golden normal
        int choice = choices.FindIndex(f => f.trigger == fruit);
        if (choice >= 0) {
            bool isEasyPick = !choices[choice].isGolden;
            StaminaBarController staminaBar = StaminaBarController.I;
            if (staminaBar != null) {
                staminaBar.AddStaminaByFruit();
            }
            LaunchBanana(fruit.gameObject, from);
            ChooseMode(isEasyPick);
            return true;
        }

        int index = fruits.FindIndex(f => f.trigger == fruit);
        if (index >= 0) {
            ArenaFruit picked = fruits[index];
            if (picked.isGolden) {
                HideGolden();
                nextGoldenTime = Time.time + goldenIntervalS;
                TakeGoldenBanana();
                return true;
            }
            picked.respawnTime = Time.time
                + bananaRespawnS * BananaRespawnScale;
            fruits[index] = picked;
        }

        // a banana on the tree's own branches flies too
        StaminaBarController stamina = StaminaBarController.I;
        if (stamina != null) {
            stamina.AddStaminaByFruit();
        }
        LaunchBanana(fruit.gameObject, from);
        return true;
    }

    // a copy of the grabbed banana arcs up at the king, spinning, with a
    // trail of fading dots, so the throw reads at a glance
    private void LaunchBanana(GameObject source, Vector3 from) {
        // the armor intro stays up until the first throw
        if (!hasThrown) {
            hasThrown = true;
            hud.HideBanner();
        }
        GameObject copy = Instantiate(source, transform, true);
        // a bare look-alike: no scripts or colliders wake up on it
        foreach (MonoBehaviour script in
            copy.GetComponentsInChildren<MonoBehaviour>(true)) {
            DestroyImmediate(script);
        }
        foreach (Collider part in copy.GetComponentsInChildren<Collider>(true)) {
            DestroyImmediate(part);
        }
        copy.transform.position = from;
        copy.transform.localScale = source.transform.lossyScale
            * thrownBananaScale;
        copy.SetActive(true);
        projectiles.Add(new Projectile {
            transform = copy.transform,
            start = from,
            ageS = 0f,
            nextTrailS = 0f
        });
    }

    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip("balance numbers: trigger height, king health, banana damage")]
    private WorldBalanceConfig worldBalanceConfig;

    [SerializeField]
    [Tooltip("confirms the revive offer, the ad skip & the reward panel, the "
        + "Player/Jump action (Space, E, Enter)")]
    private InputActionReference confirmAction;

    [SerializeField]
    [Tooltip("gives up the revive offer, the Player/RestartAfterLose action "
        + "(R)")]
    private InputActionReference giveUpAction;

    [Header("Arena")]
    [SerializeField]
    [Tooltip("name shown over the health bar")]
    private string bossName = "COBRA KING";

    [SerializeField]
    [Tooltip("height of the king's coil above where the monkey entered; u")]
    private float nestHeightU = 11f;

    [SerializeField]
    [Tooltip("the great cobra's head is held this far below the entry; u")]
    private float guardDepthU = 12f;

    [SerializeField]
    [Tooltip("a monkey this far below the entry hears the king's warning; u")]
    private float retreatLimitU = 6f;


    [SerializeField]
    [Tooltip("hazards & branches this close below the coil are cleared, so "
        + "none pokes through the king; u")]
    private float nestClearanceU = 2f;

    [SerializeField]
    [Tooltip("trunk radius, matching the tree segments; u")]
    private float trunkRadiusU = 5f;

    [SerializeField]
    [Tooltip("the camera pulls back to this many times its normal distance "
        + "in the arena, to show the king & the monkey together")]
    private float cameraDistanceScale = 2f;

    [SerializeField]
    [Tooltip("share of the height from the monkey up to the king the camera "
        + "aims up by; kept low so a knocked-down monkey stays in view")]
    private float cameraFocusShare = 0.3f;

    [SerializeField]
    [Tooltip("most the camera aims up toward the king; u")]
    private float maxCameraFocusU = 3f;

    [Header("Keep Away (ceiling & head butt)")]
    [SerializeField]
    [Tooltip("the monkey's climb is capped this far below the king's chin; u")]
    private float ceilingGapU = 1.5f;

    [SerializeField]
    [Tooltip("a monkey within this below the ceiling gets butted down; u")]
    private float pushRangeU = 1.2f;

    [SerializeField]
    [Tooltip("the head rears back this long before a butt; s")]
    private float pushWindUpS = 0.25f;

    [SerializeField]
    [Tooltip("least time between two butts; s")]
    private float pushCooldownS = 0.8f;

    [Header("Fight Loop")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip(
        "the tail sweep joins the rounds once the king has lost this share "
            + "of its health, a little b4 the first third"
    )]
    private float sweepFromLostShare = 0.2f;

    [SerializeField]
    [Tooltip("rest with the jewel lit between attack rounds; s")]
    private float restS = 2f;

    [SerializeField]
    [Tooltip("rest in the last third of the king's health; s")]
    private float restFinalS = 1.5f;

    [SerializeField]
    [Tooltip("warnings in the last third last this share of the normal; "
        + "0~1")]
    private float finalWarnScale = 0.75f;

    [Header("Bite (fatal)")]
    [SerializeField]
    [Tooltip("the target is marked this long before the bite lands; s")]
    private float biteWarnS = 1.2f;

    [SerializeField]
    [Tooltip("a monkey within this of the marked spot is bitten; u")]
    private float biteRadiusU = 2.2f;

    [Header("Tail Sweep (fatal)")]
    [SerializeField]
    [Tooltip("the ring is marked this long before the tail sweeps; s")]
    private float sweepWarnS = 1.3f;

    [SerializeField]
    [Tooltip("time the tail takes for a full loop round the trunk; s")]
    private float sweepTimeS = 0.9f;

    [SerializeField]
    [Tooltip("a monkey within this above or below the ring is swept; u")]
    private float sweepBandHalfU = 1.2f;

    [SerializeField]
    [Min(1)]
    [Tooltip("beads in the sweeping tail")]
    private int tailBeadCount = 7;

    [Header("Minions (knock-down)")]
    [SerializeField]
    [Min(1)]
    [Tooltip("small snakes per wave, one of them left out as the gap")]
    private int minionCount = 5;

    [SerializeField]
    [Tooltip("angle between neighbouring minions round the trunk; deg")]
    private float minionSpreadDeg = 28f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("delay between neighbouring minions in a wave, so the wave "
        + "sweeps across; s")]
    private float minionStaggerS = 0.18f;

    [SerializeField]
    [Min(0f)]
    [Tooltip(
        "normal mode's finale: pause between one minion wave ending & the "
            + "next one's warning; s"
    )]
    private float minionStreamGapS = 3.5f;

    [SerializeField]
    [Min(0f)]
    [Tooltip(
        "normal mode's finale: a minion wave & a bite or sweep take turns, "
            + "their dangerous parts overlapping at most this long; s"
    )]
    private float finaleOverlapS = 0.6f;


    [SerializeField]
    [Min(1)]
    [Tooltip("waves per minion attack, each re-aimed at the monkey w/ a new "
        + "gap")]
    private int minionWaves = 2;


    [SerializeField]
    [Tooltip("drop spots are marked this long before the minions fall; s")]
    private float minionWarnS = 0.9f;

    [SerializeField]
    [Tooltip("minion fall speed; u/s")]
    private float minionFallSpeedU = 9f;

    [SerializeField]
    [Tooltip("a minion this close knocks the monkey down; u")]
    private float minionHitRadiusU = 1.1f;


    [Header("Spider Webs")]
    [SerializeField]
    [Tooltip("the web prefab the tree segments use")]
    private SpiderWebTrap webPrefab;

    [SerializeField]
    [Min(0)]
    [Tooltip("webs on the trunk when the fight begins")]
    private int webCount = 3;

    [SerializeField]
    [Min(0)]
    [Tooltip("webs added each time the king loses another third of its "
        + "health")]
    private int websPerThird = 2;

    [SerializeField]
    [Tooltip("webs may hang this far below the entry, as low as a knocked-"
        + "down monkey; u")]
    private float webBelowEntryU = 4f;

    [SerializeField]
    [Tooltip("least gap b/t two webs' centers, so none overlap; u")]
    private float webSeparationU = 4.5f;

    [SerializeField]
    [Tooltip("least gap b/t a web & a branch's root on the trunk; u")]
    private float webBranchGapU = 3f;

    [SerializeField]
    [Min(0)]
    [Tooltip(
        "the climb's web attempts per 10 u, below the arena floor from the "
            + "guard up, as it had there b4 the boss zone"
    )]
    private int lowWebAttemptsPer10U = 3;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("chance one such attempt grows a web, the climb's curve there")]
    private float lowWebChance = 0.44f;

    [SerializeField]
    [Tooltip("in the arena each escape press counts this many times the "
        + "normal, so a web is shaken off within the king's attack gaps")]
    private float webEscapeGainScale = 2f;

    [SerializeField]
    [Tooltip("webs keep this far round the trunk from any fruit; deg")]
    private float webFruitGapDeg = 25f;


    [Header("Bananas")]
    [SerializeField]
    [Tooltip("the tree segments' banana branch prefab")]
    private GameObject bananaBranchPrefab;

    [SerializeField]
    [Tooltip(
        "bare branch prefab, the tree's empty kind, for the arena's own "
            + "obstacle branches"
    )]
    private GameObject branchEmptyPrefab;

    [SerializeField]
    [Tooltip("mesh shown in place of the banana on every arena fruit, "
        + "grabbed & thrown; the watermelon")]
    private Mesh watermelonMesh;

    [SerializeField]
    [Tooltip("local scale of the watermelon on its branch")]
    private Vector3 watermelonScale = new Vector3(6f, 6f, 6f);

    [SerializeField]
    [Tooltip("local offset of the watermelon on its branch; u")]
    private Vector3 watermelonOffset = new Vector3(0f, -0.629f, 0.06f);

    [SerializeField]
    [Min(1)]
    [Tooltip("banana branches in the arena")]
    private int bananaCount = 3;

    [SerializeField]
    [Min(0)]
    [Tooltip("banana branches added each time the king loses another third "
        + "of its health")]
    private int bananasPerThird = 1;

    [SerializeField]
    [Tooltip("lowest & highest banana height above the entry; u")]
    private Vector2 fruitHeightRangeU = new Vector2(2f, 5.5f);

    [SerializeField]
    [Tooltip("radius of a fruit branch's root, as the tree segments' "
        + "BranchFruitPlacementConfig.branchRadius; u")]
    private float branchRadiusU = 6.6f;

    [Header("Arena Branches")]
    [SerializeField]
    [Min(0)]
    [Tooltip(
        "branch attempts per 10 u of arena height; the boss zone grows no "
            + "tree branches, so the fight grows its own, as many as the climb "
            + "had there b4 the zone"
    )]
    private int arenaBranchAttemptsPer10U = 10;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip(
        "chance one attempt grows a branch: the climb's branch curve at the "
            + "fight's height b4 the boss zone"
    )]
    private float arenaBranchChance = 0.47f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip(
        "chance an arena branch bears fruit, the climb's banana chance there; "
            + "it is thrown at the king like any arena fruit, but never grows "
            + "back"
    )]
    private float arenaBranchFruitChance = 0.16f;

    [SerializeField]
    [Tooltip("least gap b/t two branches, the climb's own; u")]
    private float arenaBranchSeparationU = 3f;

    [SerializeField]
    [Tooltip(
        "no arena branch grows this close to the monkey's start spot, so the "
            + "fight never opens w/ a hit; u"
    )]
    private float arenaBranchStartClearU = 6f;

    [SerializeField]
    [Tooltip("a taken banana grows back on its branch after this; s")]
    private float bananaRespawnS = 9f;

    [SerializeField]
    [Tooltip("banana grow-back time as a share of the normal, once 1 and 2 "
        + "thirds of the king's health are lost")]
    private Vector2 bananaRespawnScaleByThird = new Vector2(0.75f, 0.5f);

    [Header("Throw")]
    [SerializeField]
    [Tooltip("time a thrown banana takes to reach the crown; s")]
    private float throwTimeS = 0.7f;

    [SerializeField]
    [Tooltip("how high the throw arcs over the straight line; u")]
    private float throwArcU = 3f;

    [SerializeField]
    [Tooltip("thrown banana size as a share of the grabbed one")]
    private float thrownBananaScale = 1.6f;

    [SerializeField]
    private Color trailColor = new Color(0.3f, 0.8f, 0.3f);

    [Header("Height Marker")]
    [SerializeField]
    [Min(0.05f)]
    [Tooltip(
        "size of the king look-alike on the fight's trunk height marker, as "
            + "a share of the real king"
    )]
    private float markerFigureScale = 0.3f;

    [Header("Rough Bark")]
    [SerializeField]
    [Tooltip(
        "material of the arena's rough bark patches, the same the "
            + "RoughBarkSystem uses"
    )]
    private Material roughBarkMaterial;

    [SerializeField]
    [Min(0)]
    [Tooltip(
        "rough bark attempts per 10 u of arena height, as the climb's "
            + "RoughBarkSpawner had there b4 the boss zone"
    )]
    private int roughBarkAttemptsPer10U = 2;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip(
        "chance one attempt grows a patch: the climb's rough bark curve at "
            + "the fight's height b4 the boss zone"
    )]
    private float roughBarkChance = 0.35f;

    [SerializeField]
    [Tooltip("patch height range, the climb's own; u")]
    private Vector2 roughBarkHeightRangeU = new Vector2(5f, 8.5f);

    [SerializeField]
    [Tooltip("patch width round the trunk, the climb's own; deg")]
    private Vector2 roughBarkArcRangeDeg = new Vector2(110f, 170f);

    [Header("Golden Hint")]
    [SerializeField]
    [Tooltip("banner title when the golden watermelon shows up")]
    private string goldenHintTitle = "Golden watermelon!";

    [SerializeField]
    [Tooltip("line under it")]
    private string goldenHintLine = "Grab it for a Golden Armor";

    [SerializeField]
    [Min(0.5f)]
    [Tooltip("how long that banner stays; s")]
    private float goldenHintS = 3f;

    [Header("Golden Banana")]
    [SerializeField]
    [Tooltip("outline material copied for the golden banana's thick border, "
        + "eg the bananas' grabbable outline")]
    private Material goldenOutlineMaterial;

    [SerializeField]
    private Color goldenOutlineColor = new Color(1f, 0.75f, 0f);

    [SerializeField]
    [Tooltip(
        "golden banana outline width, times the usual grab outline's, so it "
            + "follows the banana model's scale"
    )]
    private float goldenOutlineWidthScale = 2f;

    [Header("Starting Armor")]
    [SerializeField]
    [Tooltip(
        "banner title as the fight begins in the gold armor; it stays up "
            + "until the first banana is thrown"
    )]
    private string armorIntroTitle = "You get a Golden Armor!";

    [SerializeField]
    [Tooltip("line under the banner: what the armor does")]
    private string armorIntroLine = "Blocks 1 fatal hit";

    [Header("Revive")]
    [SerializeField]
    [Tooltip("every loss in the boss fight offers a revive for an ad")]
    private bool canRevive = true;

    [SerializeField]
    [Min(1f)]
    [Tooltip("how long the revive offer waits before it gives up; s")]
    private float reviveOfferS = 8f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("the ad's Skip button shows up after this long; s")]
    private float adSkipAfterS = 3f;

    [SerializeField]
    [Tooltip(
        "speed the revived monkey flies back to where the fight began; u/s"
    )]
    private float reviveReturnSpeedU = 20f;

    [SerializeField]
    [Tooltip(
        "page the ad's Course Page button opens: the CSCI 526 listing in the "
            + "USC Schedule of Classes; update the term as needed"
    )]
    private string courseUrl =
        "https://classes.usc.edu/term-20263/course/csci-526";

    [Header("Mode Choice")]
    [SerializeField]
    [Tooltip("the two choice bananas sit this far left & right of the "
        + "monkey round the trunk; deg")]
    private float choiceSideDeg = 28f;

    [SerializeField]
    [Tooltip("the choice bananas hang this far above the monkey; u")]
    private float choiceAboveU = 1.5f;

    [SerializeField]
    [Tooltip("line under the armor banner after a revive: how to pick a mode")]
    private string chooseLine = "Grab one: EASY or NORMAL";

    [SerializeField]
    [Range(0.1f, 1f)]
    [Tooltip("easy mode: the king's health as a share of the normal")]
    private float easyHealthScale = 0.6f;

    [SerializeField]
    [Tooltip("easy mode: warnings last this many times the normal")]
    private float easyWarnScale = 1.35f;

    [SerializeField]
    [Tooltip("easy mode: rest between attack rounds; s")]
    private float easyRestS = 3.25f;

    [SerializeField]
    [Tooltip("easy mode: rest in the last third of the king's health; s")]
    private float easyRestFinalS = 2.35f;

    [SerializeField]
    [Min(1)]
    [Tooltip("easy mode: waves per minion attack")]
    private int easyMinionWaves = 1;

    [Header("Win Reward")]
    [SerializeField]
    [Tooltip("banner title once the king is beaten")]
    private string winTitle = "You win!";

    [SerializeField]
    [Tooltip("line under the win title")]
    private string winLine = "Golden Armor is yours to keep!";

    [SerializeField]
    [Tooltip("small line under it: what the armor does")]
    private string winDetailLine = "It blocks 1 fatal hit";

    [SerializeField]
    [Tooltip(
        "when the armor takes the great cobra's bite, its head is thrown "
            + "this far back down the trunk; u"
    )]
    private float armorKnockBackU = 20f;

    [SerializeField]
    [Min(0f)]
    [Tooltip(
        "...& it is frozen this long after, longer than a coconut's freeze, "
            + "as the armor is the boss fight's one-off reward; s"
    )]
    private float armorCobraFreezeS = 15f;


    [SerializeField]
    [Tooltip(
        "after a win the great cobra chases again from this far further "
            + "down the trunk than its guard spot, a head start; u"
    )]
    private float winCobraDropU = 45f;

    [SerializeField]
    [Tooltip("the golden banana first shows up this long after the first "
        + "hit on the king; s")]
    private float goldenFirstDelayS = 8f;

    [SerializeField]
    [Tooltip("time between two golden banana visits, once one is gone; s")]
    private float goldenIntervalS = 15f;

    [SerializeField]
    [Tooltip("how long a golden banana stays before it vanishes; s")]
    private float goldenShowS = 8f;

    [SerializeField]
    [Tooltip("it grows this far round the trunk from the monkey, so in "
        + "view; deg")]
    private float goldenSideDeg = 30f;

    [SerializeField]
    [Tooltip("it grows this far above the monkey, kept under the ceiling; u")]
    private float goldenAboveMonkeyU = 3f;

    [SerializeField]
    [Tooltip("the armor's back plate sits this far out from the monkey's "
        + "center; u")]
    private float armorOutU = 0.45f;

    [SerializeField]
    [Tooltip("the armor's helmet sits this far above the monkey's center; u")]
    private float armorHelmetU = 0.95f;

    [SerializeField]
    [Min(0.3f)]
    [Tooltip(
        "win reward: the armor pieces swirl in onto the monkey & pulse for "
            + "this long; s"
    )]
    private float armorRewardS = 1.6f;

    [SerializeField]
    [Tooltip("win reward: the armor pieces start this far out; u")]
    private float armorRewardRadiusU = 3f;

    [Header("Finisher")]
    [SerializeField]
    [Tooltip("time scale of the last blow's slow motion, either way")]
    private float finisherTimeScale = 0.2f;

    [SerializeField]
    [Tooltip("real time the slow motion lasts; s")]
    private float finisherRealS = 1.4f;

    [Header("Victory")]
    [SerializeField]
    [Tooltip("time the crown takes to fly onto the monkey; s")]
    private float crownFlyS = 1.2f;

    [SerializeField]
    [Tooltip("crown size on the monkey, as a share of the king's")]
    private float crownScaleOnMonkey = 0.55f;

    [SerializeField]
    [Tooltip("crown height above the monkey's center; u")]
    private float crownAboveMonkeyU = 1.3f;

    [SerializeField]
    [Tooltip("pause after the crown lands before the victory panel; s")]
    private float victoryDelayS = 0.8f;

    [Header("Dotted Line")]
    [SerializeField]
    [Min(2)]
    [Tooltip("dots from the first banana to the crown")]
    private int dotCount = 12;

    [SerializeField]
    [Tooltip("dot radius; u")]
    private float dotRadiusU = 0.12f;

    [Header("Colors")]


    [SerializeField]
    private Color goldenColor = new Color(1f, 0.82f, 0.15f);

    [SerializeField]
    private Color warningColor = new Color(1f, 0.15f, 0.15f);

    [SerializeField]
    private Color warningDimColor = new Color(0.45f, 0.05f, 0.05f);



    [SerializeField]
    private Color minionColor = new Color(0.45f, 0.8f, 0.45f);

    [SerializeField]
    private Color tailColor = new Color(0.72f, 0.56f, 0.95f);

    [Header("Speech")]
    [SerializeField]
    private string approachLine = "Stay away from me!";

    [SerializeField]
    private string retreatLine = "Nowhere to run!";

    [SerializeField]
    private string firstHitLine = "How dare you!";


    [SerializeField]
    [Tooltip("said once a game, the first time the minions come")]
    private string minionCallLine = "Minions, help me!";


    [SerializeField]
    [Tooltip("the same line is not repeated within this; s")]
    private float speechCooldownS = 4f;

    [SerializeField]
    [Tooltip("a key line, eg the minion call, holds the bubble this long; "
        + "s")]
    private float keyLineHoldS = 2.2f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (worldBalanceConfig == null) {
            Debug.LogError(
                "BossFightController:\tmust assign Inspector Field: "
                    + "worldBalanceConfig",
                this
            );
            enabled = false;
            return;
        }

        if (bananaBranchPrefab == null) {
            Debug.LogError(
                "BossFightController:\tmust assign Inspector Field: "
                    + "bananaBranchPrefab",
                this
            );
            enabled = false;
            return;
        }

        if (I != null && I != this) {
            Debug.LogWarning(
                "BossFightController:\tduplicate instance destroyed",
                this
            );
            Destroy(gameObject);
            return;
        }
        I = this;

        if (confirmAction == null || giveUpAction == null) {
            Debug.LogWarning(
                "BossFightController:\tmust assign Inspector Fields: "
                    + "confirmAction, giveUpAction",
                this
            );
        }

        body = GetComponent<BossKingBody>();
        body.MonkeyTouched += OnKingTouched;
        hud = GetComponent<BossFightHud>();
        hud.WatchAdClicked += WatchAd;
        hud.GiveUpClicked += GiveUp;
        hud.CoursePageClicked += OpenCoursePage;
        hud.ContinueClicked += ContinueAfterReward;
        hud.SkipAdClicked += SkipAd;
        colorBlock = new MaterialPropertyBlock();
    }

    // the endless run has no fight: it starts in the state right after one
    private void Start() {
        if (I == this && GameController.I != null
            && GameController.I.Mode == RunMode.Endless) {
            StartAsDone();
        }
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
        TreeSegmentController.BranchesGenerated -= OnBranchesGenerated;
    }

    // the actions are shared with other scripts, so they are only switched
    // on here, never off
    private void OnEnable() {
        confirmAction?.action.Enable();
        giveUpAction?.action.Enable();
    }

    private void OnDisable() {
        if (
            phase == Phase.Finishing
            || phase == Phase.ReviveOffer
            || phase == Phase.WatchingAd
            || phase == Phase.Rewarding
        ) {
            Time.timeScale = 1f;
        }
        if (struggle != null) {
            struggle.EscapeGainScale = 1f;
        }
        if (monkeyController != null) {
            monkeyController.ClearHeightCeiling();
        }
        if (cameraFraming != null) {
            cameraFraming.SetBossFraming(0f, 1f);
        }
    }

    private void Update() {
        GameController game = GameController.I;
        if (game == null) {
            return;
        }
        if (phase == Phase.Waiting) {
            if (game.State == GameState.Playing && HasReachedTrigger()) {
                BeginFight();
            }
            return;
        }
        if (!game.IsRunLive || monkey == null) {
            return;
        }
        // after a win only the reward armor is left to follow the monkey
        if (phase == Phase.Done) {
            UpdateShieldRing();
            UpdateSparks();
            // the Spider King fight frames the camera itself
            SpiderFightController spiderFight = SpiderFightController.I;
            if (spiderFight == null || !spiderFight.IsFraming) {
                UpdateCameraFocus();
            }
            UpdateChaseWarning();
            return;
        }
        if (phase == Phase.Rewarding) {
            UpdateShieldRing();
            UpdateSparks();
            UpdateCameraFocus();
            UpdateRewardPause(game);
            return;
        }

        body.Track(monkey.position);
        hud.SpeechAnchor = body.HeadPosition;
        UpdateProjectiles();
        UpdateFruits();
        UpdateGolden();
        UpdateShieldRing();
        UpdateCameraFocus();
        if (phase == Phase.Fighting) {
            UpdateReturnToStart();
            UpdatePendingLine();
            UpdateWarnings();
            UpdateChoiceTags();
            UpdateFirstThrowHint();
            UpdateFightLoop();
            UpdateMinionStream();
        } else if (phase == Phase.ReviveOffer || phase == Phase.WatchingAd) {
            UpdateRevive(game);
        } else if (phase == Phase.Finishing) {
            UpdateFinisher();
        } else if (phase == Phase.Defeated) {
            UpdateVictory();
        }
    }

    // private methods  ########################################################
    // Setup  ------------------------------------------------------------------
    private bool HasReachedTrigger() {
        RampedDifficultyManager ramp = RampedDifficultyManager.I;
        return ramp != null
            && ramp.CurrentPlayerClimbedYDistance
                >= worldBalanceConfig.BossTriggerHeightU;
    }

    // the endless start: the armor worn, hazards on, the fight already over;
    // no arena, no FightStarted, and never marks the king beaten
    private void StartAsDone() {
        MonkeyController monkeyController =
            FindFirstObjectByType<MonkeyController>();
        if (monkeyController == null
            || monkeyController.transform.parent == null) {
            Debug.LogError(
                "BossFightController:\tno MonkeyController under a trunk "
                    + "root found",
                this
            );
            enabled = false;
            return;
        }
        monkey = monkeyController.transform;
        this.monkeyController = monkeyController;
        trunkCenter = monkey.parent.position;
        greatCobra = FindFirstObjectByType<CobraMotor>();
        cameraFraming = FindFirstObjectByType<CameraClimbFraming>();
        struggle = FindFirstObjectByType<SpiderWebStruggle>();

        isEndlessStart = true;
        BuildShieldRing();
        hasShield = true;
        phase = Phase.Done;
        hud.ShowArmorBadge(true);
        // no chase call: the cobra is already after the monkey
        hasWarnedChase = true;
        if (Debug.isDebugBuild) {
            Debug.Log("BossFightController:\tendless start in armor", this);
        }
    }

    private void BeginFight() {
        MonkeyController monkeyController =
            FindFirstObjectByType<MonkeyController>();
        if (monkeyController == null
            || monkeyController.transform.parent == null) {
            Debug.LogError(
                "BossFightController:\tno MonkeyController under a trunk "
                    + "root found",
                this
            );
            enabled = false;
            return;
        }
        monkey = monkeyController.transform;
        this.monkeyController = monkeyController;
        trunkCenter = monkey.parent.position;
        entryY = monkey.position.y;
        entryPosition = monkey.position;
        entryRotation = monkey.rotation;
        coilBottomY = entryY + nestHeightU;
        maxHealth = worldBalanceConfig.BossMaxHealth;
        health = maxHealth;
        phase = Phase.Fighting;
        FightStarted?.Invoke();
        loop = Loop.Intro;

        WarnIfArenaLeavesZone();
        // the boss zone grows nothing but the fight; only a branch of a
        // segment reaching past the zone end can hang near the nest
        ClearBranchesNearNest(
            FindObjectsByType<BranchController>(FindObjectsSortMode.None)
        );
        TreeSegmentController.BranchesGenerated += OnBranchesGenerated;

        cameraFraming = FindFirstObjectByType<CameraClimbFraming>();
        greatCobra = FindFirstObjectByType<CobraMotor>();
        if (greatCobra != null) {
            greatCobra.StartGuard(entryY - guardDepthU);
        }

        body.Build(trunkCenter, coilBottomY, AngleOf(monkey.position));
        body.SetWeakSpotLit(true);
        // the first try is normal mode; the mode choice comes w/ a revive
        SpawnFruits();
        SpawnArenaBranches();
        SpawnWebs(webCount);
        SpawnLowWebs();
        SpawnRoughBark();
        struggle = FindFirstObjectByType<SpiderWebStruggle>();
        if (struggle != null) {
            struggle.EscapeGainScale = webEscapeGainScale;
        }
        BuildDots();
        BuildShieldRing();

        hud.Show(bossName);
        hud.Flash();
        // the monkey starts in the gold armor, a spare life against the
        // king; the golden banana brings it back once lost
        hasShield = true;
        hud.ShowBannerFor(armorIntroTitle, armorIntroLine, goldenColor, 0f);
        if (Debug.isDebugBuild) {
            Debug.Log(
                "BossFightController:\tboss fight begins at y " + entryY,
                this
            );
        }
    }

    // the boss zone grows no tree branches, but the fight was tuned w/ the
    // climb's own around it: the same count, spread & fruit, from the guard
    // up to the nest, grown here once & kept through a restart
    private void SpawnArenaBranches() {
        if (branchEmptyPrefab == null || bananaBranchPrefab == null) {
            return;
        }
        float bottomY = entryY - guardDepthU;
        float topY = coilBottomY - body.HangDepthU - nestClearanceU;
        List<Vector3> placed = new List<Vector3>();
        foreach (ArenaFruit fruit in fruits) {
            if (fruit.branch != null) {
                placed.Add(fruit.branch.transform.position);
            }
        }
        for (float sliceY = bottomY; sliceY < topY; sliceY += 10f) {
            float sliceTopY = Mathf.Min(topY, sliceY + 10f);
            int attempts = Mathf.RoundToInt(
                arenaBranchAttemptsPer10U * (sliceTopY - sliceY) / 10f
            );
            for (int i = 0; i < attempts; i++) {
                if (Random.value >= arenaBranchChance) {
                    continue;
                }
                TryAddArenaBranch(sliceY, sliceTopY, placed);
            }
        }
    }

    private void TryAddArenaBranch(
        float minY, float maxY, List<Vector3> placed
    ) {
        for (int attempt = 0; attempt < 30; attempt++) {
            float angle = Random.Range(0f, 360f);
            Vector3 position = PointOnTrunk(
                angle, Random.Range(minY, maxY), branchRadiusU
            );
            if (Vector3.Distance(position, entryPosition) < arenaBranchStartClearU) {
                continue;
            }
            bool isClear = true;
            foreach (Vector3 other in placed) {
                if (Vector3.Distance(position, other) < arenaBranchSeparationU) {
                    isClear = false;
                    break;
                }
            }
            if (!isClear) {
                continue;
            }
            bool hasFruit = Random.value < arenaBranchFruitChance;
            GameObject branch = Instantiate(
                hasFruit ? bananaBranchPrefab : branchEmptyPrefab, transform
            );
            branch.transform.SetPositionAndRotation(
                position, Quaternion.Euler(0f, 90f - angle, 0f)
            );
            BranchController controller = branch.GetComponent<BranchController>();
            if (controller != null) {
                controller.Setup();
            }
            if (hasFruit) {
                ScorePickup pickup =
                    branch.GetComponentInChildren<ScorePickup>(true);
                if (pickup != null) {
                    ApplyWatermelon(pickup.gameObject);
                    arenaBranchFruits.Add(pickup.gameObject);
                }
            }
            placed.Add(position);
            arenaBranchSpots.Add(PointOnTrunk(angle, position.y, trunkRadiusU));
            return;
        }
    }

    // the arena, guard cobra down to the king's nest, belongs inside the boss
    // zone, where nothing else is generated
    private void WarnIfArenaLeavesZone() {
        RampedDifficultyManager ramp = RampedDifficultyManager.I;
        if (ramp == null) {
            return;
        }
        float zoneStartY = ramp.PlayerGameStartYPosition
            + worldBalanceConfig.BossZoneStartU;
        if (entryY - guardDepthU < zoneStartY) {
            Debug.LogWarning(
                "BossFightController:\tthe guard cobra hangs below the boss "
                + "zone start, ordinary hazards may reach the arena; move "
                + "the trigger up or shorten guardDepthU",
                this
            );
        }
    }

    // a segment recycled over the top during the fight rolls new branches
    private void OnBranchesGenerated(TreeSegmentController segment) {
        BranchController[] branches =
            segment.GetComponentsInChildren<BranchController>();
        ClearBranchesNearNest(branches);
    }

    private void ClearBranchesNearNest(BranchController[] branches) {
        float minY = coilBottomY - body.HangDepthU - nestClearanceU;
        foreach (BranchController branch in branches) {
            if (branch.transform.position.y > minY) {
                ForgetFruits(branch.gameObject);
                branch.gameObject.SetActive(false);
            }
        }
    }

    // two banana branches beside the monkey, plain & golden: the first throw
    // picks easy or normal mode
    private void SpawnChoices() {
        float angle = AngleOf(entryPosition);
        float y = entryPosition.y + choiceAboveU;
        AddFruit(angle - choiceSideDeg, y, false);
        AddFruit(angle + choiceSideDeg, y, true);
        choices.Add(fruits[fruits.Count - 2]);
        choices.Add(fruits[fruits.Count - 1]);
        fruits.RemoveRange(fruits.Count - 2, 2);
        isChoosing = true;
    }

    private void ChooseMode(bool isEasyPick) {
        isChoosing = false;
        isEasy = isEasyPick;
        maxHealth = isEasy
            ? Mathf.Max(
                1,
                Mathf.RoundToInt(worldBalanceConfig.BossMaxHealth * easyHealthScale)
            )
            : worldBalanceConfig.BossMaxHealth;
        health = maxHealth;
        hud.SetHealth(1f);
        foreach (ArenaFruit choice in choices) {
            RemoveBranch(choice.branch);
        }
        choices.Clear();
        hud.SetChoiceTags(false, Vector3.zero, Vector3.zero);
        SpawnFruits();
        hud.PopText(
            isEasy ? "Easy mode" : "Normal mode",
            monkey.position,
            isEasy ? new Color(0.55f, 0.95f, 0.55f) : goldenColor
        );
        if (Debug.isDebugBuild) {
            Debug.Log(
                "BossFightController:\t" + (isEasy ? "easy" : "normal")
                    + " mode, king health " + maxHealth,
                this
            );
        }
    }

    // the monkey's fruit sensor never sees a trigger exit for a fruit that
    // is switched off or destroyed, & a destroyed one would break its reach
    // & grab every physics step, so it is told to forget it first
    private static void ForgetFruits(GameObject branch) {
        UpwardFruitSensor sensor = UpwardFruitSensor.I;
        if (sensor == null || branch == null) {
            return;
        }
        foreach (Collider part in branch.GetComponentsInChildren<Collider>(true)) {
            sensor.RemoveFruit(part);
        }
    }

    private static void RemoveBranch(GameObject branch) {
        if (branch == null) {
            return;
        }
        ForgetFruits(branch);
        Destroy(branch);
    }

    // every arena banana branch goes, eg for a restart
    private void ClearFruits() {
        foreach (ArenaFruit fruit in fruits) {
            RemoveBranch(fruit.branch);
        }
        fruits.Clear();
        foreach (ArenaFruit choice in choices) {
            RemoveBranch(choice.branch);
        }
        choices.Clear();
        goldenIndex = -1;
        isChoosing = false;
        hud.SetChoiceTags(false, Vector3.zero, Vector3.zero);
    }

    private string ChoiceLine => armorIntroLine + "\n" + chooseLine;

    private void SpawnFruits() {
        float startAngle = AngleOf(monkey.position);
        for (int i = 0; i < bananaCount; i++) {
            // spread round the trunk, the first one right above the monkey
            float t = bananaCount > 1 ? i / (bananaCount - 1f) : 0f;
            AddFruit(
                startAngle + i * 360f / bananaCount,
                entryY + Mathf.Lerp(
                    fruitHeightRangeU.x, fruitHeightRangeU.y, t
                ),
                false
            );
        }
        // the golden banana starts hidden & shows up once the fight is on
        goldenIndex = AddFruit(startAngle, entryY, true);
        HideGolden();
    }

    // extra banana branches at random spots round the trunk
    private void AddBananas(int count) {
        for (int i = 0; i < count; i++) {
            AddFruit(
                Random.Range(0f, 360f),
                entryY + Random.Range(fruitHeightRangeU.x, fruitHeightRangeU.y),
                false
            );
        }
    }

    private bool IsNearSpot(float angle, float y, Vector3 spot) {
        return Mathf.Abs(Mathf.DeltaAngle(angle, AngleOf(spot))) < 30f
            && Mathf.Abs(y - spot.y) < 3f;
    }

    // webs on the trunk in the arena, never on a fruit's branch line
    private void SpawnWebs(int count) {
        if (webPrefab == null) {
            return;
        }
        float topY = body.LowestY - ceilingGapU - 1.5f;
        for (int i = 0; i < count; i++) {
            TryAddWeb(entryY - webBelowEntryU, Mathf.Max(entryY + 1f, topY));
        }
    }

    // the climb's own webs below the arena floor, as it had there b4 the
    // boss zone: a few rolls per 10 u from the guard up to just under the
    // entry
    private void SpawnLowWebs() {
        if (webPrefab == null) {
            return;
        }
        float bottomY = entryY - guardDepthU;
        float topY = entryY - 3f;
        for (float sliceY = bottomY; sliceY < topY; sliceY += 10f) {
            float sliceTopY = Mathf.Min(topY, sliceY + 10f);
            int attempts = Mathf.RoundToInt(
                lowWebAttemptsPer10U * (sliceTopY - sliceY) / 10f
            );
            for (int i = 0; i < attempts; i++) {
                if (Random.value < lowWebChance) {
                    TryAddWeb(sliceY, sliceTopY);
                }
            }
        }
    }

    // a web at a free spot b/t minY & maxY: off the fruit's branch lines, the
    // monkey & its start, & never overlapping another web or a branch
    private void TryAddWeb(float minY, float maxY) {
        for (int attempt = 0; attempt < 16; attempt++) {
            float angle = Random.Range(0f, 360f);
            if (IsNearFruit(angle)) {
                continue;
            }
            float y = Random.Range(minY, maxY);
            Vector3 spot = PointOnTrunk(angle, y, trunkRadiusU + 0.02f);
            if (IsWebCrowded(spot)) {
                continue;
            }
            // never right on the monkey, nor on its start spot, where
            // a restart brings it back
            bool isOnMonkey = IsNearSpot(angle, y, monkey.position)
                || IsNearSpot(angle, y, entryPosition);
            if (isOnMonkey) {
                continue;
            }
            // as the segments' spawner: the web's face looks away from
            // the trunk
            SpiderWebTrap web = Instantiate(
                webPrefab,
                PointOnTrunk(angle, y, trunkRadiusU + 0.02f),
                Quaternion.Euler(0f, -angle, 0f),
                transform
            );
            webs.Add(web);
            return;
        }
    }

    private bool IsWebCrowded(Vector3 spot) {
        foreach (SpiderWebTrap other in webs) {
            if (other != null
                && Vector3.Distance(other.transform.position, spot)
                    < webSeparationU) {
                return true;
            }
        }
        foreach (Vector3 branch in arenaBranchSpots) {
            if (Vector3.Distance(branch, spot) < webBranchGapU) {
                return true;
            }
        }
        return false;
    }

    // the teammates' rough bark, a few patches in the arena that slow the
    // monkey while it touches them; kept off its start spot & the banana
    // branches, & kept through a restart
    private void SpawnRoughBark() {
        if (roughBarkMaterial == null || roughBarkAttemptsPer10U <= 0) {
            return;
        }
        // as the climb's spawner: per 10 u a few rolls, each patch kept
        // clear of the others' heights, of branches & webs, & here of the
        // monkey's start spot
        float bottomY = entryY - guardDepthU;
        float topY = coilBottomY - body.HangDepthU - nestClearanceU;
        List<Vector3> patches = new List<Vector3>(); // x angle, y y, z extent
        Physics.SyncTransforms();
        for (float sliceY = bottomY; sliceY < topY; sliceY += 10f) {
            float sliceTopY = Mathf.Min(topY, sliceY + 10f);
            for (int i = 0; i < roughBarkAttemptsPer10U; i++) {
                if (Random.value >= roughBarkChance) {
                    continue;
                }
                TryAddRoughBark(sliceY, sliceTopY, patches);
            }
        }
        // the rolls can come up empty where the monkey fights; one patch
        // there is sure
        bool hasPlayPatch = false;
        foreach (Vector3 patch in patches) {
            if (patch.y + patch.z > entryY - 2f && patch.y - patch.z < topY) {
                hasPlayPatch = true;
            }
        }
        if (!hasPlayPatch) {
            TryAddRoughBark(entryY - 2f, topY, patches);
        }
    }

    private void TryAddRoughBark(
        float minY, float maxY, List<Vector3> patches
    ) {
        for (int attempt = 0; attempt < 16; attempt++) {
            float height = Random.Range(
                roughBarkHeightRangeU.x, roughBarkHeightRangeU.y
            );
            float arc = Random.Range(
                roughBarkArcRangeDeg.x, roughBarkArcRangeDeg.y
            );
            float extent = RoughBarkZone.GetVerticalExtent(height);
            float y = Random.Range(minY, maxY);
            float angle = Random.Range(0f, 360f);
            bool isClear = true;
            foreach (Vector3 other in patches) {
                if (Mathf.Abs(other.y - y) < extent + other.z + 1f) {
                    isClear = false;
                    break;
                }
            }
            bool isOnStart = Mathf.Abs(
                Mathf.DeltaAngle(angle, AngleOf(entryPosition))
            ) < arc * 0.5f + 15f && Mathf.Abs(y - entryY) < extent + 1f;
            if (!isClear || isOnStart || IsArcBlocked(angle, y, extent, arc)) {
                continue;
            }
            AddRoughBark(angle, y, height, arc);
            patches.Add(new Vector3(angle, y, extent));
            return;
        }
    }

    private bool IsFruitBranch(Transform part) {
        foreach (ArenaFruit fruit in fruits) {
            if (fruit.branch != null && part.IsChildOf(fruit.branch.transform)) {
                return true;
            }
        }
        return false;
    }

    // the climb's check: no fruit or web on the patch's arc
    private bool IsArcBlocked(float angle, float y, float extent, float arc) {
        float radius = trunkRadiusU + 0.05f;
        int steps = Mathf.Max(
            1, Mathf.CeilToInt(radius * arc * Mathf.Deg2Rad / 0.7f)
        );
        for (int i = 0; i <= steps; i++) {
            float sample = angle + (i / (float)steps - 0.5f) * arc;
            Vector3 point = PointOnTrunk(sample, y, radius);
            foreach (Collider hit in Physics.OverlapCapsule(
                point - Vector3.up * extent, point + Vector3.up * extent, 0.7f,
                Physics.AllLayers, QueryTriggerInteraction.Collide
            )) {
                // an obstacle branch may stick out over a patch; only a
                // fruit's branch & a web keep it off
                if (hit.GetComponentInParent<SpiderWebTrap>() != null
                    || IsFruitBranch(hit.transform)) {
                    return true;
                }
            }
        }
        return false;
    }

    // built on the tree segment at its height, so its mesh hugs the
    // faceted trunk, then kept under the fight so no recycle moves it
    private void AddRoughBark(float angle, float y, float height, float arc) {
        GameObject patch = new GameObject("BossRoughBarkPatch");
        Transform segment = NearestSegment(y);
        patch.transform.SetParent(segment != null ? segment : transform, false);
        patch.transform.position = new Vector3(trunkCenter.x, y, trunkCenter.z);
        RoughBarkZone zone = patch.AddComponent<RoughBarkZone>();
        // the bark measures angles from +z toward +x, this fight from +x
        zone.Initialize(
            trunkRadiusU + 0.05f, height, 90f - angle, arc,
            roughBarkMaterial, true, Random.Range(0, 100000)
        );
        patch.transform.SetParent(transform, true);
    }

    private static Transform NearestSegment(float y) {
        Transform nearest = null;
        float best = float.MaxValue;
        foreach (TreeSegmentController segment in
            FindObjectsByType<TreeSegmentController>(FindObjectsSortMode.None)) {
            float distance = Mathf.Abs(segment.transform.position.y - y);
            if (distance < best) {
                best = distance;
                nearest = segment.transform;
            }
        }
        return nearest;
    }

    private bool IsNearFruit(float angle) {
        foreach (ArenaFruit fruit in fruits) {
            if (fruit.isGolden) {
                continue;
            }
            float fruitAngle = AngleOf(fruit.root.transform.position);
            if (Mathf.Abs(Mathf.DeltaAngle(angle, fruitAngle)) < webFruitGapDeg) {
                return true;
            }
        }
        return false;
    }

    private float BananaRespawnScale => Third >= 2
        ? bananaRespawnScaleByThird.y
        : Third >= 1
            ? bananaRespawnScaleByThird.x
            : 1f;

    /// <returns>the new fruit's index in the fruit list</returns>
    // a real banana branch, as the tree segments grow, always bearing; the
    // golden one is the same banana tinted gold w/ a thick gold outline
    private int AddFruit(float angle, float y, bool isGolden) {
        GameObject branch = Instantiate(bananaBranchPrefab, transform);
        PlaceBranch(branch.transform, angle, y);
        BranchController controller = branch.GetComponent<BranchController>();
        if (controller != null) {
            controller.Setup(); // the banana branch prefab bears a banana
        }
        ScorePickup pickup = branch.GetComponentInChildren<ScorePickup>(true);
        GameObject banana = pickup != null ? pickup.gameObject : branch;
        ApplyWatermelon(banana);
        if (isGolden) {
            MakeGolden(banana);
        }

        fruits.Add(new ArenaFruit {
            root = banana,
            branch = branch,
            trigger = banana.GetComponent<Collider>(),
            isGolden = isGolden,
            respawnTime = -1f
        });
        return fruits.Count - 1;
    }

    // the branch root sits on the segments' branch ring, its +z away from the
    // trunk, high enough that the banana hangs about the given height
    private void PlaceBranch(Transform branch, float angle, float fruitY) {
        branch.SetPositionAndRotation(
            PointOnTrunk(angle, fruitY + BANANA_HANG_U, branchRadiusU),
            Quaternion.Euler(0f, 90f - angle, 0f)
        );
    }

    // the arena's fruit looks like a watermelon: the banana model's mesh is
    // swapped, its collider & scripts stay, so grab & throw work as before
    private void ApplyWatermelon(GameObject fruit) {
        if (watermelonMesh == null || fruit == null) {
            return;
        }
        foreach (MeshFilter filter in
            fruit.GetComponentsInChildren<MeshFilter>(true)) {
            filter.sharedMesh = watermelonMesh;
            filter.transform.localRotation = Quaternion.identity;
            filter.transform.localScale = watermelonScale;
            filter.transform.localPosition = watermelonOffset;
        }
    }

    private void MakeGolden(GameObject banana) {
        // its own outline replaces the reach-target one
        foreach (FruitOutlineListener listener in
            banana.GetComponentsInChildren<FruitOutlineListener>(true)) {
            Destroy(listener);
        }
        Material outline = null;
        if (goldenOutlineMaterial != null) {
            outline = new Material(goldenOutlineMaterial);
            outline.SetColor(OUTLINE_COLOR, goldenOutlineColor);
            outline.SetFloat(
                OUTLINE_WIDTH,
                goldenOutlineMaterial.GetFloat(OUTLINE_WIDTH)
                    * goldenOutlineWidthScale
            );
        }
        foreach (Renderer part in banana.GetComponentsInChildren<Renderer>(true)) {
            Material baseMaterial = part.sharedMaterial;
            if (outline != null) {
                part.sharedMaterials = new[] { baseMaterial, outline };
            }
            SetColor(part, goldenColor);
        }
    }

    // now & then the golden banana grows in sight, beside the monkey, for a
    // short while; only once the first banana has hit the king
    private void UpdateGolden() {
        if (goldenIndex < 0 || phase != Phase.Fighting || !hasHit) {
            return;
        }
        ArenaFruit golden = fruits[goldenIndex];
        if (golden.branch.activeSelf) {
            if (Time.time >= goldenHideTime) {
                HideGolden();
                nextGoldenTime = Time.time + goldenIntervalS;
            }
            return;
        }
        if (Time.time >= nextGoldenTime && !hasShield) {
            ShowGolden();
        }
    }

    private void ShowGolden() {
        ArenaFruit golden = fruits[goldenIndex];
        float side = Random.value < 0.5f ? -1f : 1f;
        float angle = AngleOf(monkey.position) + side * goldenSideDeg;
        float y = Mathf.Clamp(
            monkey.position.y + goldenAboveMonkeyU,
            entryY + fruitHeightRangeU.x,
            body.LowestY - ceilingGapU - 0.5f
        );
        PlaceBranch(golden.branch.transform, angle, y);
        golden.branch.SetActive(true);
        golden.root.SetActive(true);
        goldenHideTime = Time.time + goldenShowS;
        hud.PopText(
            "Golden watermelon!", golden.root.transform.position, goldenColor
        );
        // & a banner low on screen, so it is never missed
        hud.ShowBannerFor(
            goldenHintTitle, goldenHintLine, goldenColor, goldenHintS
        );
    }

    private void HideGolden() {
        ForgetFruits(fruits[goldenIndex].branch);
        fruits[goldenIndex].branch.SetActive(false);
    }



    // dotted line from the first banana to the crown, until the 1st hit
    private void BuildDots() {
        dots = new Transform[dotCount];
        for (int i = 0; i < dotCount; i++) {
            dots[i] = CreatePrimitive(
                PrimitiveType.Sphere, "HintDot" + i,
                Vector3.one * dotRadiusU * 2f, Color.white
            ).transform;
        }
    }

    // gold armor worn while shielded: a back plate facing the camera, two
    // shoulder pads & a helmet, all plain primitives
    private void BuildShieldRing() {
        armor = new Transform[4];
        armor[0] = CreatePrimitive(
            PrimitiveType.Cylinder, "ArmorPlate",
            new Vector3(1.1f, 0.08f, 1.3f), goldenColor
        ).transform;
        for (int side = 0; side < 2; side++) {
            armor[1 + side] = CreatePrimitive(
                PrimitiveType.Sphere, side == 0 ? "ArmorPadL" : "ArmorPadR",
                new Vector3(0.6f, 0.4f, 0.6f), goldenColor
            ).transform;
        }
        armor[3] = CreatePrimitive(
            PrimitiveType.Sphere, "ArmorHelmet",
            new Vector3(0.85f, 0.5f, 0.85f), goldenColor
        ).transform;
        armorBaseScales = new Vector3[armor.Length];
        for (int i = 0; i < armor.Length; i++) {
            armorBaseScales[i] = armor[i].localScale;
            armor[i].gameObject.SetActive(false);
        }
    }

    // Fight Loop  -------------------------------------------------------------
    // rest (jewel lit) → a round of attacks (jewel dark) → rest ...
    private void UpdateFightLoop() {
        switch (loop) {
            case Loop.Intro:
                // waits for the first hit, which starts the first rest
                break;
            case Loop.Rest:
                loopTimeS -= Time.deltaTime;
                // in the finale a round waits for a running wave to near its
                // end, so the two only overlap a little
                bool isWaveClear = !isMinionStreaming
                    || WaveDangerLeftS() <= finaleOverlapS;
                if (loopTimeS <= 0f && isWaveClear) {
                    StartRound();
                }
                break;
            case Loop.Attack:
                UpdateAttack();
                break;
        }
    }

    private void StartRest() {
        loop = Loop.Rest;
        loopTimeS = isEasy
            ? (Third >= 2 ? easyRestFinalS : easyRestS)
            : (Third >= 2 ? restFinalS : restS);
        body.SetWeakSpotLit(true);
    }

    // two bites a round, & each lost third of health adds an attack
    private void StartRound() {
        loop = Loop.Attack;
        attackQueue.Clear();
        // in normal mode's finale the minions stream on their own, so a
        // round is a single bite or sweep, taking turns: never more than two
        // attacks at once
        if (IsMinionFinale) {
            attackQueue.Enqueue(isFinaleSweepNext ? Attack.Sweep : Attack.Bite);
            isFinaleSweepNext = !isFinaleSweepNext;
            NextAttack();
            return;
        }
        if (Third >= 2) {
            attackQueue.Enqueue(Attack.Minions);
        }
        attackQueue.Enqueue(Attack.Bite);
        // the tail joins a little b4 the first third is lost
        if (LostShare >= sweepFromLostShare) {
            attackQueue.Enqueue(Attack.Sweep);
        }
        attackQueue.Enqueue(Attack.Bite);
        NextAttack();
    }

    private void NextAttack() {
        ClearAttackMarkers();
        if (attackQueue.Count == 0) {
            StartRest();
            return;
        }
        attack = attackQueue.Dequeue();
        attackTimeS = 0f;
        lockAngle = AngleOf(monkey.position);
        lockY = monkey.position.y;
        switch (attack) {
            case Attack.Bite:
                StartBite();
                break;
            case Attack.Sweep:
                StartSweep();
                break;
            case Attack.Minions:
                StartMinions();
                break;
        }
    }

    private void UpdateAttack() {
        attackTimeS += Time.deltaTime;
        switch (attack) {
            case Attack.Bite:
                UpdateBite();
                break;
            case Attack.Sweep:
                UpdateSweep();
                break;
            case Attack.Minions:
                UpdateMinions();
                break;
        }
    }

    // easy mode warns a notch longer: its last third as normal's start
    private float WarnScale => isEasy
        ? (Third >= 2 ? 1f : easyWarnScale)
        : (Third >= 2 ? finalWarnScale : 1f);

    private int MinionWaves => isEasy ? easyMinionWaves : minionWaves;

    // Bite: a red disc marks the spot, the head dives at it
    private void StartBite() {
        biteSpot = PointOnTrunk(lockAngle, lockY, MonkeyRadius());
        AddTrunkZone(lockAngle, lockY, biteRadiusU * 2f, "BITE!", true);
        biteStarted = false;
    }

    // the strike starts once the head is free, its wind-up filling the rest
    // of the warning; the attack ends once the head is back at rest
    private void UpdateBite() {
        UpdateMarkers(
            attackTimeS / Mathf.Max(0.01f, biteWarnS * WarnScale), false, false
        );
        if (!biteStarted) {
            if (body.IsStriking) {
                return;
            }
            biteStarted = true;
            float windUp = Mathf.Max(
                0.3f,
                biteWarnS * WarnScale - attackTimeS
            );
            Vector3 spot = biteSpot;
            body.StartStrike(spot, windUp, () => {
                if ((monkey.position - spot).magnitude < biteRadiusU) {
                    LethalHit();
                }
            });
            return;
        }
        if (!body.IsStriking) {
            biteStarted = false;
            NextAttack();
        }
    }

    // Sweep: a red band fills round the trunk at the height, then the tail
    // circles it
    private void StartSweep() {
        GameObject band = CreatePrimitive(
            PrimitiveType.Cylinder, "SweepBand",
            new Vector3(
                (trunkRadiusU + 0.08f) * 2f,
                0.01f,
                (trunkRadiusU + 0.08f) * 2f
            ),
            warningColor
        );
        band.transform.position = new Vector3(
            trunkCenter.x, lockY, trunkCenter.z
        );
        int warning = hud.AddWarning(SweepSignPoint(), "TAIL!");
        markers.Add(new Marker {
            fill = band.transform,
            warning = warning,
            isBand = true
        });
        tailHit = false;
    }

    // the band's sign follows the monkey round the trunk, so it stays in view
    private Vector3 SweepSignPoint() {
        return PointOnTrunk(
            AngleOf(monkey.position), lockY + sweepBandHalfU, MonkeyRadius()
        );
    }

    private void UpdateSweep() {
        float warn = sweepWarnS * WarnScale;
        if (attackTimeS < warn) {
            UpdateMarkers(attackTimeS / warn, true, false);
            return;
        }
        ClearAttackMarkers();
        if (tail == null) {
            tail = new Transform[tailBeadCount];
            for (int i = 0; i < tailBeadCount; i++) {
                tail[i] = CreatePrimitive(
                    PrimitiveType.Sphere, "Tail" + i,
                    Vector3.one * Mathf.Lerp(1.6f, 0.8f,
                        i / Mathf.Max(1f, tailBeadCount - 1f)),
                    tailColor
                ).transform;
            }
        }

        // starts across the trunk & loops round once
        float t = (attackTimeS - warn) / sweepTimeS;
        float headAngle = lockAngle + 180f + t * 360f;
        for (int i = 0; i < tail.Length; i++) {
            tail[i].position = PointOnTrunk(
                headAngle - i * 9f, lockY, trunkRadiusU + 1.1f
            );
        }

        float gap = Mathf.Abs(Mathf.DeltaAngle(
            headAngle, AngleOf(monkey.position)
        ));
        if (!tailHit && gap < 14f
            && Mathf.Abs(monkey.position.y - lockY) < sweepBandHalfU) {
            tailHit = true;
            LethalHit();
        }

        if (t >= 1f) {
            foreach (Transform bead in tail) {
                Destroy(bead.gameObject);
            }
            tail = null;
            NextAttack();
        }
    }

    // Minions: waves of small snakes fall one after another across the
    // monkey's side of the trunk, each wave leaving one gap to dodge into
    private void StartMinions() {
        // the king calls its minions once a game, the first time they come
        if (!hasCalledMinions) {
            hasCalledMinions = true;
            SayNow(minionCallLine, LinePriority.Key);
        }
        waveIndex = 0;
        StartWave();
    }


    private void StartWave() {
        ClearStripMarkers();
        minions.Clear();
        waveTimeS = 0f;
        isWaveRunning = true;
        // its own lock, so a bite or sweep running alongside keeps theirs
        minionLockAngle = AngleOf(monkey.position);
        minionLockY = monkey.position.y;

        // a gap never at either end, & the sweep runs either way
        int gap = Random.Range(1, Mathf.Max(2, minionCount - 1));
        float direction = Random.value < 0.5f ? -1f : 1f;
        float warn = minionWarnS * WarnScale;
        int signAt = minionCount / 2 == gap ? gap + 1 : minionCount / 2;
        int order = 0;
        for (int i = 0; i < minionCount; i++) {
            float angle = minionLockAngle
                + direction * (i - (minionCount - 1) * 0.5f) * minionSpreadDeg;
            bool isGap = i == gap;
            if (!isGap) {
                // the same filling danger zone as the first version; one
                // LOOK UP! sign for the whole wave, on a middle zone
                // the strip is the minion's whole fall: from the coil it
                // drops out of, to where it is gone
                AddTrunkStrip(
                    angle,
                    coilBottomY,
                    MinionEndY,
                    minionHitRadiusU * 1.4f,
                    i == signAt ? "LOOK UP!" : null
                );
            }
            minions.Add(new Minion {
                angle = angle,
                root = null,
                isDone = isGap,
                dropDelayS = warn + order * minionStaggerS
            });
            if (!isGap) {
                order++;
            }
        }
    }



    // normal mode's last third: minion waves stream on & on, alongside the
    // usual rounds of bites & sweeps, until the king or the monkey falls;
    // easy mode keeps minions as one attack of its rounds
    private bool IsMinionFinale => !isEasy && Third >= 2;

    private void UpdateMinionStream() {
        if (!isMinionStreaming) {
            if (!IsMinionFinale || loop == Loop.Intro) {
                return;
            }
            isMinionStreaming = true;
            if (!hasCalledMinions) {
                hasCalledMinions = true;
                SayNow(minionCallLine, LinePriority.Key);
            }
            StartWave();
            return;
        }
        if (isWaveRunning) {
            UpdateMinions();
        } else if (
            Time.time >= nextWaveTime
            && AttackDangerLeftS() <= finaleOverlapS
        ) {
            // likewise a wave waits for a bite or sweep to near its end
            StartWave();
        }
    }

    // time left till a running bite or sweep has struck; s
    private float AttackDangerLeftS() {
        if (loop != Loop.Attack) {
            return 0f;
        }
        switch (attack) {
            case Attack.Bite:
                // the warning, then a short dive
                return Mathf.Max(0f, biteWarnS * WarnScale - attackTimeS)
                    + 0.3f;
            case Attack.Sweep:
                return Mathf.Max(
                    0f, sweepWarnS * WarnScale + sweepTimeS - attackTimeS
                );
            default:
                return 0f;
        }
    }

    // time left till the running wave's last minion has fallen past the
    // monkey's height; s
    private float WaveDangerLeftS() {
        if (!isWaveRunning) {
            return 0f;
        }
        float lastDropS = minionWarnS * WarnScale
            + Mathf.Max(0, minionCount - 2) * minionStaggerS;
        float fallS = Mathf.Max(0f, coilBottomY - minionLockY)
            / Mathf.Max(0.1f, minionFallSpeedU);
        return Mathf.Max(0f, lastDropS + fallS + 0.3f - waveTimeS);
    }

    // world y a falling minion is gone at, the bottom of its fall line
    private float MinionEndY => entryY - guardDepthU;

    private void UpdateMinions() {
        waveTimeS += Time.deltaTime;
        float warn = minionWarnS * WarnScale;
        if (waveTimeS < warn) {
            UpdateMarkers(waveTimeS / warn, false, true);
        }
        bool isAnyLeft = false;
        for (int i = 0; i < minions.Count; i++) {
            Minion minion = minions[i];
            if (minion.isDone) {
                continue;
            }
            isAnyLeft = true;
            if (waveTimeS < minion.dropDelayS) {
                continue;
            }
            if (minion.root == null) {
                minion.root = CreateMinion();
                minion.root.position = PointOnTrunk(
                    minion.angle, coilBottomY, MonkeyRadius()
                );
            }
            minion.root.position += Vector3.down
                * minionFallSpeedU * Time.deltaTime;
            bool hasHit = (minion.root.position - monkey.position).magnitude
                < minionHitRadiusU;
            if (hasHit) {
                // a minion is a snake too: its touch is fatal, unless the
                // gold shield takes it
                LethalHit();
            }
            if (hasHit || minion.root.position.y < MinionEndY) {
                Destroy(minion.root.gameObject);
                minion.root = null;
                minion.isDone = true;
            }
            minions[i] = minion;
        }
        if (isAnyLeft) {
            return;
        }
        isWaveRunning = false;
        ClearStripMarkers();
        // the finale's stream starts its next wave after a short gap
        if (isMinionStreaming) {
            nextWaveTime = Time.time + minionStreamGapS;
            return;
        }
        waveIndex++;
        if (waveIndex < MinionWaves) {
            StartWave();
        } else {
            NextAttack();
        }
    }

    // a small snake: three green beads in a column
    private Transform CreateMinion() {
        Transform root = new GameObject("Minion").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < 3; i++) {
            Transform bead = CreatePrimitive(
                PrimitiveType.Sphere, "Bead" + i,
                Vector3.one * (0.7f - i * 0.12f), minionColor
            ).transform;
            bead.SetParent(root, false);
            bead.localPosition = Vector3.up * i * 0.55f;
        }
        return root;
    }

    // a bite, a sweep or a minion: fatal, unless the gold shield takes it
    private void LethalHit() {
        // nor on the way back to the start, while it cannot dodge
        if (phase != Phase.Fighting || isReturningToStart) {
            return;
        }
        if (hasShield) {
            hasShield = false;
            hud.PopText("Saved!", monkey.position, goldenColor);
            KnockDown(body.HeadCollider);
            return;
        }
        StartFinisher(false);
    }

    // Finisher  ---------------------------------------------------------------
    // the last blow either way plays in slow motion: a win flashes the
    // screen white before the king shatters; a loss flashes red & spins the
    // monkey off the tree under a K.O. banner
    private void StartFinisher(bool isWin) {
        phase = Phase.Finishing;
        isFinisherWin = isWin;
        finisherTimeS = 0f;
        ClearMarkers();
        hud.SetPrompt(false, Vector3.zero);
        Time.timeScale = finisherTimeScale;

        if (isWin) {
            hud.Flash(Color.white);
            return;
        }
        hud.Flash(new Color(0.9f, 0.1f, 0.1f));
        hud.ShowBanner("K.O.", new Color(0.95f, 0.15f, 0.15f));
        // from the K.O. till a revive lands, no web, cobra or branch may
        // touch the monkey: the same untouchable state as a pineapple leap
        SetUntouchable(true);
        if (struggle != null) {
            struggle.ForceRelease();
        }
        if (monkeyController != null) {
            monkeyController.ClearHeightCeiling();
            Vector3 outward = monkey.position - trunkCenter;
            outward.y = 0f;
            monkeyController.IsHeld = true;
            monkeyController.PullTo(
                monkey.position + outward.normalized * 6f + Vector3.down * 3f
            );
        }
    }

    // runs on unscaled time, since the game itself crawls meanwhile
    private void UpdateFinisher() {
        finisherTimeS += Time.unscaledDeltaTime;
        if (!isFinisherWin) {
            monkey.Rotate(Vector3.up, 900f * Time.unscaledDeltaTime);
        }
        if (finisherTimeS < finisherRealS) {
            return;
        }

        Time.timeScale = 1f;
        if (isFinisherWin) {
            Defeat();
            return;
        }
        if (canRevive) {
            OfferRevive();
            return;
        }
        EndAsLoss();
    }

    private void EndAsLoss() {
        phase = Phase.Over;
        SetUntouchable(false);
        if (GameController.I != null) {
            GameController.I.LoseGame();
        }
    }

    // Revive  -----------------------------------------------------------------
    // once a game a loss stops on an offer: watch a (mock) ad to fight on.
    // Time stands still meanwhile, so all of it runs on unscaled time
    private void OfferRevive() {
        phase = Phase.ReviveOffer;
        reviveLeftS = reviveOfferS;
        Time.timeScale = 0f;
        hud.HideBanner();
        hud.ShowReviveOffer();
        hud.SetReviveCountdown(Mathf.CeilToInt(reviveLeftS));
    }

    private void UpdateRevive(GameController game) {
        // a pause & resume would run the game under the offer otherwise
        Time.timeScale = 0f;
        // the clock holds while paused; the step is capped so a long time
        // away, eg on the course page, skips nothing. Input is read even
        // without focus, as coming back from the page may leave the game
        // unfocused till a click
        if (game.IsPaused) {
            return;
        }
        float stepS = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        // the ad runs till skipped: no countdown, only a Skip button after
        // a short while
        if (phase == Phase.WatchingAd) {
            adElapsedS += stepS;
            bool canSkip = adElapsedS >= adSkipAfterS;
            hud.SetAdSkippable(canSkip);
            if (canSkip && WasPressed(confirmAction)) {
                SkipAd();
            }
            return;
        }

        reviveLeftS -= stepS;

        hud.SetReviveCountdown(Mathf.CeilToInt(Mathf.Max(0f, reviveLeftS)));
        if (WasPressed(confirmAction)) {
            WatchAd();
        } else if (WasPressed(giveUpAction) || reviveLeftS <= 0f) {
            GiveUp();
        }
    }

    private void WatchAd() {
        if (phase != Phase.ReviveOffer) {
            return;
        }
        phase = Phase.WatchingAd;
        adElapsedS = 0f;
        hud.SetAdSkippable(false);
        hud.ShowAd();
    }

    private void GiveUp() {
        if (phase != Phase.ReviveOffer) {
            return;
        }
        hud.HideRevive();
        Time.timeScale = 1f;
        EndAsLoss();
    }

    // after the ad the fight starts over: the monkey flies back to where it
    // began, in the armor, full stamina, a full-health king & the mode
    // choice again
    private void RestartFight() {
        hud.HideRevive();
        Time.timeScale = 1f;
        phase = Phase.Fighting;
        Revived?.Invoke();

        body.CancelStrike();
        body.SetWeakSpotLit(true);
        ClearAttackObjects();
        ClearMarkers();
        attackQueue.Clear();
        ClearFruits();
        foreach (SpiderWebTrap web in webs) {
            if (web != null) {
                Destroy(web.gameObject);
            }
        }
        webs.Clear();

        loop = Loop.Intro;
        isMinionStreaming = false;
        isWaveRunning = false;
        isFinaleSweepNext = false;
        hasHit = false;
        hasThrown = false;
        // a new try, so the minion call is heard again
        hasCalledMinions = false;
        isEasy = false;
        maxHealth = worldBalanceConfig.BossMaxHealth;
        health = maxHealth;
        hud.SetHealth(1f);
        hasShield = true;
        StaminaBarController stamina = StaminaBarController.I;
        if (stamina != null) {
            stamina.AddStamina(stamina.MaxStamina);
        }

        // the guard below ends any peek, so it is not up on the way back
        if (greatCobra != null) {
            greatCobra.KnockBack(0f);
        }
        if (struggle != null) {
            struggle.ForceRelease();
        }
        SetUntouchable(true);
        monkey.rotation = entryRotation;
        if (monkeyController != null) {
            monkeyController.ClearStun();
            monkeyController.IsHeld = true;
            monkeyController.PullTo(entryPosition, reviveReturnSpeedU);
            isReturningToStart = true;
        }
        SpawnChoices();
        SpawnWebs(webCount);
        SpawnLowWebs();
        hud.Flash(goldenColor);
        hud.ShowBannerFor(armorIntroTitle, ChoiceLine, goldenColor, 0f);
        if (Debug.isDebugBuild) {
            Debug.Log(
                "BossFightController:\trevived after the ad, fight restarts",
                this
            );
        }
    }

    private void SkipAd() {
        if (phase != Phase.WatchingAd
            || adElapsedS < adSkipAfterS) {
            return;
        }
        RestartFight();
    }

    private void OpenCoursePage() {
        if (phase == Phase.WatchingAd && !string.IsNullOrEmpty(courseUrl)) {
            Application.OpenURL(courseUrl);
        }
    }

    // flies the monkey back to where the fight began, untouchable & out of
    // any web on the way, as after a revive
    private void ReturnToStart() {
        if (monkeyController == null) {
            return;
        }
        if (struggle != null) {
            struggle.ForceRelease();
        }
        SetUntouchable(true);
        monkeyController.ClearStun();
        monkeyController.IsHeld = true;
        monkeyController.PullTo(entryPosition, reviveReturnSpeedU);
        isReturningToStart = true;
    }

    // lets go of the monkey once it is back at the fight's start
    private void UpdateReturnToStart() {
        if (!isReturningToStart || monkeyController == null) {
            return;
        }
        if (
            monkeyController.HasReachedPullTarget
            || (monkey.position - entryPosition).sqrMagnitude < 0.01f
        ) {
            isReturningToStart = false;
            // back on the ring at the fight's start, not where it fell
            monkeyController.SyncOrbitAngle();
            monkeyController.IsHeld = false;
            SetUntouchable(false);
        }
    }

    private static void SetUntouchable(bool isUntouchable) {
        if (GameController.I != null) {
            GameController.I.SetLeaping(isUntouchable);
        }
    }

    // the usual stun & drop w/ the heavier shake, never fatal
    private static void KnockDown(Collider source) {
        HitBranchSensor sensor = HitBranchSensor.I;
        if (sensor != null && source != null) {
            sensor.ReportHit(
                source,
                HitBranchSensor.HitKind.FallingCobraController
            );
        }
    }

    // the climb is capped below the king, & a monkey at the cap is butted
    // down; too low, the king taunts
    private void UpdateWarnings() {
        float ceilingY = body.LowestY - ceilingGapU;
        if (monkeyController != null) {
            monkeyController.SetHeightCeiling(ceilingY);
        }
        float y = monkey.position.y;
        if (y < entryY - retreatLimitU) {
            Say(retreatLine);
        } else if (y > ceilingY - pushRangeU) {
            StartPush();
        }
    }

    // a striking head reaches far below the rest pose, so its touch is no
    // sign of a monkey too close; only the rest pose's range counts then
    private void OnKingTouched() {
        if (phase == Phase.Fighting && !body.IsStriking) {
            StartPush();
        }
    }

    // any time the monkey gets too close the king shoves it off & says so:
    // a head butt when its head is free, else at once
    private void StartPush() {
        if (Time.time < nextPushTime) {
            return;
        }
        nextPushTime = Time.time + pushCooldownS;
        SayNow(approachLine, LinePriority.Urgent);
        if (body.IsStriking) {
            KnockDown(body.HeadCollider);
            return;
        }
        body.StartStrike(monkey.position, pushWindUpS, () => {
            if (monkey.position.y > body.LowestY - pushRangeU - 1.5f) {
                KnockDown(body.HeadCollider);
            }
        });
    }

    // Golden Banana  ----------------------------------------------------------
    private void TakeGoldenBanana() {
        StaminaBarController stamina = StaminaBarController.I;
        if (stamina != null) {
            stamina.AddStamina(stamina.MaxStamina);
        }
        hasShield = true;
        hud.PopText("Shield!", monkey.position, goldenColor);
    }

    // the armor follows the monkey; its back faces out from the trunk
    private void UpdateShieldRing() {
        if (armor == null) {
            return;
        }
        Vector3 outward = monkey.position - trunkCenter;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.001f ? outward.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        float shine = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        Color gold = Color.Lerp(goldenColor, Color.white, shine * 0.35f);
        float rewardT = UpdateArmorReward();

        Vector3[] positions = {
            monkey.position + outward * armorOutU + Vector3.up * 0.15f,
            monkey.position + outward * (armorOutU * 0.6f) - side * 0.55f
                + Vector3.up * 0.6f,
            monkey.position + outward * (armorOutU * 0.6f) + side * 0.55f
                + Vector3.up * 0.6f,
            monkey.position + Vector3.up * armorHelmetU,
        };
        for (int i = 0; i < armor.Length; i++) {
            armor[i].gameObject.SetActive(hasShield);
            if (!hasShield) {
                continue;
            }
            Vector3 position = positions[i];
            float scale = 1f;
            Color color = gold;
            if (rewardT >= 0f) {
                // swirl: each piece spirals in from its own side, growing
                // smaller as it nears, then all pulse bright once it lands
                float fly = Mathf.Clamp01(rewardT / ARMOR_LAND_SHARE);
                float ease = 1f - Mathf.Pow(1f - fly, 3f);
                float radius = Mathf.Lerp(armorRewardRadiusU, 0f, ease);
                float spin = (i * 90f + fly * 540f) * Mathf.Deg2Rad;
                position += (outward * Mathf.Cos(spin) + side * Mathf.Sin(spin))
                    * radius + Vector3.up * radius * 0.7f;
                if (rewardT < ARMOR_LAND_SHARE) {
                    scale = Mathf.Lerp(1.8f, 1f, ease);
                } else {
                    float pulse = Mathf.Sin(
                        (rewardT - ARMOR_LAND_SHARE) / (1f - ARMOR_LAND_SHARE)
                            * Mathf.PI
                    );
                    scale = 1f + 0.35f * pulse;
                    color = Color.Lerp(gold, Color.white, pulse * 0.8f);
                }
            }
            armor[i].position = position;
            armor[i].localScale = armorBaseScales[i] * scale;
            armor[i].rotation = i == 0
                ? Quaternion.FromToRotation(Vector3.up, outward)
                : Quaternion.LookRotation(outward, Vector3.up);
            SetColor(armor[i].GetComponent<Renderer>(), color);
        }
    }

    // the win reward's swirl, 0~1 while it plays, else -1; on landing a
    // gold burst & flash, & the armor badge that stays while it is kept
    private float UpdateArmorReward() {
        if (armorRewardTimeS < 0f) {
            return -1f;
        }
        armorRewardTimeS += Time.deltaTime;
        float t = Mathf.Clamp01(armorRewardTimeS / armorRewardS);
        if (!hasArmorLanded && t >= ARMOR_LAND_SHARE) {
            hasArmorLanded = true;
            BurstAt(monkey.position + Vector3.up * 0.4f, goldenColor);
            hud.Flash(goldenColor);
            hud.ShowArmorBadge(true);
        }
        if (t >= 1f) {
            armorRewardTimeS = -1f;
        }
        return t;
    }

    // thrown bananas follow an arc onto the crown, then burst on impact
    private void UpdateProjectiles() {
        Vector3 target = body.WeakSpotPosition;
        for (int i = projectiles.Count - 1; i >= 0; i--) {
            Projectile thrown = projectiles[i];
            if (thrown.transform == null) {
                projectiles.RemoveAt(i);
                continue;
            }
            thrown.ageS += Time.deltaTime;
            float t = Mathf.Clamp01(thrown.ageS / throwTimeS);
            Vector3 position = Vector3.Lerp(thrown.start, target, t)
                + Vector3.up * Mathf.Sin(t * Mathf.PI) * throwArcU;
            thrown.transform.position = position;
            thrown.transform.Rotate(Vector3.forward, 900f * Time.deltaTime);

            // a dot every few hundredths of a second marks the path
            if (thrown.ageS >= thrown.nextTrailS) {
                thrown.nextTrailS = thrown.ageS + 0.035f;
                AddTrailDot(position);
            }
            projectiles[i] = thrown;
            if (t < 1f) {
                continue;
            }
            Destroy(thrown.transform.gameObject);
            projectiles.RemoveAt(i);
            BurstAt(target, trailColor);
            OnJewelHit();
        }
        UpdateSparks();
    }

    private void AddTrailDot(Vector3 position) {
        Transform dot = CreatePrimitive(
            PrimitiveType.Sphere, "TrailDot", Vector3.one * 0.22f, trailColor
        ).transform;
        dot.position = position;
        sparks.Add(new Spark {
            transform = dot,
            velocity = Vector3.zero,
            size = 0.22f,
            ageS = 0f,
            lifeS = 0.35f
        });
    }

    // cubes fly out, eg where a banana lands or the reward armor settles
    private void BurstAt(Vector3 position, Color color) {
        for (int i = 0; i < 10; i++) {
            Transform cube = CreatePrimitive(
                PrimitiveType.Cube, "Burst", Vector3.one * 0.25f, color
            ).transform;
            cube.position = position;
            cube.rotation = Random.rotation;
            sparks.Add(new Spark {
                transform = cube,
                velocity = Random.insideUnitSphere * 7f + Vector3.up * 2f,
                size = 0.25f,
                ageS = 0f,
                lifeS = 0.6f
            });
        }
    }

    private void UpdateSparks() {
        for (int i = sparks.Count - 1; i >= 0; i--) {
            Spark spark = sparks[i];
            spark.ageS += Time.deltaTime;
            if (spark.transform == null || spark.ageS >= spark.lifeS) {
                if (spark.transform != null) {
                    Destroy(spark.transform.gameObject);
                }
                sparks.RemoveAt(i);
                continue;
            }
            spark.velocity += Physics.gravity * 0.5f * Time.deltaTime;
            spark.transform.position += spark.velocity * Time.deltaTime;
            spark.transform.localScale = Vector3.one * spark.size
                * (1f - spark.ageS / spark.lifeS);
            sparks[i] = spark;
        }
    }

    // a banana on the crown always hurts
    private void OnJewelHit() {
        if (phase != Phase.Fighting) {
            return;
        }

        int lastThird = Third;
        int damage = worldBalanceConfig.BossBananaDamage;
        health = Mathf.Max(0, health - damage);
        body.FlashHit();
        hud.SetHealth(health / (float)maxHealth);
        hud.PopDamage("-" + damage, body.WeakSpotPosition);

        if (health <= 0) {
            StartFinisher(true);
            return;
        }
        if (!hasHit) {
            hasHit = true;
            Say(firstHitLine);
            nextGoldenTime = Time.time + goldenFirstDelayS;
            StartRest();
        } else if (Third > lastThird) {
            // each lost third: more bananas to throw & more webs
            AddBananas(bananasPerThird);
            SpawnWebs(websPerThird);
        }
    }

    /// <returns>share of its health the king has lost, 0~1</returns>
    private float LostShare => maxHealth > 0
        ? 1f - health / (float)maxHealth
        : 0f;

    /// <returns>how many thirds of its health the king has lost: 0, 1, 2
    /// </returns>
    private int Third {
        get {
            float lost = 1f - health / (float)maxHealth;
            return Mathf.Clamp(Mathf.FloorToInt(lost * 3f), 0, 2);
        }
    }

    // Victory  ----------------------------------------------------------------
    private void Defeat() {
        phase = Phase.Defeated;
        ProgressStore.MarkBossBeaten();
        ClearMarkers();
        ClearAttackObjects();
        RemoveArenaFruit();
        if (monkeyController != null) {
            monkeyController.ClearHeightCeiling();
        }
        hud.SetPrompt(false, Vector3.zero);
        hud.SetHealth(0f);

        crown = body.Shatter();
        if (crown != null) {
            crownStart = crown.position;
            crownStartScale = crown.localScale;
        }
        victoryTimeS = 0f;
        if (Debug.isDebugBuild) {
            Debug.Log("BossFightController:\tCobra King defeated", this);
        }
    }

    // the crown flies in an arc onto the monkey's head, then the run is won
    private void UpdateVictory() {
        victoryTimeS += Time.deltaTime;
        if (crown != null) {
            float t = Mathf.Clamp01(victoryTimeS / crownFlyS);
            Vector3 end = monkey.position + Vector3.up * crownAboveMonkeyU;
            Vector3 position = Vector3.Lerp(crownStart, end, t)
                + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2.5f;
            crown.position = position;
            crown.localScale = crownStartScale
                * Mathf.Lerp(1.4f, crownScaleOnMonkey, t);
            crown.rotation = Quaternion.Euler(0f, t * 720f, 0f);
            if (t >= 1f) {
                crown.SetParent(monkey, true);
            }
        }
        if (!hasWon && victoryTimeS >= crownFlyS + victoryDelayS) {
            hasWon = true;
            FinishWithReward();
        }
    }

    private void UpdateChaseWarning() {
        if (hasWarnedChase || Time.time < chaseWarnTime) {
            return;
        }
        hasWarnedChase = true;
        hud.ShowBannerFor(
            "The cobra is coming!", "Climb!",
            new Color(0.95f, 0.25f, 0.2f), 2.5f
        );
    }

    // the run goes on: the gold armor is the reward, the hazards & the great
    // cobra's chase come back, & the climb is free again
    private void FinishWithReward() {
        phase = Phase.Rewarding;
        hasShield = true;
        isRewardPanelShown = false;
        // the armor swirls in onto the monkey, then a badge keeps it in view
        // & the game pauses on the reward till the player goes on
        armorRewardTimeS = 0f;
        hasArmorLanded = false;
        hud.HideBossBar();
        hud.SetPrompt(false, Vector3.zero);
    }

    // once the swirl is done, time stops on the reward panel; the confirm
    // action or its button goes on
    private void UpdateRewardPause(GameController game) {
        if (!isRewardPanelShown) {
            if (armorRewardTimeS >= 0f) {
                return;
            }
            isRewardPanelShown = true;
            hud.ShowRewardPanel(winTitle, winLine, winDetailLine);
        }
        // a pause & resume would run the game under the panel otherwise
        Time.timeScale = 0f;
        if (game.IsPaused) {
            return;
        }
        if (WasPressed(confirmAction)) {
            ContinueAfterReward();
        }
    }

    // if the action is wired and went down this frame; the action is shared
    // (Player/Jump is also switched off by SpiderWebStruggle), so it is
    // switched back on here at the moment of use
    private static bool WasPressed(InputActionReference reference) {
        if (reference == null) {
            return false;
        }

        InputAction action = reference.action;
        if (!action.enabled) {
            action.Enable();
        }
        return action.WasPressedThisFrame();
    }

    private void ContinueAfterReward() {
        if (phase != Phase.Rewarding || !isRewardPanelShown) {
            return;
        }
        hud.HideRewardPanel();
        Time.timeScale = 1f;
        phase = Phase.Done;
        FightEnded?.Invoke();

        TreeSegmentController.BranchesGenerated -= OnBranchesGenerated;
        // a head start: the cobra chases again from far down the trunk,
        // called out a moment after
        if (greatCobra != null) {
            greatCobra.StopGuard();
            greatCobra.KnockBack(winCobraDropU);
        }
        chaseWarnTime = Time.time + 1f;
        hasWarnedChase = false;
        if (monkeyController != null) {
            monkeyController.ClearHeightCeiling();
        }
        if (struggle != null) {
            struggle.EscapeGainScale = 1f;
        }
        if (Debug.isDebugBuild) {
            Debug.Log(
                "BossFightController:\twon, armor given, the climb goes on",
                this
            );
        }
    }

    // Hint, Fruits, Camera  ---------------------------------------------------
    private void UpdateFirstThrowHint() {
        // until a mode is picked: the key prompt between the two choices,
        // the dotted line from there to the crown
        bool isShown;
        Vector3 from;
        if (isChoosing && choices.Count == 2) {
            isShown = choices[0].root != null && choices[1].root != null;
            from = isShown
                ? (choices[0].root.transform.position
                    + choices[1].root.transform.position) * 0.5f
                : Vector3.zero;
        } else {
            ArenaFruit first = fruits.Count > 0 ? fruits[0] : default;
            isShown = !hasHit && first.root != null && first.root.activeSelf;
            from = first.root != null
                ? first.root.transform.position
                : Vector3.zero;
        }
        hud.SetPrompt(isShown, from);
        if (dots == null) {
            return;
        }

        Vector3 to = body.WeakSpotPosition;
        Vector3 outward = from - trunkCenter;
        outward.y = 0f;
        Vector3 control = (from + to) * 0.5f + outward.normalized * 3f;
        float wave = Time.time * 1.5f;
        for (int i = 0; i < dots.Length; i++) {
            dots[i].gameObject.SetActive(isShown);
            if (!isShown) {
                continue;
            }
            float t = (i + 1f) / (dots.Length + 1f);
            float u = 1f - t;
            dots[i].position = u * u * from + 2f * u * t * control + t * t * to;
            // a pulse runs along the line toward the king
            float pulse = Mathf.Repeat(t - wave, 1f);
            dots[i].localScale = Vector3.one * dotRadiusU * 2f
                * Mathf.Lerp(1.6f, 0.8f, pulse);
        }
    }

    // EASY & NORMAL tags over the two choice bananas
    private void UpdateChoiceTags() {
        if (!isChoosing || choices.Count < 2) {
            return;
        }
        hud.SetChoiceTags(
            true,
            choices[0].root.transform.position,
            choices[1].root.transform.position
        );
    }

    // a taken banana grows back on its branch
    // the arena's watermelons belong to the fight: once it is won they go,
    // their branches left bare, so none is eaten like a banana after
    private void RemoveArenaFruit() {
        isArenaFruitGone = true;
        foreach (ArenaFruit fruit in fruits) {
            if (fruit.root != null) {
                ForgetFruits(fruit.root);
                fruit.root.SetActive(false);
            }
        }
        foreach (GameObject melon in arenaBranchFruits) {
            if (melon != null) {
                ForgetFruits(melon);
                melon.SetActive(false);
            }
        }
    }

    private void UpdateFruits() {
        if (isArenaFruitGone) {
            return;
        }
        for (int i = 0; i < fruits.Count; i++) {
            ArenaFruit fruit = fruits[i];
            if (fruit.isGolden || fruit.root.activeSelf
                || fruit.respawnTime < 0f || Time.time < fruit.respawnTime) {
                continue;
            }
            fruit.root.SetActive(true);
            fruit.respawnTime = -1f;
            fruits[i] = fruit;
        }
    }

    // the camera aims up between the monkey & the king, so both stay in view
    private void UpdateCameraFocus() {
        if (cameraFraming == null) {
            return;
        }
        bool isFighting = phase == Phase.Fighting
            || phase == Phase.Finishing;
        float focus = isFighting
            ? Mathf.Clamp(
                (body.HeadPosition.y - monkey.position.y) * cameraFocusShare,
                0f,
                maxCameraFocusU
            )
            : 0f;
        // pulled back through the fight & the crown's flight
        cameraFraming.SetBossFraming(
            focus,
            phase != Phase.Waiting && phase != Phase.Done
                ? cameraDistanceScale
                : 1f
        );
    }

    // Markers & Helpers  ------------------------------------------------------
    // a danger zone flat on the trunk, facing out: a dark red rim whose
    // bright red center fills it as the hit nears, w/ a hud sign if worded
    private void AddTrunkZone(
        float angle,
        float y,
        float size,
        string word,
        bool hasRimSign
    ) {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 outward = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        Quaternion facing = Quaternion.FromToRotation(Vector3.up, outward);

        GameObject rim = CreatePrimitive(
            PrimitiveType.Cylinder, "ZoneRim",
            new Vector3(size, 0.02f, size), warningDimColor
        );
        rim.transform.position = PointOnTrunk(angle, y, trunkRadiusU + 0.04f);
        rim.transform.rotation = facing;

        GameObject fill = CreatePrimitive(
            PrimitiveType.Cylinder, "ZoneFill",
            new Vector3(0.01f, 0.03f, 0.01f), warningColor
        );
        fill.transform.position = PointOnTrunk(angle, y, trunkRadiusU + 0.07f);
        fill.transform.rotation = facing;

        int warning = word != null
            ? hud.AddWarning(rim.transform.position, word)
            : -1;
        markers.Add(new Marker {
            rim = rim.transform,
            fill = fill.transform,
            size = size,
            warning = warning,
            isBand = false
        });
    }

    // a minion's fall line: a dark red strip down the trunk from the king's
    // coil, filling top-down in bright red as the drop nears
    private void AddTrunkStrip(
        float angle,
        float topY,
        float bottomY,
        float width,
        string word
    ) {
        float length = Mathf.Max(0.5f, topY - bottomY);
        float radians = angle * Mathf.Deg2Rad;
        Vector3 outward = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        Quaternion facing = Quaternion.LookRotation(outward, Vector3.up);

        GameObject rim = CreatePrimitive(
            PrimitiveType.Cube, "StripRim",
            new Vector3(width, length, 0.04f), warningDimColor
        );
        rim.transform.SetPositionAndRotation(
            PointOnTrunk(angle, topY - length * 0.5f, trunkRadiusU + 0.04f),
            facing
        );
        // the whole line is red from the start, it only beats faster
        GameObject fill = CreatePrimitive(
            PrimitiveType.Cube, "StripFill",
            new Vector3(width * 0.8f, length, 0.06f), warningColor
        );
        fill.transform.SetPositionAndRotation(
            PointOnTrunk(angle, topY - length * 0.5f, trunkRadiusU + 0.07f),
            facing
        );

        int warning = word != null
            ? hud.AddWarning(
                PointOnTrunk(angle, minionLockY + 2.5f, trunkRadiusU),
                word
            )
            : -1;
        markers.Add(new Marker {
            rim = rim.transform,
            fill = fill.transform,
            size = width,
            warning = warning,
            isBand = false,
            isStrip = true,
            topY = topY,
            length = length,
            angle = angle
        });
    }

    // fills the attack's zones (or the minion strips) to progress 0~1; the
    // band grows from its height
    private void UpdateMarkers(float progress, bool isSweep, bool isStrips) {
        progress = Mathf.Clamp01(progress);
        foreach (Marker marker in markers) {
            if (marker.fill == null || marker.isStrip != isStrips) {
                continue;
            }
            if (marker.isStrip) {
                // the full strip glows red, beating faster & brighter as
                // the minions near
                float beat = 0.5f + 0.5f * Mathf.Sin(
                    Time.time * Mathf.Lerp(6f, 22f, progress)
                );
                float glow = Mathf.Lerp(0.55f, 1f, Mathf.Max(beat, progress));
                SetColor(
                    marker.fill.GetComponent<Renderer>(),
                    Color.Lerp(warningDimColor, warningColor, glow)
                );
                marker.fill.localScale = new Vector3(
                    marker.size * Mathf.Lerp(0.8f, 0.95f, progress),
                    marker.length,
                    0.06f
                );
                if (marker.warning >= 0) {
                    hud.UpdateWarning(
                        marker.warning,
                        PointOnTrunk(
                            marker.angle,
                            minionLockY + 2.5f,
                            trunkRadiusU
                        ),
                        progress
                    );
                }
                continue;
            }
            if (marker.isBand) {
                marker.fill.localScale = new Vector3(
                    (trunkRadiusU + 0.08f) * 2f,
                    Mathf.Max(0.01f, progress * sweepBandHalfU),
                    (trunkRadiusU + 0.08f) * 2f
                );
            } else {
                float size = marker.size * Mathf.Max(0.02f, progress);
                marker.fill.localScale = new Vector3(size, 0.03f, size);
            }
            if (marker.warning >= 0) {
                Vector3 signAt = isSweep && marker.isBand
                    ? SweepSignPoint()
                    : marker.rim != null
                        ? marker.rim.position
                        : marker.fill.position;
                hud.UpdateWarning(marker.warning, signAt, progress);
            }
        }
    }

    // every marker & sign, eg for the finisher or a restart
    private void ClearMarkers() {
        foreach (Marker marker in markers) {
            DestroyMarker(marker);
        }
        markers.Clear();
        hud.ClearWarnings();
    }

    // a bite's or sweep's markers, leaving the minion strips
    private void ClearAttackMarkers() {
        ClearMarkersOfKind(false);
    }

    // the minion strips, leaving a bite's or sweep's markers
    private void ClearStripMarkers() {
        ClearMarkersOfKind(true);
    }

    private void ClearMarkersOfKind(bool isStrip) {
        for (int i = markers.Count - 1; i >= 0; i--) {
            if (markers[i].isStrip != isStrip) {
                continue;
            }
            DestroyMarker(markers[i]);
            markers.RemoveAt(i);
        }
    }

    private void DestroyMarker(Marker marker) {
        if (marker.rim != null) {
            Destroy(marker.rim.gameObject);
        }
        if (marker.fill != null) {
            Destroy(marker.fill.gameObject);
        }
        if (marker.warning >= 0) {
            hud.RemoveWarning(marker.warning);
        }
    }

    private void ClearAttackObjects() {
        if (tail != null) {
            foreach (Transform bead in tail) {
                Destroy(bead.gameObject);
            }
            tail = null;
        }
        foreach (Minion minion in minions) {
            if (minion.root != null) {
                Destroy(minion.root.gameObject);
            }
        }
        minions.Clear();
        foreach (Projectile thrown in projectiles) {
            if (thrown.transform != null) {
                Destroy(thrown.transform.gameObject);
            }
        }
        projectiles.Clear();
    }

    private void Say(string line) {
        if (line == lastLine && Time.time < nextSameLineTime) {
            return;
        }
        SayNow(line, LinePriority.Normal);
    }

    // a key line holds the bubble for a while, so no other line cuts it off
    // "Stay away from me!" comes first, then the minion call, then the rest;
    // a held line blocks lower ones, & a minion call cut short by the push
    // line is said again right after it
    private void SayNow(string line, LinePriority priority) {
        bool isHeld = Time.time < keyLineEndTime;
        if (isHeld && priority < heldPriority) {
            if (priority == LinePriority.Key) {
                pendingKeyLine = line;
            }
            return;
        }
        if (isHeld && heldPriority == LinePriority.Key
            && priority == LinePriority.Urgent) {
            pendingKeyLine = lastLine;
        }
        lastLine = line;
        nextSameLineTime = Time.time + speechCooldownS;
        heldPriority = priority;
        keyLineEndTime = priority > LinePriority.Normal
            ? Time.time + keyLineHoldS
            : 0f;
        hud.Say(line);
    }

    private void UpdatePendingLine() {
        if (pendingKeyLine == null || Time.time < keyLineEndTime) {
            return;
        }
        string line = pendingKeyLine;
        pendingKeyLine = null;
        SayNow(line, LinePriority.Key);
    }

    private enum LinePriority {
        Normal,
        Key, // the minion call
        Urgent, // "Stay away from me!"
    }

    // horizontal distance from the trunk axis to the monkey
    private float MonkeyRadius() {
        Vector3 offset = monkey.position - trunkCenter;
        offset.y = 0f;
        return offset.magnitude;
    }

    private float AngleOf(Vector3 position) {
        Vector3 offset = position - trunkCenter;
        return Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
    }

    private Vector3 PointOnTrunk(float angleDeg, float y, float radius) {
        float radians = angleDeg * Mathf.Deg2Rad;
        return new Vector3(
            trunkCenter.x + Mathf.Cos(radians) * radius,
            y,
            trunkCenter.z + Mathf.Sin(radians) * radius
        );
    }

    // a decor primitive in the king's lit material, no collider
    private GameObject CreatePrimitive(
        PrimitiveType type,
        string objectName,
        Vector3 scale,
        Color color
    ) {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = objectName;
        part.transform.SetParent(transform, true);
        part.transform.localScale = scale;
        DestroyImmediate(part.GetComponent<Collider>());
        Renderer partRenderer = part.GetComponent<Renderer>();
        if (body.PrimitiveMaterial != null) {
            partRenderer.sharedMaterial = body.PrimitiveMaterial;
        }
        SetColor(partRenderer, color);
        return part;
    }

    private void SetColor(Renderer target, Color color) {
        if (target == null) {
            return;
        }
        target.GetPropertyBlock(colorBlock);
        colorBlock.SetColor(BASE_COLOR, color);
        colorBlock.SetColor(COLOR, color);
        target.SetPropertyBlock(colorBlock);
    }

    // private members  ########################################################
    private const string FRUIT_TAG = "FruitCollider";
    // the banana hangs about this far below its branch root; u
    private const float BANANA_HANG_U = 1f;
    private static readonly int OUTLINE_COLOR =
        Shader.PropertyToID("_Outline_Color");
    private static readonly int OUTLINE_WIDTH =
        Shader.PropertyToID("_Outline_Width");
    private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
    private static readonly int COLOR = Shader.PropertyToID("_Color");

    // share of the win swirl spent flying in, the rest is the pulse
    private const float ARMOR_LAND_SHARE = 0.7f;

    private enum Phase {
        Waiting, // climbing to the trigger height
        Fighting,
        Finishing, // the last blow, in slow motion
        ReviveOffer, // time stopped, asking to watch an ad
        WatchingAd, // time stopped, the mock ad playing
        Defeated, // the king beaten, the crown on its way
        Over, // the monkey beaten
        Rewarding, // the armor reward, then a pause till the player goes on
        Done, // the king beaten & the climb going on, the armor kept
    }

    private enum Loop {
        Intro, // before the first hit: no attacks
        Rest, // a pause between attack rounds
        Attack, // a round of attacks
    }

    private enum Attack {
        Bite,
        Sweep,
        Minions,
    }

    private struct ArenaFruit {
        public GameObject root;
        public Collider trigger;
        public Renderer renderer;
        public bool isGolden;
        public GameObject branch; // the branch it grows on
        public float respawnTime; // Time.time it grows back, -1 if hanging
    }

    private struct Projectile {
        public Transform transform;
        public Vector3 start; // world point it was thrown from
        public float ageS;
        public float nextTrailS; // age the next trail dot drops at
    }

    private struct Spark {
        public Transform transform;
        public Vector3 velocity;
        public float size;
        public float ageS;
        public float lifeS;
    }

    private struct Marker {
        public Transform rim; // full-size dark disc, null for the band
        public Transform fill; // grows with the warning's progress
        public float size; // disc diameter; u
        public int warning; // hud warning id, -1 if none
        public bool isBand; // the sweep's band round the trunk
        public bool isStrip; // a minion's vertical fall line
        public float topY; // strip top, world y
        public float length; // strip length; u
        public float angle; // strip angle round the trunk; deg
    }

    private struct Minion {
        public float angle; // round the trunk; deg
        public Transform root; // null until it falls
        public bool isDone; // fallen, or the wave's gap
        public float dropDelayS; // wave time it starts to fall at
    }

    private Phase phase = Phase.Waiting;
    private Loop loop;
    private Attack attack;
    private readonly Queue<Attack> attackQueue = new Queue<Attack>();
    private float loopTimeS; // rest time left
    private float attackTimeS; // time into the running attack
    private float lockAngle; // monkey angle when the attack began; deg
    private float lockY; // monkey y when the attack began
    private float minionLockAngle; // monkey angle when the wave began; deg
    private float minionLockY; // monkey y when the wave began
    private bool isMinionStreaming; // normal finale: waves run on their own
    private bool isFinaleSweepNext; // the finale's next round is a sweep
    private bool isWaveRunning; // a minion wave is warning or falling
    private float nextWaveTime; // Time.time the stream's next wave starts
    private bool biteStarted;
    private int waveIndex; // minion wave under way
    private bool hasCalledMinions; // the minion call was said this game
    private LinePriority heldPriority; // of the line held in the bubble
    private string pendingKeyLine; // a key line to say once the bubble frees
    private float waveTimeS; // time into the minion wave
    private Vector3 biteSpot; // world point the running bite dives at
    private bool tailHit;
    private int health;
    private bool hasHit; // the monkey has landed a first hit
    private bool hasShield; // the golden banana's shield is up
    private bool isEndlessStart; // the endless run skips the fight
    private bool hasThrown; // the first banana has been thrown
    private bool isReturningToStart; // flying back to the fight's start
    private float reviveLeftS; // revive offer time left; unscaled s
    private float adElapsedS; // time into the mock ad; unscaled s
    private float cobraSaveGraceEndTime; // the armor's save ignores contacts
    private float chaseWarnTime; // Time.time the chase is called after a win
    private bool hasWarnedChase;
    private Vector3 entryPosition; // monkey's pose when the fight began
    private Quaternion entryRotation;
    private bool isChoosing; // waiting for the first throw to pick a mode
    private bool isEasy;
    private int maxHealth; // the king's full health in the chosen mode
    private readonly List<ArenaFruit> choices = new List<ArenaFruit>();
    private bool hasWon;
    private bool isFinisherWin;
    private float finisherTimeS; // real time into the finisher; s
    private readonly List<SpiderWebTrap> webs = new List<SpiderWebTrap>();
    private readonly List<Vector3> arenaBranchSpots = new List<Vector3>();
    private readonly List<GameObject> arenaBranchFruits = new List<GameObject>();
    private bool isArenaFruitGone; // the fight is won, its fruit taken away
    private int goldenIndex = -1; // the golden banana in the fruit list
    private float nextGoldenTime; // Time.time the next visit starts
    private float goldenHideTime; // Time.time the current visit ends
    private float victoryTimeS;
    private float entryY; // monkey's y when the fight began
    private float coilBottomY; // world y of the king's coil
    private Vector3 trunkCenter;
    private string lastLine;
    private float keyLineEndTime; // Time.time a key line stops holding
    private float nextSameLineTime;
    private float nextPushTime; // Time.time b4 which no new butt starts
    private Vector3 crownStart;
    private Vector3 crownStartScale;
    private readonly List<ArenaFruit> fruits = new List<ArenaFruit>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<Spark> sparks = new List<Spark>();
    private readonly List<Marker> markers = new List<Marker>();
    private readonly List<Minion> minions = new List<Minion>();
    private Transform[] tail;
    private Transform[] dots;
    private Transform[] armor;
    private Vector3[] armorBaseScales;
    private float armorRewardTimeS = -1f; // into the win swirl, -1 if none
    private bool hasArmorLanded; // the swirl's pieces have settled
    private bool isRewardPanelShown; // the win's pause panel is up

    // cached references  ------------------------------------------------------
    private BossKingBody body;
    private BossFightHud hud;
    private Transform monkey;
    private MonkeyController monkeyController;
    private SpiderWebStruggle struggle;
    private Transform crown;
    private CobraMotor greatCobra;
    private CameraClimbFraming cameraFraming;
    private MaterialPropertyBlock colorBlock;
}
