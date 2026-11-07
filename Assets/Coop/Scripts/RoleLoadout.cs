using UnityEngine;
using Onslaught.Mission;
namespace SASCoop
{
    public static class RoleLoadout
    {
        public static readonly string[] Names={"DEMO","CAPTAIN","BREACHER","HEAVY"};
        public static readonly string[] Descriptions={"SMG + pistol\nStandard movement speed\nPlants C4 in a full squad", "Assault rifle + pistol\n10% faster movement", "Shotgun + pistol\n5% slower movement\nShotgun penetrates two enemies", "45-round rifle + pistol\n10% slower movement\n10% bonus weapon damage"};
        public static int Selected=>CoopSession.Instance?CoopSession.Instance.LocalRole:1;
        public static float Speed(int role)=>role==1?1.1f:role==2?.95f:role==3?.9f:1;
        public static int Magazine(int role)=>role==2?8:role==3?45:30;
        public static float Damage(int role)=>role==0?18:role==2?70:24;
        public static float Rate(int role)=>role==0?11:role==2?1.35f:8;
        public static float Reload(int role)=>role==2?2.4f:2;
        public static bool CanPlant=>CoopSession.SquadSize<4||Selected==0;
        public static void Apply(PlayerWeapons gun)
        {
            int role=Selected;var rifle=Object.Instantiate(gun.specifications[1]);var pistol=Object.Instantiate(gun.specifications[0]);
            gun.specifications=new[]{pistol,rifle};rifle.displayName=role==0?"KITE / SMG":role==2?"BREACH / SHOTGUN":role==3?"BULWARK / 45":"MANTIS / RIFLE";
            rifle.damage=Damage(role);rifle.magazineSize=Magazine(role);rifle.shotsPerSecond=Rate(role);rifle.reloadSeconds=Reload(role);rifle.automatic=role!=2;rifle.penetration=role==2?2:1;
            if(role==3){pistol.damage*=1.1f;rifle.damage*=1.1f;}
            if(gun.vaccine)gun.vaccine.walkSpeed=5*Speed(role);else gun.explorer.walkSpeed=4.5f*Speed(role);
            if(role==0)gun.models[1].transform.localScale=new Vector3(.9f,.9f,.75f);
            if(role==2){gun.models[1].transform.localScale=new Vector3(1.1f,1.1f,1.12f);CoopWorld.Shape(gun.models[1].transform,PrimitiveType.Cube,new Vector3(0,-.05f,.25f),new Vector3(.075f,.07f,.3f),new Color(.23f,.18f,.12f));}
            if(role==3)gun.magazines[1].localScale=new Vector3(1.15f,1.4f,1.15f);
        }
    }
}
