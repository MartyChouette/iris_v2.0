using UnityEngine;
using UnityEngine.Events;
using System.Collections; // Required for Coroutines

public class ScissorStation : MonoBehaviour
{
    [Header("Objects")]
    public GameObject scissorsModel; // The prop on the table
    public Collider returnMatCollider; // The mat collider

    [Header("Connections")]
    public UnityEvent onEquipScissors;
    public UnityEvent onUnequipScissors;

    // Track state
    private bool isEquipped = false;
    // Track if we are in the middle of a transition (prevent spam clicking)
    private bool isBusy = false;

    void Update()
    {
        // If we are currently switching, ignore inputs
        if (isBusy) return;

        if (Input.GetMouseButtonDown(0))
        {
            CheckClick();
        }
    }

    void CheckClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // Case 1: Pick Up
            if (hit.collider.gameObject == scissorsModel && !isEquipped)
            {
                StartCoroutine(EquipRoutine());
            }
            // Case 2: Put Down
            else if (hit.collider == returnMatCollider && isEquipped)
            {
                Unequip();
            }
        }
    }

    // This Coroutine handles the "Safety Delay"
    IEnumerator EquipRoutine()
    {
        isBusy = true; // Lock input

        // 1. Hide the table scissors immediately so it feels responsive
        scissorsModel.SetActive(false);

        // 2. WAIT! This gives your finger time to lift off the mouse button.
        // It also ensures the "Click" that picked up the object is NOT read as a "Cut"
        yield return new WaitForSeconds(0.15f);

        // 3. Now it is safe to turn on the cutter
        isEquipped = true;
        onEquipScissors.Invoke();

        isBusy = false; // Unlock input
    }

    void Unequip()
    {
        isEquipped = false;

        // Turn off the cutter
        onUnequipScissors.Invoke();

        // Show the table scissors
        scissorsModel.SetActive(true);
    }
}