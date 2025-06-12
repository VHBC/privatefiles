using UnityEngine;

public class ObjectPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 5f;
    public float holdDistance = 2.5f;
    public float pickupForce = 800f;
    public float dampingForce = 300f;
    public float rotationForce = 500f;
    public float rotationDamping = 50f;
    public float maxCarryMass = 50f;
    public LayerMask pickupLayerMask = -1;

    [Header("Input")]
    public KeyCode pickupKey = KeyCode.E;
    public KeyCode throwKey = KeyCode.Mouse0;

    [Header("Throw Settings")]
    public float throwForce = 500f;
    public float maxThrowForce = 1000f;

    [Header("UI")]
    public bool showPickupPrompt = true;
    public string pickupPromptText = "Press E to pick up";

    [Header("Collision Fix Options")]
    [Tooltip("Method to prevent player-object collision")]
    public CollisionFixMethod collisionFixMethod = CollisionFixMethod.IgnoreCollision;

    public enum CollisionFixMethod
    {
        IgnoreCollision,    // Ignore collision between player and held object
        DisableCollider,    // Temporarily disable object's collider
        ChangeLayer,        // Move object to non-colliding layer
        MakeKinematic      // Make object kinematic while held
    }

    [Header("Layer Settings (for ChangeLayer method)")]
    public int heldObjectLayer = 8; // Layer for held objects that doesn't collide with player

    private Camera playerCamera;
    private Rigidbody heldObject;
    private Vector3 holdPosition;
    private float holdDistance_current;
    private Rigidbody lastLookedAtObject;
    private bool isHoldingObject = false;

    // For smooth object movement
    private Vector3 targetPosition;
    private float holdSmoothing = 10f;

    // Store original properties for restoration
    private bool originalUseGravity;
    private float originalDrag;
    private float originalAngularDrag;
    private bool originalIsKinematic;
    private int originalLayer;
    private Collider objectCollider;
    private Collider playerCollider;

    void Start()
    {
        // Get the camera component
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
        {
            Debug.LogError("No camera found! Please assign a camera to the player.");
        }

        // Get player's collider for collision ignoring
        playerCollider = GetComponent<Collider>();
        if (playerCollider == null)
            playerCollider = GetComponentInChildren<Collider>();
    }

    void Update()
    {
        if (playerCamera == null) return;

        HandleInput();

        if (isHoldingObject && heldObject != null)
        {
            UpdateHeldObject();
        }
        else
        {
            CheckForPickupTarget();
        }
    }

    void HandleInput()
    {
        // Pickup/Drop object
        if (Input.GetKeyDown(pickupKey))
        {
            if (isHoldingObject)
            {
                DropObject();
            }
            else
            {
                TryPickupObject();
            }
        }

        // Throw object
        if (Input.GetKeyDown(throwKey) && isHoldingObject)
        {
            ThrowObject();
        }

        // Adjust hold distance with scroll wheel
        if (isHoldingObject)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                holdDistance_current += scroll * 2f;
                holdDistance_current = Mathf.Clamp(holdDistance_current, 1f, pickupRange);
            }
        }
    }

    void CheckForPickupTarget()
    {
        RaycastHit hit;
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

        if (Physics.Raycast(ray, out hit, pickupRange, pickupLayerMask))
        {
            Rigidbody rb = hit.collider.GetComponent<Rigidbody>();

            if (rb != null && CanPickupObject(rb))
            {
                lastLookedAtObject = rb;
                return;
            }
        }

        lastLookedAtObject = null;
    }

    bool CanPickupObject(Rigidbody rb)
    {
        // Check if object is too heavy
        if (rb.mass > maxCarryMass)
            return false;

        // Check if object has the pickup tag (optional)
        if (rb.gameObject.CompareTag("NoPickup"))
            return false;

        // Check if object is kinematic
        if (rb.isKinematic)
            return false;

        return true;
    }

    void TryPickupObject()
    {
        if (lastLookedAtObject != null)
        {
            PickupObject(lastLookedAtObject);
        }
    }

    void PickupObject(Rigidbody rb)
    {
        heldObject = rb;
        isHoldingObject = true;
        holdDistance_current = holdDistance;
        objectCollider = heldObject.GetComponent<Collider>();

        // Store original physics properties
        originalUseGravity = heldObject.useGravity;
        originalDrag = heldObject.drag;
        originalAngularDrag = heldObject.angularDrag;
        originalIsKinematic = heldObject.isKinematic;
        originalLayer = heldObject.gameObject.layer;

        // Apply collision fix based on selected method
        ApplyCollisionFix();

        // Set physics properties for holding
        if (collisionFixMethod != CollisionFixMethod.MakeKinematic)
        {
            heldObject.useGravity = false;
            heldObject.drag = 1f;
            heldObject.angularDrag = 5f;
        }

        // Zero out any existing velocity for clean pickup
        heldObject.velocity = Vector3.zero;
        heldObject.angularVelocity = Vector3.zero;

        // Start the object closer to prevent initial large displacement
        Vector3 rayDirection = playerCamera.transform.forward;
        Vector3 startPosition = playerCamera.transform.position + rayDirection * Mathf.Min(holdDistance_current,
            Vector3.Distance(playerCamera.transform.position, heldObject.transform.position));

        // Smoothly move to start position to prevent flinging
        heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, startPosition, 0.3f);

        // Set initial rotation to face the camera but keep upright
        Vector3 directionToCamera = (playerCamera.transform.position - heldObject.transform.position).normalized;
        Vector3 forwardDirection = -directionToCamera;
        forwardDirection.y = 0; // Remove vertical component to keep upright
        forwardDirection.Normalize();

        Quaternion initialRotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
        heldObject.transform.rotation = Quaternion.Slerp(heldObject.transform.rotation, initialRotation, 0.5f);

        targetPosition = playerCamera.transform.position + rayDirection * holdDistance_current;

        Debug.Log($"Picked up: {heldObject.name}");
    }

    void ApplyCollisionFix()
    {
        switch (collisionFixMethod)
        {
            case CollisionFixMethod.IgnoreCollision:
                if (playerCollider != null && objectCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, objectCollider, true);
                }
                break;

            case CollisionFixMethod.DisableCollider:
                if (objectCollider != null)
                {
                    objectCollider.enabled = false;
                }
                break;

            case CollisionFixMethod.ChangeLayer:
                heldObject.gameObject.layer = heldObjectLayer;
                break;

            case CollisionFixMethod.MakeKinematic:
                heldObject.isKinematic = true;
                heldObject.useGravity = false;
                break;
        }
    }

    void RemoveCollisionFix()
    {
        switch (collisionFixMethod)
        {
            case CollisionFixMethod.IgnoreCollision:
                if (playerCollider != null && objectCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, objectCollider, false);
                }
                break;

            case CollisionFixMethod.DisableCollider:
                if (objectCollider != null)
                {
                    objectCollider.enabled = true;
                }
                break;

            case CollisionFixMethod.ChangeLayer:
                heldObject.gameObject.layer = originalLayer;
                break;

            case CollisionFixMethod.MakeKinematic:
                heldObject.isKinematic = originalIsKinematic;
                break;
        }
    }

    void UpdateHeldObject()
    {
        if (heldObject == null)
        {
            isHoldingObject = false;
            return;
        }

        // Calculate target position
        Vector3 rayDirection = playerCamera.transform.forward;
        Vector3 desiredPosition = playerCamera.transform.position + rayDirection * holdDistance_current;

        targetPosition = desiredPosition;

        if (collisionFixMethod == CollisionFixMethod.MakeKinematic)
        {
            // For kinematic objects, directly move them
            heldObject.MovePosition(Vector3.Lerp(heldObject.transform.position, targetPosition, Time.deltaTime * holdSmoothing));

            // Handle rotation for kinematic objects
            Vector3 directionToCamera = (playerCamera.transform.position - heldObject.transform.position).normalized;
            Vector3 forwardDirection = -directionToCamera;
            forwardDirection.y = 0;
            forwardDirection.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
            heldObject.MoveRotation(Quaternion.Slerp(heldObject.transform.rotation, targetRotation, Time.deltaTime * 5f));
        }
        else
        {
            // Use physics forces for non-kinematic objects
            Vector3 displacement = targetPosition - heldObject.transform.position;
            float massScaler = Mathf.Clamp(1f / heldObject.mass, 0.1f, 2f);

            Vector3 springForce = displacement * pickupForce * massScaler;
            Vector3 dampingForceVector = -heldObject.velocity * dampingForce * massScaler;
            Vector3 totalForce = springForce + dampingForceVector;

            totalForce = Vector3.ClampMagnitude(totalForce, pickupForce * 2f * massScaler);
            heldObject.AddForce(totalForce, ForceMode.Force);

            // Handle rotation with physics
            Vector3 directionToCamera = (playerCamera.transform.position - heldObject.transform.position).normalized;
            Vector3 forwardDirection = -directionToCamera;
            forwardDirection.y = 0;
            forwardDirection.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
            Quaternion rotationDifference = targetRotation * Quaternion.Inverse(heldObject.transform.rotation);

            rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            Vector3 rotationalForce = axis * angle * rotationForce * massScaler * Mathf.Deg2Rad;
            Vector3 rotationalDamping = -heldObject.angularVelocity * rotationDamping * massScaler;
            Vector3 totalTorque = rotationalForce + rotationalDamping;

            heldObject.AddTorque(totalTorque, ForceMode.Force);
        }

        // Check if object is too far away (dropped accidentally)
        float distanceToPlayer = Vector3.Distance(transform.position, heldObject.transform.position);
        if (distanceToPlayer > pickupRange * 2f)
        {
            DropObject();
        }
    }

    void DropObject()
    {
        if (heldObject != null)
        {
            // Remove collision fix
            RemoveCollisionFix();

            // Restore original physics properties
            heldObject.useGravity = originalUseGravity;
            heldObject.drag = originalDrag;
            heldObject.angularDrag = originalAngularDrag;
            heldObject.isKinematic = originalIsKinematic;

            Debug.Log($"Dropped: {heldObject.name}");
            heldObject = null;
        }

        isHoldingObject = false;
        objectCollider = null;
    }

    void ThrowObject()
    {
        if (heldObject != null)
        {
            // Remove collision fix
            RemoveCollisionFix();

            // Restore physics properties
            heldObject.useGravity = originalUseGravity;
            heldObject.drag = originalDrag;
            heldObject.angularDrag = originalAngularDrag;
            heldObject.isKinematic = originalIsKinematic;

            // Calculate throw force based on current velocity and camera direction
            Vector3 throwDirection = playerCamera.transform.forward;
            float currentSpeed = heldObject.velocity.magnitude;
            float finalThrowForce = Mathf.Clamp(throwForce + currentSpeed * 50f, throwForce, maxThrowForce);

            // Apply throw force
            heldObject.velocity = throwDirection * finalThrowForce / heldObject.mass;

            Debug.Log($"Threw: {heldObject.name} with force: {finalThrowForce}");
            heldObject = null;
        }

        isHoldingObject = false;
        objectCollider = null;
    }

    // Optional: GUI for pickup prompt
    void OnGUI()
    {
        if (!showPickupPrompt) return;

        if (lastLookedAtObject != null && !isHoldingObject)
        {
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 50, 200, 30),
                     pickupPromptText, new GUIStyle() { alignment = TextAnchor.MiddleCenter });
        }

        if (isHoldingObject && heldObject != null)
        {
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 50, 200, 30),
                     $"Holding: {heldObject.name}\nScroll to adjust distance\nClick to throw",
                     new GUIStyle() { alignment = TextAnchor.MiddleCenter });
        }
    }

    // Debug visualization
    void OnDrawGizmos()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * pickupRange);

            if (isHoldingObject && heldObject != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(targetPosition, 0.2f);
                Gizmos.DrawLine(playerCamera.transform.position, heldObject.transform.position);
            }
        }
    }
}