using UnityEngine;

// When the player enters the trigger, teleport them to the destination (prototype portal).
public class PortalTrigger : MonoBehaviour
{
    public Vector3 destination = new Vector3(110f, 1f, -20f);

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerIsoController>();
        if (pc != null)
        {
            pc.transform.position = destination;
        }
    }
}
