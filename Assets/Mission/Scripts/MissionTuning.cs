using UnityEngine;

namespace Onslaught.Mission
{
    [CreateAssetMenu(menuName="Onslaught/Mission tuning")]
    public sealed class MissionTuning : ScriptableObject
    {
        public float defenseSeconds=240, bossArrivalDelay=4, gateHealth=420, playerHealth=120;
        public int maximumLiving=28;
        public float firstSpawnDelay=6, initialSpawnInterval=3.2f, finalSpawnInterval=2.2f, bossSpawnInterval=3.1f;
        public float rampSealSeconds=3, flightSeconds=7;
    }
}
