using UnityEngine;

public class FallingCobraSpawner: MonoBehaviour {
    [Header("References")]
    [SerializeField] private FallingCobra fallingCobraPrefab;
    [SerializeField] private Transform treeCenter;

    [Header("When to Spawn")]
    [SerializeField]
    [Tooltip("Vertical distance climbed from the player's starting height before falling cobras can spawn; u")]
    private float startClimbHeightU = 60f;

    [SerializeField] private float initialSpawnIntervalS = 8f;
    [SerializeField] private float minSpawnIntervalS = 4f;
    [SerializeField]
    [Tooltip("Additional climb after falling cobras unlock before the minimum spawn interval is reached; u")]
    private float climbDistanceToMinIntervalU = 200f;
    [SerializeField] private float retryIntervalS = 1f;

    [Header("Branch Selection")]
    [SerializeField] private float minBranchAbovePlayerU = 4f;
    [SerializeField] private float maxBranchAbovePlayerU = 9f;
    [SerializeField] private float maxSideDistanceU = 3f;
    [SerializeField] private float heightAboveBranchU = 0.5f;

    private Transform player;
    private FallingCobra activeCobra;
    private float playerStartY;
    private float highestClimbHeightU;
    private bool spawningUnlocked;
    private float nextSpawnTime;

    private void Awake() {
        if (fallingCobraPrefab == null) {
            Debug.LogError(
                "FallingCobraSpawner:\tmust assign Inspector Field: "
                    + "fallingCobraPrefab",
                this
            );
        }
        if (treeCenter == null) {
            Debug.LogError(
                "FallingCobraSpawner:\tmust assign Inspector Field: "
                    + "treeCenter",
                this
            );
        }
    }

    private void Start() {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) {
            player = playerObject.transform;
            playerStartY = player.position.y;
        } else {
            Debug.LogError(
                "FallingCobraSpawner:\tPlayer not found",
                this
            );
        }
    }

    private void Update() {
        if (
            player == null
            || treeCenter == null
            || fallingCobraPrefab == null
        ) {
            return;
        }

        highestClimbHeightU = Mathf.Max(
            highestClimbHeightU,
            player.position.y - playerStartY
        );
        if (!spawningUnlocked) {
            if (highestClimbHeightU < startClimbHeightU) {
                return;
            }
            spawningUnlocked = true;
        }

        if (activeCobra != null || Time.time < nextSpawnTime) {
            return;
        }

        if (!TryFindBranchSpawn(out Vector3 spawnPosition)) {
            nextSpawnTime = Time.time + retryIntervalS;
            return;
        }

        activeCobra = Instantiate(
            fallingCobraPrefab,
            spawnPosition,
            Quaternion.identity
        );
        float climbAfterUnlockU =
            highestClimbHeightU - startClimbHeightU;
        float difficulty = Mathf.Clamp01(
            climbAfterUnlockU / climbDistanceToMinIntervalU
        );
        float currentIntervalS = Mathf.Lerp(
            initialSpawnIntervalS,
            minSpawnIntervalS,
            difficulty
        );
        nextSpawnTime = Time.time + currentIntervalS;
    }

    private bool TryFindBranchSpawn(out Vector3 spawnPosition) {
        spawnPosition = Vector3.zero;

        Vector3 treeCenterPosition = treeCenter.position;
        float playerRadius = new Vector2(
            player.position.x - treeCenterPosition.x,
            player.position.z - treeCenterPosition.z
        ).magnitude;

        float bestSideDistanceSqr =
            maxSideDistanceU * maxSideDistanceU;
        bool found = false;

        foreach (
            GameObject branch in GameObject.FindGameObjectsWithTag("Branch")
        ) {
            float heightAbovePlayer =
                branch.transform.position.y - player.position.y;
            if (
                heightAbovePlayer < minBranchAbovePlayerU
                || heightAbovePlayer > maxBranchAbovePlayerU
            ) {
                continue;
            }

            Vector3 direction =
                branch.transform.position - treeCenterPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) {
                continue;
            }

            // 枝條比猴子的繞樹路徑更靠外；
            // 將落蛇位置移到相同角度的猴子路徑上。
            Vector3 pathPosition =
                treeCenterPosition + direction.normalized * playerRadius;
            float sideDistanceSqr =
                new Vector2(
                    pathPosition.x - player.position.x,
                    pathPosition.z - player.position.z
                ).sqrMagnitude;

            if (sideDistanceSqr > bestSideDistanceSqr) {
                continue;
            }

            bestSideDistanceSqr = sideDistanceSqr;
            spawnPosition = new Vector3(
                pathPosition.x,
                branch.transform.position.y + heightAboveBranchU,
                pathPosition.z
            );
            found = true;
        }

        return found;
    }

    private void OnValidate() {
        startClimbHeightU = Mathf.Max(0f, startClimbHeightU);
        initialSpawnIntervalS =
            Mathf.Max(0.1f, initialSpawnIntervalS);
        minSpawnIntervalS = Mathf.Clamp(
            minSpawnIntervalS,
            0.1f,
            initialSpawnIntervalS
        );
        climbDistanceToMinIntervalU =
            Mathf.Max(0.1f, climbDistanceToMinIntervalU);
        retryIntervalS = Mathf.Max(0.1f, retryIntervalS);
        minBranchAbovePlayerU =
            Mathf.Max(0f, minBranchAbovePlayerU);
        maxBranchAbovePlayerU = Mathf.Max(
            minBranchAbovePlayerU,
            maxBranchAbovePlayerU
        );
        maxSideDistanceU = Mathf.Max(0f, maxSideDistanceU);
        heightAboveBranchU = Mathf.Max(0f, heightAboveBranchU);
    }
}
