using UnityEngine;

namespace Onslaught.Mission
{
    public sealed partial class MissionDirector
    {
        public int PressureTier { get; private set; }
        public float AdaptiveSpawnMultiplier => 1 + .15f * PressureTier;
        public int AdaptiveLivingLimit => Mathf.Min(56, Mathf.CeilToInt(tuning.maximumLiving *
            SASCoop.CoopSession.InitialPopulationFor(SASCoop.CoopSession.SquadSize)) + 2 * PressureTier);
        int nextPressureMinute = 1;

        void UpdateAdaptivePressure()
        {
            if (!Spawning || !SASCoop.CoopSession.Authority) return;
            float weakest = 1;
            if (gates == null || gates.Length == 0) weakest = 0;
            else foreach (var gate in gates)
                weakest = Mathf.Min(weakest, gate && !gate.Broken && gate.Health
                    ? gate.Health.Current / Mathf.Max(1, gate.Health.maximum) : 0);

            int before = PressureTier;
            // A damaged/breached lane immediately cancels the extra pressure.
            // Existing enemies stay alive; only future arrivals are eased.
            if (weakest <= .5f) PressureTier = 0;
            if (Phase == MissionPhase.Defense && nextPressureMinute <= 3 &&
                DefenseElapsed >= nextPressureMinute * 60)
            {
                int minute = nextPressureMinute++;
                if (minute == 1 && weakest > .9f) PressureTier = 1;
                // Minute two retains the earned tier only while both gates hold.
                if (minute == 3 && weakest > .5f) PressureTier = Mathf.Min(2, PressureTier + 1);
            }
            if (PressureTier != before)
                Notify(PressureTier > before
                    ? "DEFENSE HOLDING / INFECTED PRESSURE +" + (PressureTier * 15) + "%"
                    : "GATE UNDER PRESSURE / EXTRA REINFORCEMENTS EASED", 5);
        }
    }
}
