using UnityEngine;

/// <summary>
/// Prefab root driving movement along the tree trunk. Vertical input
/// moves the object directly along world Y; horizontal input orbits
/// it around <see cref="treeParent"/>'s root at a fixed radius.
/// </summary>
public class MonkeyPrefabRoot : MonoBehaviour
{
    // Inspector Fields  #######################################################
    [SerializeField]
    private Transform treeParent;
    [SerializeField]
    private float verticalSpeed;
    [SerializeField]
    private float rotationSpeed;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake()
    {
        // Inspector Assignment Guard  -----------------------------------------
        if (treeParent == null)
        {
            Debug.LogWarning("must assign Inspector Field: treeParent", this);
        }

        body = GetComponentInChildren<Rigidbody>();
        if (body == null)
        {
            Debug.LogError("fail to get Component: Rigidbody", this);
        }

        orbitAngleDeg = CalcCurrentOrbitAngle();
    }

    private void FixedUpdate()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        orbitAngleDeg += horizontalInput * rotationSpeed * Time.fixedDeltaTime;

        Vector3 orbitTarget = treeParent.position
                               + CalcOrbitOffset(orbitAngleDeg);
        Vector3 towardOrbit = orbitTarget - body.transform.position;
        towardOrbit.y = 0f;

        Vector3 velocity = towardOrbit / Time.fixedDeltaTime;
        velocity.y = verticalInput * verticalSpeed;

        body.linearVelocity = velocity;

        if (Debug.isDebugBuild)
        {
            Debug.Log($"orbitAngleDeg={orbitAngleDeg:F1} "
                      + $"velocity={velocity}");
        }
    }

    // constants  ##############################################################
    private const float ORBIT_RADIUS = 20.0f;  // fixed dist kept from treeParent's root

    // private members  ########################################################
    private float orbitAngleDeg;

    // cached references  ------------------------------------------------------
    private Rigidbody body;

    // private methods  ########################################################
    private Vector3 CalcOrbitOffset(float angleDeg)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad))
               * ORBIT_RADIUS;
    }

    private float CalcCurrentOrbitAngle()
    {
        Vector3 toSelf = body.transform.position - treeParent.position;
        return Mathf.Atan2(toSelf.x, toSelf.z) * Mathf.Rad2Deg;
    }
}
