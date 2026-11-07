using System.Collections.Generic;
using UnityEngine;
using ShipEnemies;

namespace ShipHUD
{
    // Optional contact adapter for preview actors or future non-EnemyBrain hostiles.
    public sealed class MinimapContact : MonoBehaviour
    {
        public Vitality health;
        public bool boss;
        public bool Visible => isActiveAndEnabled && (health == null || health.Alive);
        public static readonly HashSet<MinimapContact> Active = new HashSet<MinimapContact>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => Active.Clear();
        void Awake() { if (health == null) health = GetComponent<Vitality>(); }
        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
    }
}
