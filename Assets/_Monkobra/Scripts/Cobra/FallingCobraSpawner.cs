using UnityEngine;

public class FallingCobraSpawner: MonoBehaviour {
    [Header("References")]
    [SerializeField] private FallingCobra fallingCobraPrefab;

    [Header("When to Spawn")]
    [SerializeField, Range(0f, 1f)]
    private float startProgress = 0.6f;

    [SerializeField] private float spawnIntervalS = 8f;
    [SerializeField] private float retryIntervalS = 1f;

    [Header("Branch Selection")]
    [SerializeField] private float minBranchAbovePlayerU = 4f;
    [SerializeField] private float maxBranchAbovePlayerU = 9f;
    [SerializeField] private float maxSideDistanceU = 3f;
    [SerializeField] private float heightAboveBranchU = 0.5f;

    private Transform player;
    private FallingCobra activeCobra;
    private float nextSpawnTime;

    private void Awake() {
        if (fallingCobraPrefab == null) {
            Debug.LogError(
                "FallingCobraSpawner:\tmust assign Inspector Field: "
                    + "fallingCobraPrefab",
                this
            );
        }
    }

    private void Start() {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) {
            player = playerObject.transform;
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
            || TreeManager.I == null
            || fallingCobraPrefab == null
            || activeCobra != null
            || Time.time < nextSpawnTime
        ) {
            return;
        }

        float progress = Mathf.InverseLerp(
            TreeManager.I.MinY,
            TreeManager.I.MaxY,
            player.position.y
        );
        if (progress < startProgress) {
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
        nextSpawnTime = Time.time + spawnIntervalS;
    }

    private bool TryFindBranchSpawn(out Vector3 spawnPosition) {
        spawnPosition = Vector3.zero;

        Vector3 treeCenter = TreeManager.I.transform.position;
        float playerRadius = new Vector2(
            player.position.x - treeCenter.x,
            player.position.z - treeCenter.z
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
                branch.transform.position - treeCenter;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) {
                continue;
            }

            // 枝條比猴子的繞樹路徑更靠外；
            // 將落蛇位置移到相同角度的猴子路徑上。
            Vector3 pathPosition =
                treeCenter + direction.normalized * playerRadius;
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
        spawnIntervalS = Mathf.Max(0f, spawnIntervalS);
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