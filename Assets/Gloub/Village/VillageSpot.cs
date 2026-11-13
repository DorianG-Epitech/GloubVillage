using UnityEngine;

namespace Gloub.Village
{
    public abstract class VillageSpot : MonoBehaviour
    {
        public Transform Approach;
        public Vector3 ApproachPoint => Approach != null ? Approach.position : transform.position;
    }
}
