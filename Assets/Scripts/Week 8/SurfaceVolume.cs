using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SurfaceVolume : MonoBehaviour
{
    public SurfaceProfile profile;

    void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }
}
