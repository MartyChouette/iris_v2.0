using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabPull : MonoBehaviour
{
    public Camera cam;
    public KeyCode grabKey = KeyCode.Mouse0;
    public float grabSpring = 120f;      // pull strength
    public float grabDamper = 18f;       // oppose overshoot
    public float maxAccel = 60f;         // safety cap
    public float maxSpeed = 12f;

    Rigidbody rb;
    bool grabbing;
    Vector3 grabWorld;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetKeyDown(grabKey))
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit) && hit.rigidbody == rb)
            {
                grabbing = true;
                grabWorld = hit.point; // start at hit
            }
        }
        if (Input.GetKeyUp(grabKey))
            grabbing = false;
    }

    void FixedUpdate()
    {
        if (!grabbing) return;

        // project cursor onto a plane through the object COM
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(-cam.transform.forward, rb.worldCenterOfMass);
        if (plane.Raycast(ray, out float enter))
            grabWorld = ray.GetPoint(enter);

        Vector3 toTarget = grabWorld - rb.worldCenterOfMass;
        Vector3 accel = toTarget * grabSpring - rb.linearVelocity * grabDamper;

        if (accel.sqrMagnitude > maxAccel * maxAccel)
            accel = accel.normalized * maxAccel;

        rb.AddForce(accel, ForceMode.Acceleration);

        // speed cap
        if (rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }
}
