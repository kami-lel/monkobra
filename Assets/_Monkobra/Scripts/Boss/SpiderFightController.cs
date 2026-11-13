using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The second boss, the Spider King, high on the climb. At
/// <see cref="WorldBalanceConfig.SpiderTriggerHeightU"/> the spawners are
/// already paused (its zone is a boss zone too), the great cobra guards from
/// below & the Spider King crawls on the trunk beside the monkey.
///
/// The player wins by ripping its glowing silk knots, grabbed like fruit:
/// 1 damage each, 2 inside the red ring of its territory, where it attacks
/// faster & swipes w/ its front legs. A ripped knot near the spider is
/// re-spun, healing it 1, unless it is busy elsewhere; a far knot just grows
/// back. Attacks, each telegraphed by its eyes glowing red:
/// <list type="bullet">
/// <item>web shot at the monkey: a miss leaves a sticky web on the trunk</item>
/// <item>silk hook: drags the monkey in, tap Space or grab to break free
/// </item>
/// <item>cocoon: a trapped monkey is crawled to, & wrapped (fatal) on
/// arrival</item>
/// <item>leg swipe in its territory: a knock down, never fatal</item>
/// </list>
/// A grabbed banana is tossed round to the far side of the trunk: the
/// spider leaves to eat it a few seconds, taking its territory along.
/// The Cobra King's gold armor, if still worn, saves one fatal hit. A win
/// gives the silk gloves: webs break twice as fast for the rest of the run.
/// Built at runtime from primitives, w/ the <see cref="BossFightHud"/>.
/// </summary>
[RequireComponent(typeof(SpiderKingBody))]
[RequireComponent(typeof(BossFightHud))]
public class SpiderFightController: MonoBehaviour {
    // Public Members  #########################################################
    public static SpiderFightController I {
        get; private set;
    }

    /// <returns>if the fight is on, from its start till the reward is
    /// taken</returns>
    public bool IsInArena => phase != Phase.Waiting && phase != Phase.Done
        && phase != Phase.Over;

    /// <returns>if this fight sets the camera framing now, so the Cobra
    /// King's leftover armor logic leaves it alone</returns>
    public bool IsFraming => IsInArena;

    /// <summary>raised when the fight begins</summary>
    public event System.Action FightStarted;

    /// <summary>raised when the reward is taken & the climb goes on</summary>
    public event System.Action FightEnded;

    // Public Methods  #########################################################
    /// <summary>
    /// Called by <see cref="CobraSensor"/> when the great cobra catches the
    /// monkey: in the fight it is a fatal hit, taken by the armor if worn.
    /// </summary>
    /// <returns>true if handled here, so the sensor must not end the run
    /// </returns>
    public bool TryAbsorbCobraCatch() {
        if (monkey == null) {
            return false;
        }
        if (
            phase == Phase.Finishing
            || phase == Phase.ReviveOffer
            || phase == Phase.WatchingAd
        ) {
            return true;
        }
        if (phase != Phase.Fighting) {
            return false;
        }
        if (Time.time < saveGraceEndTime) {
            return true;
        }
        if (TrySpendArmor()) {
            if (greatCobra != null) {
                greatCobra.KnockBack(0f);
            }
            ReturnToStart();
            return true;
        }
        StartFinisher(false);
        return true;
    }

    /// <summary>
    /// Called by <see cref="ArmController"/> on every grab: a silk knot is
    /// ripped, a banana is tossed round the trunk as a lure; any grab also
    /// breaks the silk hook's drag.
    /// </summary>
    /// <returns>true if the grab was handled here, so the arm skips its own
    /// fly-to-the-bar effect</returns>
    public bool TryGrab(Collider fruit, Vector3 from) {
        if (phase != Phase.Fighting || fruit == null) {
            return false;
        }
        if (isDragging) {
            EndDrag(true);
        }

        int knot = knots.FindIndex(k => k.trigger == fruit);
        if (knot >= 0) {
            RipKnot(knot);
            return true;
        }
        int lureIndex = lureFruits.FindIndex(f => f.trigger == fruit);
        if (lureIndex >= 0) {
            ArenaItem picked = lureFruits[lureIndex];
            picked.backTime = Time.time + lureRespawnS;
            lureFruits[lureIndex] = picked;
            StaminaBarController stamina = StaminaBarController.I;
            if (stamina != null) {
                stamina.AddStaminaByFruit();
            }
            ThrowLure(from);
            return true;
        }
        return false;
    }

    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [Tooltip("zone & trigger heights, the Spider King's health")]
    [SerializeField]
    private WorldBalanceConfig worldBalanceConfig;

    [Tooltip("watches the ad / goes on after the reward, Player/Jump; also "
        + "the taps that break the silk hook's drag")]
    [SerializeField]
    private InputActionReference confirmAction;

    [Tooltip("gives up the revive offer")]
    [SerializeField]
    private InputActionReference giveUpAction;

    [Tooltip("the climb's spider web, shot by the king & left on the trunk")]
    [SerializeField]
    private SpiderWebTrap webPrefab;

    [Tooltip("the climb's banana branch: bears the silk knots & the lures")]
    [SerializeField]
    private GameObject bananaBranchPrefab;

    [Tooltip("the course page the mock ad's button opens")]
    [SerializeField]
    private string courseUrl =
        "https://classes.usc.edu/term-20263/course/csci-526";

    [Header("Arena")]
    [SerializeField]
    private string bossName = "SPIDER KING";

    [Tooltip("trunk radius; u")]
    [SerializeField]
    [Min(0.5f)]
    private float trunkRadiusU = 5f;

    [Tooltip("radius the branch roots sit at, as the segments' branch ring; u")]
    [SerializeField]
    [Min(0.5f)]
    private float branchRadiusU = 6.6f;

    [Tooltip("height of the arena above where the fight began; the climb is "
        + "capped there; u")]
    [SerializeField]
    [Min(4f)]
    private float arenaHeightU = 14f;

    [Tooltip("the guarding great cobra's head sits this far below the start; u")]
    [SerializeField]
    [Min(2f)]
    private float guardDepthU = 12f;

    [Tooltip("how much farther the camera pulls back in the fight, 1 = none")]
    [SerializeField]
    [Min(1f)]
    private float cameraDistanceScale = 1.6f;

    [Header("Silk Knots")]
    [Tooltip("silk knots in the arena")]
    [SerializeField]
    [Min(1)]
    private int knotCount = 6;

    [Tooltip("health a ripped knot takes off far from the spider")]
    [SerializeField]
    [Min(1)]
    private int knotDamage = 1;

    [Tooltip("health a ripped knot takes off inside the spider's territory")]
    [SerializeField]
    [Min(1)]
    private int territoryKnotDamage = 2;

    [Tooltip("seconds a ripped knot takes to grow back on its own, no heal")]
    [SerializeField]
    [Min(1f)]
    private float knotRegrowS = 14f;

    [Tooltip("diameter of a knot's glowing ball; u")]
    [SerializeField]
    [Min(0.1f)]
    private float knotSizeU = 1.1f;

    [Tooltip("color of the knots")]
    [SerializeField]
    private Color knotColor = new Color(0.95f, 0.97f, 1f);

    [Header("Repair")]
    [Tooltip("a ripped knot this close to the spider is re-spun by it; u")]
    [SerializeField]
    [Min(0f)]
    private float repairRangeU = 9f;

