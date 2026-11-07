using UnityEngine;

namespace ShipEnemies
{
    public enum EnemyKind { Shambler, Sprinter, Spitter, Bloater, Regurgitator, Worm, PukeWorm }
    [CreateAssetMenu(menuName = "Ship/Enemy definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public EnemyKind kind;
        public string displayName;
        [Min(1)] public float health = 95;
        [Min(.1f)] public float speed = 1.7f;
        public float turnSpeed = 360, radius = .35f, height = 1.9f;
        public float meleeDamage = 10, meleeRange = 1.2f, meleeCooldown = 1.2f;
        public float meleeWindup = .42f, meleeDuration = .9f;
        public float rangedRange, rangedCooldown = 2.7f, rangedWindup = .6f, rangedDuration = 1.3f;
        public float acidImpactDamage = 8, acidDamagePerSecond = 6, acidRadius = 1.3f, acidLifetime = 5;
        [Range(0, 1)] public float chemicalResistance;
        public int deathParasites, vomitParasites;
        public EnemyBrain parasitePrefab;
        public AcidProjectile projectilePrefab;
        public AcidPool poolPrefab;
    }
}
