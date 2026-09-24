using UnityEngine;

public class CobraClimb : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform pathCenter;
    [SerializeField] private Transform[] bodySegments;

    [Header("Tree Path")]
    [SerializeField] private float orbitRadius = 5.8f;
    [SerializeField] private float startHeight = 0.5f;
    [SerializeField] private float climbSpeed = 0.25f;
    [SerializeField] private float orbitSpeedDegrees = 20f;
    [SerializeField] private bool clockwise = true;

    [Header("Snake Shape")]
    [SerializeField] private float segmentAngleSpacing = 7f;
    [SerializeField] private float segmentHeightSpacing = 0.06f;

    private float headAngle;
    private float headHeight;

    private void Start()
    {
        if (pathCenter == null)
        {
            Debug.LogError("CobraClimb requires a Path Center.", this);
            enabled = false;
            return;
        }

        headHeight = startHeight;
        UpdateBody();
    }

    private void Update()
    {
        float direction = clockwise ? -1f : 1f;

        headAngle += direction * orbitSpeedDegrees * Time.deltaTime;
        headHeight += climbSpeed * Time.deltaTime;

        UpdateBody();
    }

    private void UpdateBody()
    {
        if (bodySegments == null)
        {
            return;
        }

        float direction = clockwise ? -1f : 1f;

        for (int i = 0; i < bodySegments.Length; i++)
        {
            Transform segment = bodySegments[i];

            if (segment == null)
            {
                continue;
            }

            float angle =
                headAngle - direction * segmentAngleSpacing * i;

            float height =
                headHeight - segmentHeightSpacing * i;

            segment.position = GetPathPosition(angle, height);

            Vector3 nextPosition =
                GetPathPosition(angle + direction, height + 0.01f);

            Vector3 movementDirection =
                nextPosition - segment.position;

            if (movementDirection.sqrMagnitude > 0.001f)
            {
                segment.rotation = Quaternion.LookRotation(
                    movementDirection,
                    Vector3.up
                );
            }
        }
    }

    private Vector3 GetPathPosition(float angleDegrees, float height)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            height,
            Mathf.Sin(radians) * orbitRadius
        );

        return pathCenter.position + offset;
    }
}