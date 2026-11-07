using UnityEngine;

namespace Onslaught.Mission
{
    [CreateAssetMenu(menuName="Onslaught/Weapon specification")]
    public sealed class WeaponSpec : ScriptableObject
    {
        public string displayName;
        public bool automatic;
        public float damage, headMultiplier=2, shotsPerSecond, reloadSeconds, range=90, recoilDegrees=1.1f;
        public int penetration=1;
        public int magazineSize, startingReserve, maximumReserve;
        public AudioClip shot, reload;
    }
}
