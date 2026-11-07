using System;
using UnityEngine;

namespace ShipEnemies
{
    public enum DamageKind { Physical, Acid }
    // Deliberately independent of any weapon, player controller or HUD.
    public sealed class Vitality : MonoBehaviour
    {
        public float maximum = 100;
        [Range(0, 1)] public float acidResistance;
        public bool receivesEnemyAttacks;
        public float aimHeight = .9f;
        public float Current { get; private set; }
        public bool Alive => Current > 0;
        public int Hits { get; private set; }
        public float TotalDamage { get; private set; }
        public event Action<float> Damaged;
        public event Action<Vitality,float> DamagedBy;
        public float InvulnerableUntil {get;set;}
        public event Action Died;
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
        void Awake() => ResetHealth();
        public void Heal(float amount) { if (Alive && amount > 0) Current = Mathf.Min(maximum, Current + amount); }
        public void ResetHealth() { Current = maximum; Hits = 0; TotalDamage = 0; }
        public void Replicate(float current,float max){maximum=max;Current=Mathf.Clamp(current,0,max);}
        public void TakeDamage(float amount, DamageKind kind = DamageKind.Physical, Vitality attacker = null)
        {
            if (SASCoop.CoopSession.Client || !Alive || amount <= 0 || !isActiveAndEnabled || Time.time<InvulnerableUntil) return;
            float actual = amount * (kind == DamageKind.Acid ? 1 - acidResistance : 1);
            if (actual <= 0) return;
            float credited=Mathf.Min(Current,actual);
            Current = Mathf.Max(0, Current - actual); Hits++; TotalDamage += actual;
            if(attacker)DamagedBy?.Invoke(attacker,credited);
            Damaged?.Invoke(actual);
            if (!Alive) Died?.Invoke();
        }
    }
}