    [Tooltip("seconds the spider spins at a knot to re-spin it")]
    [SerializeField]
    [Min(0.2f)]
    private float repairS = 2.5f;

    [Tooltip("seconds b4 it looks for another knot to re-spin")]
    [SerializeField]
    [Min(0f)]
    private float repairCooldownS = 5f;

    [Tooltip("health a re-spun knot gives back")]
    [SerializeField]
    [Min(0)]
    private int repairHeal = 1;

    [Header("Movement & Territory")]
    [Tooltip("radius of the territory ring round the spider, on the bark; u")]
    [SerializeField]
    [Min(1f)]
    private float territoryRadiusU = 4.5f;

    [Tooltip("the spider stalks the monkey till this far off; u")]
    [SerializeField]
    [Min(1f)]
    private float stalkDistanceU = 7f;

    [Tooltip("crawl speed while stalking; u/s")]
    [SerializeField]
    [Min(0.1f)]
    private float stalkSpeedU = 1.6f;

    [Tooltip("crawl speed to a knot it re-spins; u/s")]
    [SerializeField]
    [Min(0.1f)]
    private float repairSpeedU = 3f;

    [Header("Attacks")]
    [Tooltip("seconds between attacks far from its territory")]
    [SerializeField]
    [Min(0.3f)]
    private float restS = 2.6f;

    [Tooltip("scale on the rest while the monkey is in its territory")]
    [SerializeField]
    [Range(0.2f, 1f)]
    private float territoryRestScale = 0.55f;

    [Tooltip("scale on the rest once all its health is lost, lerped")]
    [SerializeField]
    [Range(0.2f, 1f)]
    private float lowHealthRestScale = 0.7f;

    [Tooltip("wind up of a web shot, its eyes glowing; s")]
    [SerializeField]
    [Min(0.2f)]
    private float webShotWindUpS = 1f;

    [Tooltip("the aim stops following the monkey this long b4 the shot; s")]
    [SerializeField]
    [Min(0f)]
    private float webShotLockS = 0.35f;

    [Tooltip("speed of a web shot; u/s")]
    [SerializeField]
    [Min(1f)]
    private float webShotSpeedU = 16f;

    [Tooltip("missed webs kept on the trunk, the oldest goes first")]
    [SerializeField]
    [Min(0)]
    private int maxStickyWebs = 5;

    [Tooltip("chance an attack out of its territory is the silk hook")]
    [SerializeField]
    [Range(0f, 1f)]
    private float dragChance = 0.35f;

    [Tooltip("the silk hook reaches this far; u")]
    [SerializeField]
    [Min(1f)]
    private float dragRangeU = 14f;

    [Tooltip("wind up of the silk hook; s")]
    [SerializeField]
    [Min(0.2f)]
    private float dragWindUpS = 1.1f;

    [Tooltip("speed the hook reels the monkey in; u/s")]
    [SerializeField]
    [Min(0.1f)]
    private float dragSpeedU = 2.6f;

    [Tooltip("taps that break the silk hook")]
    [SerializeField]
    [Min(1)]
    private int dragEscapeTaps = 5;

    [Tooltip("reeled this close the monkey is wrapped, fatal; u")]
    [SerializeField]
    [Min(0.3f)]
    private float dragReachU = 1.8f;

    [Tooltip("wind up of the leg swipe; s")]
    [SerializeField]
    [Min(0.1f)]
    private float swipeWindUpS = 0.55f;

    [Tooltip("the leg swipe knocks down a monkey this close; u")]
    [SerializeField]
    [Min(0.5f)]
    private float swipeRangeU = 3.5f;

    [Tooltip("crawl speed to a trapped monkey; u/s")]
    [SerializeField]
    [Min(0.1f)]
    private float cocoonSpeedU = 2.2f;

    [Tooltip("this close to a trapped monkey it wraps it, fatal; u")]
    [SerializeField]
    [Min(0.3f)]
    private float cocoonReachU = 1.6f;

    [Tooltip("scale on the web escape gain in the fight")]
    [SerializeField]
    [Min(0.1f)]
    private float webEscapeGainScale = 1.25f;

    [Header("Fruit Lure")]
    [Tooltip("bananas in the arena")]
    [SerializeField]
    [Min(0)]
    private int lureFruitCount = 3;

    [Tooltip("seconds a grabbed banana takes to grow back")]
    [SerializeField]
    [Min(1f)]
    private float lureRespawnS = 10f;

    [Tooltip("flight of a tossed banana round the trunk; s")]
    [SerializeField]
    [Min(0.1f)]
    private float lureFlyS = 0.9f;

    [Tooltip("crawl speed to a tossed banana; u/s")]
    [SerializeField]
    [Min(0.1f)]
    private float lureRunSpeedU = 5f;

    [Tooltip("seconds the spider eats a banana, no attacks meanwhile")]
    [SerializeField]
    [Min(0.5f)]
    private float lureEatS = 4f;

    [Header("Loss & Revive")]
    [Tooltip("seconds of grace after the armor saves the monkey")]
    [SerializeField]
    [Min(0f)]
    private float saveGraceS = 2f;

    [Tooltip("seconds the spider is stunned after the armor saves the monkey")]
    [SerializeField]
    [Min(0f)]
    private float armorStunS = 2.5f;

    [Tooltip("time scale of the slow last blow")]
    [SerializeField]
    [Range(0.05f, 1f)]
    private float finisherTimeScale = 0.25f;

    [Tooltip("real seconds the slow last blow lasts")]
    [SerializeField]
    [Min(0.1f)]
    private float finisherRealS = 1.2f;

    [Tooltip("seconds the revive offer waits")]
    [SerializeField]
    [Min(1f)]
    private float reviveOfferS = 5f;

    [Tooltip("seconds of the mock ad b4 it can be skipped")]
    [SerializeField]
    [Min(0f)]
    private float adSkipAfterS = 3f;

    [Tooltip("speed of the flight back to the start; u/s")]
    [SerializeField]
    [Min(1f)]
    private float reviveReturnSpeedU = 20f;

    [Header("Win")]
    [Tooltip("seconds the beaten spider falls b4 the reward")]
    [SerializeField]
    [Min(0.2f)]
    private float defeatFallS = 2f;

    [Tooltip("web escape gain scale the silk gloves give")]
    [SerializeField]
    [Min(1f)]
    private float glovesEscapeGainScale = 2f;

    [Tooltip("how far down the great cobra restarts its chase after a win; u")]
    [SerializeField]
    [Min(0f)]
    private float winCobraDropU = 45f;

    [SerializeField]
    private string winTitle = "SPIDER KING DEFEATED!";

    [SerializeField]
    private string winLine = "You got the Silk Gloves";

    [SerializeField]
    private string winDetailLine =
        "Break free of spider webs twice as fast";

    [Header("Colors")]
    [SerializeField]
    private Color warnColor = new Color(0.95f, 0.2f, 0.15f);

    [SerializeField]
    private Color silkColor = new Color(0.92f, 0.92f, 0.95f);

