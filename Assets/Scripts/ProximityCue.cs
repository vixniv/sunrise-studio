using UnityEngine;

public class DoorProximityCue : MonoBehaviour
{
    [SerializeField] private GameObject cue;

    private void Awake()
    {
        if (cue != null)
            cue.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMove>() != null
            && cue != null)
        {
            cue.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMove>() != null
            && cue != null)
        {
            cue.SetActive(false);
        }
    }
}