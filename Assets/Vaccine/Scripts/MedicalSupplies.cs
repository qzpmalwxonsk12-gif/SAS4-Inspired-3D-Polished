using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShipEnemies;
namespace Vaccine
{
    public static class MedicalSupplies
    {
        [Serializable]public class Entry {public string key;public ulong[] users;}
        static readonly Dictionary<string,HashSet<ulong>> used=new Dictionary<string,HashSet<ulong>>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]public static void Reset()=>used.Clear();
        public static ulong LocalId=>SASCoop.CoopSession.Online?SASCoop.CoopSession.Instance.net.LocalClientId:0;
        public static bool Used(string key,ulong id)=>used.TryGetValue(key,out var players)&&players.Contains(id);
        public static bool Take(string key,ulong id,Vitality hp)
        {
            if(SASCoop.CoopSession.Client||string.IsNullOrEmpty(key)||!hp||!hp.Alive||hp.Current>=hp.maximum||Used(key,id))return false;
            if(!used.TryGetValue(key,out var players))used[key]=players=new HashSet<ulong>();players.Add(id);hp.Heal(80);return true;
        }
        public static Entry[] Snapshot()=>used.Select(p=>new Entry{key=p.Key,users=p.Value.ToArray()}).ToArray();
        public static void Apply(Entry[] entries){used.Clear();if(entries!=null)foreach(var e in entries)used[e.key]=new HashSet<ulong>(e.users);}
    }
}
