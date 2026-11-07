using UnityEngine;

namespace Onslaught.Mission
{
    public sealed class SupplyPickup : MonoBehaviour
    {
        public bool medical;
        float expires;
        Vector3 basePosition;
        void Start(){expires=Time.time+40;basePosition=transform.position;}
        void Update()
        {
            var mission=MissionDirector.Instance;
            if(mission==null||Time.time>=expires){Destroy(gameObject);return;}
            transform.position=basePosition+Vector3.up*(Mathf.Sin(Time.time*2)*.08f);transform.Rotate(0,40*Time.deltaTime,0);
            var nearest=SASCoop.CoopWorld.Nearest(basePosition,mission.player);
            if(nearest.Alive && Vector3.Distance(nearest.transform.position,basePosition)<1.6f)
            {if(SASCoop.CoopSession.Online)SASCoop.CoopWorld.GrantSupply(nearest,medical);else if(medical)mission.player.Heal(30);else mission.weapons.AddAmmo(24,60);mission.Notify(medical?"MEDKIT  +30 HEALTH":"SUPPLIES  +60 RIFLE / +24 PISTOL",2);Destroy(gameObject);}
        }
    }
}
