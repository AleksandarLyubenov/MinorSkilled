using System.Collections.Generic;
using UnityEngine;

public static class SurfaceResolver
{
    static readonly Collider[] _buf = new Collider[16];

    public static SurfaceProfile Resolve(Vector3 pos, float probeRadius, LayerMask triggerMask, Collider ground)
    {
        // Any overlapping SurfaceVolume triggers?
        int count = Physics.OverlapSphereNonAlloc(pos, probeRadius, _buf, triggerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var vol = _buf[i]?.GetComponentInParent<SurfaceVolume>();
            if (vol != null && vol.profile != null) return vol.profile;
        }

        // SurfaceTag on ground collider chain
        if (ground != null)
        {
            var tag = ground.GetComponentInParent<SurfaceTag>();
            if (tag != null && tag.profile != null) return tag.profile;
        }

        return null;
    }
}
