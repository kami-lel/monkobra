using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prefab root driving movement along the tree trunk. Vertical input
/// moves the object directly along world Y; horizontal input orbits
/// it around this object's own parent's root at a fixed radius, so
/// motion stays relative to wherever the prefab is parented.
/// </summary>
public class MonkeyPrefabRoot : MonoBehaviour
{
    // Inspector Fields  #######################################################
    [Tooltip("climb speed along world Y, in units/s")]
    [SerializeField]
    private float verticalSpeed;
    [Tooltip("orbit speed around this object's parent, in deg/s")]
    [SerializeField]
    private float rotationSpeed;
    [Tooltip("directional input action driving climb and orbit, a Vector2 "
             + "of horizontal (orbit) and vertical (climb) axes")]
    [SerializeField]
    private InputActionReference moveAction;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake()
    {
        // Inspector Assignment Guard  -----------------------------------------
        if (moveAction == null)
        {
            Debug.LogWarning("must assign Inspector Field: moveAction", this);
        }

        if (transform.parent == null)
        {
            Debug.LogError("fail to get Transform: parent", this);
        }

        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            Debug.LogError("fail to get Component: Rigidbody", this);
        }

        orbitAngleDeg = CalcCurrentOrbitAngle();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
    }

    private void FixedUpdate()
    {
        Vector2 directionalInput = moveAction.action.ReadValue<Vector2>();

        orbitAngleDeg -= directionalInput.x * rotationSpeed * Time.fixedDeltaTime;

        Vector3 orbitTarget = transform.parent.position
                               + CalcOrbitOffset(orbitAngleDeg);
        Vector3 towardOrbit = orbitTarget - transform.position;
        towardOrbit.y = 0f;

        Vector3 velocity = towardOrbit / Time.fixedDeltaTime;
        velocity.y = directionalInput.y * verticalSpeed;

        body.linearVelocity = velocity;
    }

    // constants  ##############################################################
    private const float ORBIT_RADIUS = 20.0f;  // fixed dist kept from parent's root

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
        Vector3 toSelf = transform.position - transform.parent.position;
        return Mathf.Atan2(toSelf.x, toSelf.z) * Mathf.Rad2Deg;
    }
}