    [SerializeField]
    private Color goldenColor = new Color(1f, 0.8f, 0.15f);

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (worldBalanceConfig == null || bananaBranchPrefab == null
            || webPrefab == null) {
            Debug.LogError(
                "SpiderFightController:\tmust assign Inspector Fields: "
                    + "worldBalanceConfig, bananaBranchPrefab, webPrefab",
                this
            );
            enabled = false;
            return;
        }
        if (I != null && I != this) {
            Debug.LogWarning(
                "SpiderFightController:\tduplicate instance destroyed",
                this
            );
            Destroy(gameObject);
            return;
        }
        I = this;
        if (confirmAction == null || giveUpAction == null) {
            Debug.LogWarning(
                "SpiderFightController:\tmust assign Inspector Fields: "
                    + "confirmAction, giveUpAction",
                this
            );
        }

        body = GetComponent<SpiderKingBody>();
        hud = GetComponent<BossFightHud>();
        hud.WatchAdClicked += WatchAd;
        hud.GiveUpClicked += GiveUp;
        hud.CoursePageClicked += OpenCoursePage;
        hud.ContinueClicked += ContinueAfterReward;
        hud.SkipAdClicked += SkipAd;
        colorBlock = new MaterialPropertyBlock();
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
    }

    // the actions are shared, so only switched on here, never off
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
        if (monkeyController != null && IsInArena) {
            monkeyController.ClearHeightCeiling();
        }
        if (cameraFraming != null && IsInArena) {
            cameraFraming.SetBossFraming(0f, 1f);
        }
    }

    private void Update() {
        GameController game = GameController.I;
        if (game == null) {
            return;
        }
        if (phase == Phase.Waiting) {
            if (CanBegin(game)) {
                BeginFight();
            }
            return;
        }
        if (!game.IsRunLive || monkey == null
            || phase == Phase.Done || phase == Phase.Over) {
            return;
        }
        if (phase == Phase.Rewarding) {
            UpdateRewardPause(game);
            return;
        }

        hud.SpeechAnchor = body.HeadPosition;
        UpdateCamera();
        UpdateProjectiles();
        UpdateLureFlight();
        if (phase == Phase.Fighting) {
            UpdateReturnToStart();
            UpdateItems();
            UpdateSpider();
            UpdateDrag(game);
            UpdateTerritoryRing();
            UpdateCocoonBanner();
        } else if (phase == Phase.ReviveOffer || phase == Phase.WatchingAd) {
            UpdateRevive(game);
        } else if (phase == Phase.Finishing) {
            UpdateFinisher();
        } else if (phase == Phase.Defeated) {
            UpdateDefeat();
        }
    }

    // private methods  ########################################################
    // Setup  ------------------------------------------------------------------
    // after the Cobra King, never in the endless run, never mid leap
    private bool CanBegin(GameController game) {
        if (game.State != GameState.Playing || game.IsLeaping
            || game.Mode == RunMode.Endless) {
            return false;
        }
        BossFightController cobraKing = BossFightController.I;
        if (cobraKing != null && !cobraKing.IsDone) {
            return false;
        }
        RampedDifficultyManager ramp = RampedDifficultyManager.I;
        return ramp != null
            && ramp.CurrentPlayerClimbedYDistance
                >= worldBalanceConfig.SpiderTriggerHeightU;
    }

    private void BeginFight() {
        monkeyController = FindFirstObjectByType<MonkeyController>();
        if (monkeyController == null
            || monkeyController.transform.parent == null) {
            Debug.LogError(
                "SpiderFightController:\tno MonkeyController under a trunk "
                    + "root found",
                this
            );
            enabled = false;
            return;
        }
        monkey = monkeyController.transform;
        trunkCenter = monkey.parent.position;
        entryY = monkey.position.y;
        entryPosition = monkey.position;
        entryRotation = monkey.rotation;
        maxHealth = worldBalanceConfig.SpiderMaxHealth;
        health = maxHealth;
        phase = Phase.Fighting;
        FightStarted?.Invoke();

        // anything grown near the arena from b4 the zone goes
        foreach (BranchController branch in
            FindObjectsByType<BranchController>(FindObjectsSortMode.None)) {
            if (branch.transform.IsChildOf(transform)) {
                continue;
            }
            float y = branch.transform.position.y;
            if (y > entryY - 3f && y < entryY + arenaHeightU + 4f) {
                ForgetFruits(branch.gameObject);
                Destroy(branch.gameObject);
            }
        }

        cameraFraming = FindFirstObjectByType<CameraClimbFraming>();
        greatCobra = FindFirstObjectByType<CobraMotor>();
        if (greatCobra != null) {
            greatCobra.StartGuard(entryY - guardDepthU);
        }
        struggle = FindFirstObjectByType<SpiderWebStruggle>();
        if (struggle != null) {
            struggle.EscapeGainScale = webEscapeGainScale;
        }
        monkeyController.SetHeightCeiling(entryY + arenaHeightU);

        // the spider starts a little above, a quarter round the trunk
        startAngle = AngleOf(monkey.position) + 70f;
        startY = entryY + 6f;
        spiderAngle = startAngle;
        spiderY = startY;
        body.Build(trunkCenter, spiderAngle, spiderY);
        BuildTerritoryRing();
        SpawnKnots();
        SpawnLures();
        ResetSpiderState();

        hud.Show(bossName);
        hud.SetHealth(1f);
        hud.Flash(warnColor);
        BossFightController cobraKing = BossFightController.I;
        bool hasArmor = cobraKing != null && cobraKing.HasArmor;
        hud.ShowBannerFor(
            bossName,
            "Grab the glowing silk knots to rip its web!\n"
                + "Inside its red ring a rip does double damage"
                + (hasArmor ? "\nYour gold armor still guards you" : ""),
            silkColor,
            4.5f
        );
        hud.Say("My web, my rules!");
        if (Debug.isDebugBuild) {
            Debug.Log(
                "SpiderFightController:\tspider fight begins at y " + entryY,
                this
            );
        }
    }

    private void ResetSpiderState() {
        mode = Mode.Idle;
        attack = Attack.None;
        attackTimeS = 0f;
        nextAttackTime = Time.time + 2.5f;
        nextRepairTime = Time.time + repairCooldownS;
        stunEndTime = 0f;
        saveGraceEndTime = 0f;
        hasShownLureHint = false;
        isCocoonBannerShown = false;
        body.SetEyeGlow(0f);
        body.SetSwipe(0f);
        body.SetEating(false);
    }

    // Knots & Lures  ----------------------------------------------------------
    private void SpawnKnots() {
        ClearItems(knots);
        float baseAngle = AngleOf(monkey.position);
        for (int i = 0; i < knotCount; i++) {
            // round the trunk, at three alternating heights
            float angle = baseAngle + 30f + i * 360f / knotCount
                + Random.Range(-12f, 12f);
            float y = entryY + Mathf.Lerp(2f, arenaHeightU - 2.5f,
                (i % 3) / 2f);
            knots.Add(AddItem(angle, y, true));
        }
    }

    private void SpawnLures() {
        ClearItems(lureFruits);
        float baseAngle = AngleOf(monkey.position);
        for (int i = 0; i < lureFruitCount; i++) {
            // between the knots
            float angle = baseAngle + i * 360f / Mathf.Max(1, lureFruitCount);
            float y = entryY + Mathf.Lerp(1f, arenaHeightU - 4f,
                lureFruitCount > 1 ? i / (lureFruitCount - 1f) : 0.5f);
            lureFruits.Add(AddItem(angle, y, false));
        }
    }

    private ArenaItem AddItem(float angle, float y, bool isKnot) {
        GameObject branch = Instantiate(bananaBranchPrefab, transform);
        branch.transform.SetPositionAndRotation(
            PointOnTrunk(angle, y + BANANA_HANG_U, branchRadiusU),
            Quaternion.Euler(0f, 90f - angle, 0f)
        );
        BranchController controller = branch.GetComponent<BranchController>();
        if (controller != null) {
            controller.Setup(); // the banana branch prefab bears a banana
        }
        ScorePickup pickup = branch.GetComponentInChildren<ScorePickup>(true);
        GameObject fruit = pickup != null ? pickup.gameObject : branch;
        Collider trigger = fruit.GetComponent<Collider>();
        if (trigger == null) {
            trigger = fruit.GetComponentInChildren<Collider>(true);
        }
        if (isKnot) {
            MakeKnot(fruit, trigger);
        }
        return new ArenaItem {
            root = fruit,
            branch = branch,
            trigger = trigger,
            backTime = -1f,
        };
    }

    // the banana's model is hidden behind a glowing silk ball; its collider
    // & scripts stay, so the grab works as on any fruit
    private void MakeKnot(GameObject fruit, Collider trigger) {
        foreach (Renderer part in fruit.GetComponentsInChildren<Renderer>(true)) {
            part.enabled = false;
        }
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "SilkKnot";
        DestroyImmediate(ball.GetComponent<Collider>());
        ball.transform.SetParent(fruit.transform, false);
        ball.transform.position = trigger != null
            ? trigger.bounds.center
            : fruit.transform.position;
        float scale = Mathf.Max(0.0001f, fruit.transform.lossyScale.x);
        ball.transform.localScale = Vector3.one * (knotSizeU / scale);
        Renderer ballRenderer = ball.GetComponent<Renderer>();
        if (body.PrimitiveMaterial != null) {
            ballRenderer.sharedMaterial = body.PrimitiveMaterial;
        }
        SetColor(ballRenderer, knotColor);
        knotBalls[fruit] = ballRenderer;
    }

    private void ClearItems(List<ArenaItem> items) {
        foreach (ArenaItem item in items) {
            if (item.branch != null) {
                ForgetFruits(item.branch);
                Destroy(item.branch);
            }
        }
        items.Clear();
    }

    // knots grow back on their own, bananas too; knots pulse their glow
    private void UpdateItems() {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        for (int i = 0; i < knots.Count; i++) {
            ArenaItem knot = knots[i];
            if (knot.root == null) {
                continue;
            }
            if (!knot.root.activeSelf && knot.backTime >= 0f
                && Time.time >= knot.backTime) {
                RestoreKnot(i, false);
                continue;
            }
            if (knotBalls.TryGetValue(knot.root, out Renderer ball)) {
                // a knot in the territory glows gold: worth double
                Color glow = IsInTerritory(knot.root.transform.position)
                    ? goldenColor
                    : knotColor;
                SetColor(ball, Color.Lerp(glow * 0.75f, glow, pulse));
            }
        }
        for (int i = 0; i < lureFruits.Count; i++) {
            ArenaItem fruit = lureFruits[i];
            if (fruit.root == null || fruit.root.activeSelf
                || fruit.backTime < 0f || Time.time < fruit.backTime) {
                continue;
            }
            fruit.root.SetActive(true);
            fruit.backTime = -1f;
            lureFruits[i] = fruit;
        }
    }

    private void RipKnot(int index) {
        ArenaItem knot = knots[index];
        Vector3 spot = knot.root.transform.position;
        bool isClose = IsInTerritory(spot);
        int damage = isClose ? territoryKnotDamage : knotDamage;
        knot.backTime = Time.time + knotRegrowS;
        knots[index] = knot;

        health = Mathf.Max(0, health - damage);
        hud.SetHealth(health / (float)maxHealth);
        hud.PopDamage(isClose ? "-" + damage + " CLOSE!" : "-" + damage, spot);
        hud.Flash(isClose ? goldenColor : silkColor);
        if (health <= 0) {
            StartFinisher(true);
            return;
        }
        if (isClose) {
            hud.Say("Get away from my web!");
            // ripped right under its nose: it lashes out soon
            if (mode == Mode.Idle) {
                nextAttackTime = Mathf.Min(nextAttackTime, Time.time + 0.6f);
            }
        }
    }

    private void RestoreKnot(int index, bool isRepair) {
        ArenaItem knot = knots[index];
        knot.root.SetActive(true);
        knot.backTime = -1f;
        knots[index] = knot;
        if (!isRepair || repairHeal <= 0 || health >= maxHealth) {
            return;
        }
        health = Mathf.Min(maxHealth, health + repairHeal);
        hud.SetHealth(health / (float)maxHealth);
        hud.PopText("+" + repairHeal + " re-spun", knot.root.transform.position,
            silkColor);
    }

    // a banana copy flies round the trunk to the far side from the monkey,
    // so the spider leaves to eat it there; the player only picks when
    private void ThrowLure(Vector3 from) {
        if (lure != null) {
            Destroy(lure.gameObject);
        }
        lure = CreatePrimitive(PrimitiveType.Capsule, "Lure",
            new Vector3(0.5f, 0.8f, 0.5f), new Color(1f, 0.85f, 0.2f)).transform;
        lureFromAngle = AngleOf(from);
        lureFromY = from.y;
        lureFromRadius = Mathf.Max(trunkRadiusU + 0.5f,
            HorizontalDistance(from));
        // round the side away from the spider
        float toSpider = Mathf.DeltaAngle(lureFromAngle, spiderAngle);
        lureTurnDeg = toSpider >= 0f ? -180f : 180f;
        lureToY = Mathf.Clamp(from.y, entryY + 1f, entryY + arenaHeightU - 1f);
        lureAgeS = 0f;
        isLureFlying = true;
        lure.position = from;
    }

    private void UpdateLureFlight() {
        if (lure == null) {
            return;
        }
        if (!isLureFlying) {
            // eaten away while the spider munches
            if (mode == Mode.Eating) {
                float left = 1f - Mathf.Clamp01(modeTimeS / lureEatS);
                lure.localScale = new Vector3(0.5f, 0.8f, 0.5f)
                    * Mathf.Max(0.15f, left);
            }
            return;
        }
        lureAgeS += Time.deltaTime;
        float t = Mathf.Clamp01(lureAgeS / lureFlyS);
        float angle = lureFromAngle + lureTurnDeg * t;
        float radius = Mathf.Lerp(lureFromRadius, trunkRadiusU + 0.35f, t)
            + Mathf.Sin(t * Mathf.PI) * 3f;
        float y = Mathf.Lerp(lureFromY, lureToY, t) + Mathf.Sin(t * Mathf.PI) * 2f;
        lure.position = PointOnTrunk(angle, y, radius);
        lure.rotation = Quaternion.Euler(t * 540f, angle, 0f);
        if (t < 1f) {
            return;
        }
        isLureFlying = false;
        lureAngle = lureFromAngle + lureTurnDeg;
        lureY = lureToY;
        lure.rotation = Quaternion.Euler(0f, -lureAngle, 90f);
        // the snack wins over anything but a wrap already under way
        CancelAttack();
        if (isDragging) {
            EndDrag(true);
        }
        SetMode(Mode.ToLure);
        hud.Say("Ooh, a snack...");
        if (!hasShownLureHint) {
            hasShownLureHint = true;
            hud.ShowBannerFor(
                "Lured!",
                "It goes for the fruit: rip the knots near it now!",
                goldenColor,
                2.5f
            );
        }
    }

    // Spider  -----------------------------------------------------------------
    private void SetMode(Mode next) {
        mode = next;
        modeTimeS = 0f;
        body.SetEating(next == Mode.Eating || next == Mode.Repairing);
    }

    private void UpdateSpider() {
        modeTimeS += Time.deltaTime;
        float walk = 0f;
        bool isTrapped = struggle != null && struggle.IsTrapped;
        bool isStunned = Time.time < stunEndTime;

        // a trapped monkey is crawled to, unless a snack holds it
        if (isTrapped && !isStunned && mode != Mode.Eating
            && mode != Mode.ToLure && mode != Mode.Cocooning) {
            CancelAttack();
            SetMode(Mode.Cocooning);
            hud.Say("Hold still, little monkey...");
        }

        switch (mode) {
            case Mode.Idle:
                walk = UpdateIdle(isStunned);
                break;
            case Mode.Repairing:
                walk = UpdateRepair();
                break;
            case Mode.ToLure:
                walk = MoveToward(lureAngle, lureY, lureRunSpeedU, 1.2f)
                    ? 0f
                    : 1f;
                if (lure == null || isLureFlying) {
                    SetMode(Mode.Idle);
                } else if (walk == 0f) {
                    SetMode(Mode.Eating);
                }
                break;
            case Mode.Eating:
                if (modeTimeS >= lureEatS) {
                    if (lure != null) {
                        Destroy(lure.gameObject);
                    }
                    lure = null;
                    SetMode(Mode.Idle);
                    nextAttackTime = Time.time + 1f;
                }
                break;
            case Mode.Cocooning:
                walk = UpdateCocoon(isTrapped, isStunned);
                break;
        }

        float glow = attack != Attack.None
            ? Mathf.Clamp01(attackTimeS / WindUpOf(attack))
            : mode == Mode.Cocooning ? 1f : 0f;
        body.SetEyeGlow(glow);
        body.Place(spiderAngle, spiderY, walk);
    }

    private float UpdateIdle(bool isStunned) {
        if (isStunned) {
            return 0f;
        }
        if (attack != Attack.None) {
            UpdateAttack();
            return 0f;
        }
        if (TryStartRepair()) {
            return 0f;
        }
        float walk = 0f;
        Vector3 at = monkey.position;
        if (SurfaceDistance(spiderAngle, spiderY, AngleOf(at), at.y)
            > stalkDistanceU) {
            MoveToward(AngleOf(at), at.y, stalkSpeedU, stalkDistanceU);
            walk = 0.5f;
        }
        if (Time.time >= nextAttackTime && !isDragging) {
            StartAttack();
        }
        return walk;
    }

    // a ripped knot near it is re-spun, the nearest first
    private bool TryStartRepair() {
        if (Time.time < nextRepairTime) {
            return false;
        }
        int best = -1;
        float bestDistance = repairRangeU;
        for (int i = 0; i < knots.Count; i++) {
            ArenaItem knot = knots[i];
            if (knot.root == null || knot.root.activeSelf) {
                continue;
            }
            Vector3 spot = knot.root.transform.position;
            float distance = SurfaceDistance(spiderAngle, spiderY,
                AngleOf(spot), spot.y);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = i;
            }
        }
        if (best < 0) {
            return false;
        }
        repairIndex = best;
        SetMode(Mode.Repairing);
        isRepairSpinning = false;
        return true;
    }

    private float UpdateRepair() {
        if (repairIndex < 0 || repairIndex >= knots.Count
            || knots[repairIndex].root == null) {
            SetMode(Mode.Idle);
            return 0f;
        }
        ArenaItem knot = knots[repairIndex];
        // grown back meanwhile: nothing to mend
        if (knot.root.activeSelf) {
            SetMode(Mode.Idle);
            nextRepairTime = Time.time + repairCooldownS;
            return 0f;
        }
        if (!isRepairSpinning) {
            Vector3 spot = knot.root.transform.position;
            bool hasArrived = MoveToward(AngleOf(spot), spot.y, repairSpeedU,
                1f);
            if (!hasArrived) {
                return 0.8f;
            }
            isRepairSpinning = true;
            modeTimeS = 0f;
            hud.Say("Mending...");
            return 0f;
        }
        if (modeTimeS >= repairS) {
            RestoreKnot(repairIndex, true);
            SetMode(Mode.Idle);
            nextRepairTime = Time.time + repairCooldownS;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.8f);
        }
        return 0f;
    }

    private float UpdateCocoon(bool isTrapped, bool isStunned) {
        if (!isTrapped) {
            SetMode(Mode.Idle);
            nextAttackTime = Time.time + RestNow() * 0.7f;
            return 0f;
        }
        if (isStunned) {
            return 0f;
        }
        Vector3 at = monkey.position;
        bool hasArrived = MoveToward(AngleOf(at), at.y, cocoonSpeedU,
            cocoonReachU);
        if (hasArrived) {
            LethalHit("Wrapped!");
        }
        return hasArrived ? 0f : 1f;
    }

    // moves along the bark, the shortest way round; true once within reach
    private bool MoveToward(
        float targetAngle,
        float targetY,
        float speedU,
        float reachU
    ) {
        targetY = Mathf.Clamp(targetY, entryY - 1f, entryY + arenaHeightU);
        float arcU = Mathf.DeltaAngle(spiderAngle, targetAngle)
            * Mathf.Deg2Rad * trunkRadiusU;
        float dy = targetY - spiderY;
        float distance = Mathf.Sqrt(arcU * arcU + dy * dy);
        if (distance <= reachU) {
            return true;
        }
        float step = Mathf.Min(speedU * Time.deltaTime, distance - reachU);
        spiderAngle += arcU / distance * step / trunkRadiusU * Mathf.Rad2Deg;
        spiderY += dy / distance * step;
        return false;
    }

    // Attacks  ----------------------------------------------------------------
    private float RestNow() {
        float lostShare = 1f - health / (float)maxHealth;
        float rest = restS * Mathf.Lerp(1f, lowHealthRestScale, lostShare);
        if (IsInTerritory(monkey.position)) {
            rest *= territoryRestScale;
        }
        return rest;
    }

    private float WindUpOf(Attack kind) {
        switch (kind) {
            case Attack.WebShot:
                return webShotWindUpS;
            case Attack.Drag:
                return dragWindUpS;
            case Attack.Swipe:
                return swipeWindUpS;
            default:
                return 1f;
        }
    }

    private void StartAttack() {
        Vector3 at = monkey.position;
        float distance = SurfaceDistance(spiderAngle, spiderY, AngleOf(at),
            at.y);
        bool isTrapped = struggle != null && struggle.IsTrapped;
        if (isTrapped) {
            return;
        }
        if (distance < territoryRadiusU) {
            attack = Attack.Swipe;
        } else if (distance < dragRangeU && Random.value < dragChance) {
            attack = Attack.Drag;
        } else {
            attack = Attack.WebShot;
        }
        attackTimeS = 0f;
        aimPoint = at;
        string word = attack == Attack.Swipe
            ? "SWIPE!"
            : attack == Attack.Drag ? "SILK!" : "WEB!";
        warningId = hud.AddWarning(aimPoint, word);
        if (attack == Attack.Drag) {
            ShowSilk(true);
        }
    }

    private void UpdateAttack() {
        attackTimeS += Time.deltaTime;
        float windUp = WindUpOf(attack);
        float progress = Mathf.Clamp01(attackTimeS / windUp);
        // the aim follows the monkey till a moment b4 the shot
        bool isLocked = attack == Attack.WebShot
            && attackTimeS >= windUp - webShotLockS;
        if (!isLocked) {
            aimPoint = monkey.position;
        }
        if (warningId >= 0) {
            hud.UpdateWarning(warningId, aimPoint, progress);
        }
        if (attack == Attack.Swipe) {
            body.SetSwipe(progress);
        }
        if (attack == Attack.Drag) {
            UpdateSilkLine(Random.value < 0.75f);
        }
        if (progress < 1f) {
            return;
        }

        Attack fired = attack;
        CancelAttack();
        nextAttackTime = Time.time + RestNow();
        switch (fired) {
            case Attack.WebShot:
                FireWeb();
                break;
            case Attack.Drag:
                StartDrag();
                break;
            case Attack.Swipe:
                Swipe();
                break;
        }
    }

    private void CancelAttack() {
        attack = Attack.None;
        attackTimeS = 0f;
        if (warningId >= 0) {
            hud.RemoveWarning(warningId);
            warningId = -1;
        }
        body.SetSwipe(0f);
        if (!isDragging) {
            ShowSilk(false);
        }
    }

    private void FireWeb() {
        Transform shot = CreatePrimitive(PrimitiveType.Sphere, "WebShot",
            Vector3.one * 0.7f, silkColor).transform;
        shot.position = body.HeadPosition;
        projectiles.Add(new Projectile {
            transform = shot,
            target = aimPoint,
        });
    }

    // web shots fly straight to their aim, & stick a web on the trunk there
    private void UpdateProjectiles() {
        for (int i = projectiles.Count - 1; i >= 0; i--) {
            Projectile shot = projectiles[i];
            if (shot.transform == null) {
                projectiles.RemoveAt(i);
                continue;
            }
            shot.transform.position = Vector3.MoveTowards(
                shot.transform.position,
                shot.target,
                webShotSpeedU * Time.deltaTime
            );
            if (shot.transform.position != shot.target) {
                continue;
            }
            Destroy(shot.transform.gameObject);
            projectiles.RemoveAt(i);
            if (phase == Phase.Fighting) {
                PlaceWeb(AngleOf(shot.target), shot.target.y);
            }
        }
    }

    // as the segments' spawner: the web's face looks away from the trunk
    private void PlaceWeb(float angle, float y) {
        stickyWebs.RemoveAll(w => w == null);
        while (maxStickyWebs > 0 && stickyWebs.Count >= maxStickyWebs) {
            SpiderWebTrap oldest = stickyWebs[0];
            stickyWebs.RemoveAt(0);
            // never pull the web holding the monkey right now
            if (oldest != null && !IsWebHoldingMonkey(oldest)) {
                Destroy(oldest.gameObject);
            }
        }
        SpiderWebTrap web = Instantiate(
            webPrefab,
            PointOnTrunk(angle, y, trunkRadiusU + 0.02f),
            Quaternion.Euler(0f, -angle, 0f),
            transform
        );
        stickyWebs.Add(web);
    }

    private bool IsWebHoldingMonkey(SpiderWebTrap web) {
        return struggle != null && struggle.IsTrapped && monkey != null
            && Vector3.Distance(web.transform.position, monkey.position) < 2f;
    }

    private void Swipe() {
        Vector3 at = monkey.position;
        float distance = SurfaceDistance(spiderAngle, spiderY, AngleOf(at),
            at.y);
        if (distance > swipeRangeU || Time.time < saveGraceEndTime) {
            return;
        }
        hud.PopText("Swiped!", at, warnColor);
        KnockDown(body.Contact);
    }

    // the silk hook reels the monkey in along the bark; taps or a grab
    // break it, reaching the spider wraps it
    private void StartDrag() {
        if (monkeyController == null
            || (struggle != null && (struggle.IsTrapped || struggle.IsProtected))
            || Time.time < saveGraceEndTime) {
            ShowSilk(false);
            return;
        }
        isDragging = true;
        dragTaps = 0;
        dragTimeS = 0f;
        monkeyController.IsHeld = true;
        ShowSilk(true);
        hud.ShowBannerFor("Silk hook!", "Tap SPACE or grab to break free",
            warnColor, 0f);
    }

    private void UpdateDrag(GameController game) {
        if (!isDragging) {
            return;
        }
        dragTimeS += Time.deltaTime;
        UpdateSilkLine(true);
        if (!game.IsPaused && WasPressed(confirmAction)) {
            dragTaps++;
            hud.PopText((dragEscapeTaps - dragTaps).ToString(),
                monkey.position, silkColor);
            if (dragTaps >= dragEscapeTaps) {
                EndDrag(true);
                return;
            }
        }
        // a web took over, or the hook ran out of reel
        if ((struggle != null && struggle.IsTrapped) || dragTimeS > 8f) {
            EndDrag(false);
            return;
        }
        Vector3 at = monkey.position;
        float monkeyAngle = AngleOf(at);
        float arcU = Mathf.DeltaAngle(monkeyAngle, spiderAngle)
            * Mathf.Deg2Rad * trunkRadiusU;
        float dy = spiderY - at.y;
        float distance = Mathf.Sqrt(arcU * arcU + dy * dy);
        if (distance <= dragReachU) {
            EndDrag(false);
            LethalHit("Wrapped!");
            return;
        }
        // a pull target a short way ahead along the bark
        float lead = Mathf.Min(0.6f, distance);
        float radius = HorizontalDistance(at);
        Vector3 target = PointOnTrunk(
            monkeyAngle + arcU / distance * lead / trunkRadiusU * Mathf.Rad2Deg,
            at.y + dy / distance * lead,
            radius
        );
        monkeyController.PullTo(target, dragSpeedU);
    }

    private void EndDrag(bool isEscape) {
        if (!isDragging) {
            return;
        }
        isDragging = false;
        ShowSilk(false);
        hud.HideBanner();
        bool isTrapped = struggle != null && struggle.IsTrapped;
        if (monkeyController != null && !isTrapped) {
            monkeyController.SyncOrbitAngle();
            monkeyController.IsHeld = false;
        }
        if (isEscape && monkey != null) {
            hud.PopText("Broke free!", monkey.position, goldenColor);
        }
    }

    private void ShowSilk(bool isShown) {
        if (isShown && silkLine == null) {
            silkLine = CreatePrimitive(PrimitiveType.Cylinder, "SilkLine",
                Vector3.one * 0.08f, silkColor).transform;
        }
        if (silkLine != null) {
            silkLine.gameObject.SetActive(isShown);
            if (isShown) {
                UpdateSilkLine(true);
            }
        }
    }

    private void UpdateSilkLine(bool isVisible) {
        if (silkLine == null) {
            return;
        }
        silkLine.gameObject.SetActive(isVisible);
        Vector3 from = body.HeadPosition;
        Vector3 to = monkey.position;
        Vector3 span = to - from;
        if (span.sqrMagnitude < 0.0001f) {
            return;
        }
        silkLine.SetPositionAndRotation(
            (from + to) * 0.5f,
            Quaternion.FromToRotation(Vector3.up, span.normalized)
        );
        // thin while it winds up, thicker once it holds
        float thickness = isDragging ? 0.12f : 0.05f;
        silkLine.localScale = new Vector3(thickness, span.magnitude * 0.5f,
            thickness);
    }

    private void UpdateCocoonBanner() {
        bool isCocooning = mode == Mode.Cocooning;
        if (isCocooning == isCocoonBannerShown) {
            return;
        }
        isCocoonBannerShown = isCocooning;
        if (isCocooning) {
            hud.ShowBannerFor("Trapped!",
                "Mash SPACE before it reaches you", warnColor, 0f);
        } else if (!isDragging) {
            hud.HideBanner();
        }
    }

    // Territory  --------------------------------------------------------------
    private bool IsInTerritory(Vector3 position) {
        return SurfaceDistance(spiderAngle, spiderY, AngleOf(position),
            position.y) < territoryRadiusU;
    }

    private void BuildTerritoryRing() {
        ClearTerritoryRing();
        for (int i = 0; i < TERRITORY_DOTS; i++) {
            ringDots.Add(CreatePrimitive(PrimitiveType.Sphere, "TerritoryDot",
                Vector3.one * 0.3f, warnColor).GetComponent<Renderer>());
        }
    }

    private void ClearTerritoryRing() {
        foreach (Renderer dot in ringDots) {
            if (dot != null) {
                Destroy(dot.gameObject);
            }
        }
        ringDots.Clear();
    }

    // a ring of red dots on the bark round the spider, bright while the
    // monkey is inside
    private void UpdateTerritoryRing() {
        bool isInside = IsInTerritory(monkey.position);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (isInside ? 10f : 3f));
        Color color = Color.Lerp(warnColor * 0.55f, warnColor,
            isInside ? 0.6f + 0.4f * pulse : 0.3f * pulse);
        for (int i = 0; i < ringDots.Count; i++) {
            float t = (i + Time.time * 0.15f) / ringDots.Count * Mathf.PI * 2f;
            float angle = spiderAngle + Mathf.Cos(t) * territoryRadiusU
                / trunkRadiusU * Mathf.Rad2Deg;
            float y = spiderY + Mathf.Sin(t) * territoryRadiusU;
            ringDots[i].transform.position =
                PointOnTrunk(angle, y, trunkRadiusU + 0.08f);
            SetColor(ringDots[i], color);
        }
    }

    // Loss, Finisher & Revive  ------------------------------------------------
    // a fatal hit: the kept gold armor takes it, else the slow last blow
    private void LethalHit(string word) {
        if (phase != Phase.Fighting || isReturningToStart
            || Time.time < saveGraceEndTime) {
            return;
        }
        if (TrySpendArmor()) {
            // the spider reels back, stunned, & the monkey is let go
            stunEndTime = Time.time + armorStunS;
            SetMode(Mode.Idle);
            CancelAttack();
            float away = Mathf.Sign(Mathf.DeltaAngle(AngleOf(monkey.position),
                spiderAngle));
            spiderAngle += (away == 0f ? 1f : away) * 6f / trunkRadiusU
                * Mathf.Rad2Deg;
            nextAttackTime = stunEndTime + 1f;
            return;
        }
        hud.PopText(word, monkey.position, warnColor);
        StartFinisher(false);
    }

    // the Cobra King's leftover armor, if worn; frees the monkey too
    private bool TrySpendArmor() {
        BossFightController cobraKing = BossFightController.I;
        if (cobraKing == null || !cobraKing.TrySpendArmor()) {
            return false;
        }
        saveGraceEndTime = Time.time + saveGraceS;
        if (isDragging) {
            EndDrag(false);
        }
        if (struggle != null) {
            struggle.ForceRelease();
        }
        return true;
    }

    private void StartFinisher(bool isWin) {
        phase = Phase.Finishing;
        isFinisherWin = isWin;
        finisherTimeS = 0f;
        CancelAttack();
        if (isDragging) {
            EndDrag(false);
        }
        hud.HideBanner();
        hud.ClearWarnings();
        Time.timeScale = finisherTimeScale;
        if (isWin) {
            hud.Flash(Color.white);
            return;
        }
        hud.Flash(new Color(0.9f, 0.1f, 0.1f));
        hud.ShowBanner("K.O.", new Color(0.95f, 0.15f, 0.15f));
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
        OfferRevive();
    }

    private void OfferRevive() {
        phase = Phase.ReviveOffer;
        reviveLeftS = reviveOfferS;
        Time.timeScale = 0f;
        hud.HideBanner();
        hud.ShowReviveOffer();
        hud.SetReviveCountdown(Mathf.CeilToInt(reviveLeftS));
    }

    private void UpdateRevive(GameController game) {
        Time.timeScale = 0f;
        if (game.IsPaused) {
            return;
        }
        float stepS = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
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
        phase = Phase.Over;
        SetUntouchable(false);
        if (GameController.I != null) {
            GameController.I.LoseGame();
        }
    }

    private void SkipAd() {
        if (phase != Phase.WatchingAd || adElapsedS < adSkipAfterS) {
            return;
        }
        RestartFight();
    }

    private void OpenCoursePage() {
        if (phase == Phase.WatchingAd && !string.IsNullOrEmpty(courseUrl)) {
            Application.OpenURL(courseUrl);
        }
    }

    // after the ad the fight starts over at full health; a spent armor
    // stays spent
    private void RestartFight() {
        hud.HideRevive();
        Time.timeScale = 1f;
        phase = Phase.Fighting;
        health = maxHealth;
        hud.SetHealth(1f);
        foreach (Projectile shot in projectiles) {
            if (shot.transform != null) {
                Destroy(shot.transform.gameObject);
            }
        }
        projectiles.Clear();
        ClearStickyWebs();
        if (lure != null) {
            Destroy(lure.gameObject);
        }
        lure = null;
        isLureFlying = false;
        for (int i = 0; i < knots.Count; i++) {
            if (knots[i].root != null) {
                RestoreKnot(i, false);
            }
        }
        spiderAngle = startAngle;
        spiderY = startY;
        ResetSpiderState();
        StaminaBarController stamina = StaminaBarController.I;
        if (stamina != null) {
            stamina.AddStamina(stamina.MaxStamina);
        }
        if (greatCobra != null) {
            greatCobra.KnockBack(0f);
        }
        monkey.rotation = entryRotation;
        ReturnToStart();
        hud.Flash(silkColor);
        hud.Say("Back for more?");
    }

    private void ReturnToStart() {
        if (monkeyController == null) {
            return;
        }
        if (isDragging) {
            EndDrag(false);
        }
        if (struggle != null) {
            struggle.ForceRelease();
        }
        SetUntouchable(true);
        monkeyController.SetHeightCeiling(entryY + arenaHeightU);
        monkeyController.ClearStun();
        monkeyController.IsHeld = true;
        monkeyController.PullTo(entryPosition, reviveReturnSpeedU);
        isReturningToStart = true;
    }

    private void UpdateReturnToStart() {
        if (!isReturningToStart || monkeyController == null) {
            return;
        }
        if (
            monkeyController.HasReachedPullTarget
            || (monkey.position - entryPosition).sqrMagnitude < 0.01f
        ) {
            isReturningToStart = false;
            monkeyController.SyncOrbitAngle();
            monkeyController.IsHeld = false;
            SetUntouchable(false);
        }
    }

    private void ClearStickyWebs() {
        foreach (SpiderWebTrap web in stickyWebs) {
            if (web != null) {
                Destroy(web.gameObject);
            }
        }
        stickyWebs.Clear();
    }

    // Win  --------------------------------------------------------------------
    private void Defeat() {
        phase = Phase.Defeated;
        defeatTimeS = 0f;
        hud.SetHealth(0f);
        hud.ClearWarnings();
        ClearTerritoryRing();
        ClearStickyWebs();
        hud.Say("My beautiful web...");
        if (Debug.isDebugBuild) {
            Debug.Log("SpiderFightController:\tSpider King defeated", this);
        }
    }

    private void UpdateDefeat() {
        defeatTimeS += Time.deltaTime;
        body.Fall(defeatTimeS);
        if (defeatTimeS < defeatFallS) {
            return;
        }
        body.Clear();
        phase = Phase.Rewarding;
        // the gloves: every web from now on breaks twice as fast
        if (struggle != null) {
            struggle.EscapeGainScale = glovesEscapeGainScale;
        }
        hud.HideBossBar();
        hud.Flash(silkColor);
        hud.ShowRewardPanel(winTitle, winLine, winDetailLine);
    }

    // time stops on the reward panel till the player goes on
    private void UpdateRewardPause(GameController game) {
        Time.timeScale = 0f;
        if (game.IsPaused) {
            return;
        }
        if (WasPressed(confirmAction)) {
            ContinueAfterReward();
        }
    }

    private void ContinueAfterReward() {
        if (phase != Phase.Rewarding) {
            return;
        }
        hud.HideRewardPanel();
        Time.timeScale = 1f;
        phase = Phase.Done;
        ClearItems(knots);
        ClearItems(lureFruits);
        knotBalls.Clear();
        if (lure != null) {
            Destroy(lure.gameObject);
        }
        if (silkLine != null) {
            Destroy(silkLine.gameObject);
        }
        if (greatCobra != null) {
            greatCobra.StopGuard();
            greatCobra.KnockBack(winCobraDropU);
        }
        if (monkeyController != null) {
            monkeyController.ClearHeightCeiling();
        }
        if (cameraFraming != null) {
            cameraFraming.SetBossFraming(0f, 1f);
        }
        hud.ShowBannerFor("The cobra is coming!", "Climb!",
            new Color(0.95f, 0.25f, 0.2f), 2.5f);
        FightEnded?.Invoke();
        if (Debug.isDebugBuild) {
            Debug.Log(
                "SpiderFightController:\twon, silk gloves given, climb on",
                this
            );
        }
    }

    // Camera & Helpers  -------------------------------------------------------
    // pulled back, aimed a little toward the spider, so both stay in view
    private void UpdateCamera() {
        if (cameraFraming == null) {
            return;
        }
        float focus = Mathf.Clamp((spiderY - monkey.position.y) * 0.3f, 0f, 3f);
        cameraFraming.SetBossFraming(focus, cameraDistanceScale);
    }

    // if the action is wired & went down this frame; it is shared, so it is
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

    private static void ForgetFruits(GameObject branch) {
        UpwardFruitSensor sensor = UpwardFruitSensor.I;
        if (sensor == null || branch == null) {
            return;
        }
        foreach (Collider part in branch.GetComponentsInChildren<Collider>(true)) {
            sensor.RemoveFruit(part);
        }
    }

    private float SurfaceDistance(float angleA, float yA, float angleB, float yB) {
        float arcU = Mathf.DeltaAngle(angleA, angleB) * Mathf.Deg2Rad
            * trunkRadiusU;
        float dy = yB - yA;
        return Mathf.Sqrt(arcU * arcU + dy * dy);
    }

    private float HorizontalDistance(Vector3 position) {
        Vector3 offset = position - trunkCenter;
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

    // a decor primitive in the body's lit material, no collider
    private GameObject CreatePrimitive(
        PrimitiveType type,
        string objectName,
        Vector3 scale,
        Color color
    ) {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = objectName;
        DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(transform, true);
        part.transform.localScale = scale;
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
    // the banana hangs about this far below its branch root; u
    private const float BANANA_HANG_U = 1f;
    private const int TERRITORY_DOTS = 16;
    private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
    private static readonly int COLOR = Shader.PropertyToID("_Color");

    private enum Phase {
        Waiting, // climbing to the trigger height
        Fighting,
        Finishing, // the slow last blow, either way
        ReviveOffer,
        WatchingAd,
        Defeated, // the beaten spider falls
        Rewarding, // the reward panel, time stopped
        Done, // won, the climb goes on
        Over, // lost w/o a revive
    }

    private enum Mode {
        Idle, // stalks the monkey & attacks
        Repairing, // to a ripped knot near it, then spins it back
        ToLure, // runs to a tossed banana
        Eating,
        Cocooning, // crawls to a trapped monkey
    }

    private enum Attack {
        None,
        WebShot,
        Drag,
        Swipe,
    }

    private struct ArenaItem {
        public GameObject root; // the grabbable fruit
        public GameObject branch;
        public Collider trigger;
        public float backTime; // Time.time it grows back, -1 if hanging
    }

    private struct Projectile {
        public Transform transform;
        public Vector3 target;
    }

    private SpiderKingBody body;
    private BossFightHud hud;
    private MaterialPropertyBlock colorBlock;
    private Transform monkey;
    private MonkeyController monkeyController;
    private CobraMotor greatCobra;
    private CameraClimbFraming cameraFraming;
    private SpiderWebStruggle struggle;
    private Vector3 trunkCenter;
    private float entryY;
    private Vector3 entryPosition;
    private Quaternion entryRotation;

    private Phase phase = Phase.Waiting;
    private Mode mode = Mode.Idle;
    private float modeTimeS;
    private Attack attack = Attack.None;
    private float attackTimeS;
    private float nextAttackTime;
    private Vector3 aimPoint;
    private int warningId = -1;
    private int health;
    private int maxHealth;

    private float startAngle;
    private float startY;
    private float spiderAngle;
    private float spiderY;
    private float stunEndTime;
    private float saveGraceEndTime;
    private float nextRepairTime;
    private int repairIndex = -1;
    private bool isRepairSpinning;

    private readonly List<ArenaItem> knots = new List<ArenaItem>();
    private readonly List<ArenaItem> lureFruits = new List<ArenaItem>();
    private readonly Dictionary<GameObject, Renderer> knotBalls =
        new Dictionary<GameObject, Renderer>();
    private readonly List<SpiderWebTrap> stickyWebs = new List<SpiderWebTrap>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<Renderer> ringDots = new List<Renderer>();

    private Transform lure;
    private bool isLureFlying;
    private float lureAgeS;
    private float lureFromAngle;
    private float lureFromY;
    private float lureFromRadius;
    private float lureTurnDeg;
    private float lureToY;
    private float lureAngle;
    private float lureY;
    private bool hasShownLureHint;

    private bool isDragging;
    private int dragTaps;
    private float dragTimeS;
    private Transform silkLine;
    private bool isCocoonBannerShown;

    private bool isFinisherWin;
    private float finisherTimeS;
    private float reviveLeftS;
    private float adElapsedS;
    private bool isReturningToStart;
    private float defeatTimeS;
}
