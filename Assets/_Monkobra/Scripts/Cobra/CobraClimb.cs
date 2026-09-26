using UnityEngine;

public class CobraClimb: MonoBehaviour {
    [Header("References")]
    [SerializeField]
    private Transform pathCenter;

    [SerializeField]
    private Transform[] bodySegments;

    [Header("Tree Path")]
    [SerializeField]
    private float orbitRadius = 5.8f;

    [SerializeField]
    private float startHeight = 0.5f;

    [SerializeField]
    private float climbSpeed = 0.25f;

    [SerializeField]
    private float orbitSpeedDegrees = 20f;

    [SerializeField]
    private bool clockwise = true;

    [Header("Snake Shape")]
    [SerializeField]
    private float segmentAngleSpacing = 7f;

    [SerializeField]
    private float segmentHeightSpacing = 0.06f;

    private float headAngle;
    private float headHeight;

    [Header("Player Tracking")]
    [SerializeField] private bool limitToPlayerHeight = true;
    [SerializeField] private float slowDownDistance = 2f;
    [SerializeField] private float minimumHeightGap = 0f;

private Transform player;

    private void Start() {
        if (pathCenter == null) {
            Debug.LogError("CobraClimb requires a Path Center.", this);
            enabled = false;
            return;
        }

        headHeight = startHeight;
        UpdateBody();
    }

    private void Update() {
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        float direction = clockwise ? -1f : 1f;

        
        headAngle += direction * orbitSpeedDegrees * Time.deltaTime;

        float currentClimbSpeed = climbSpeed;

        if (limitToPlayerHeight && player != null)
        {
            float maximumWorldHeight =
                player.position.y - minimumHeightGap;

            float maximumLocalHeight =
                maximumWorldHeight - pathCenter.position.y;

            float remainingDistance =
                maximumLocalHeight - headHeight;

            if (remainingDistance <= 0f)
            {
                // reach monkey, stop
                currentClimbSpeed = 0f;
            }
            else if (remainingDistance < slowDownDistance)
            {
                // approcah monkey, speed decrease
                float speedMultiplier = Mathf.Lerp(
                    0.15f,
                    1f,
                    remainingDistance / slowDownDistance
                );

                currentClimbSpeed *= speedMultiplier;
            }

            headHeight += currentClimbSpeed * Time.deltaTime;

            // No exceed monkey
            headHeight = Mathf.Min(
                headHeight,
                maximumLocalHeight
            );
        }
        else
        {
            headHeight += currentClimbSpeed * Time.deltaTime;
        }

        UpdateBody();
    }

    private void UpdateBody() {
        if (bodySegments == null) {
            return;
        }

        float direction = clockwise ? -1f : 1f;

        for (int i = 0; i < bodySegments.Length; i++) {
            Transform segment = bodySegments[i];

            if (segment == null) {
                continue;
            }

            float angle = headAngle - direction * segmentAngleSpacing * i;

            float height = headHeight - segmentHeightSpacing * i;

            segment.position = GetPathPosition(angle, height);

            Vector3 nextPosition = GetPathPosition(
                angle + direction,
                height + 0.01f
            );

            Vector3 movementDirection = nextPosition - segment.position;

            if (movementDirection.sqrMagnitude > 0.001f) {
                segment.rotation = Quaternion.LookRotation(
                    movementDirection,
                    Vector3.up
                );
            }
        }
    }

    private Vector3 GetPathPosition(float angleDegrees, float height) {
        float radians = angleDegrees * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            height,
            Mathf.Sin(radians) * orbitRadius
        );

        return pathCenter.position + offset;
    }
}
