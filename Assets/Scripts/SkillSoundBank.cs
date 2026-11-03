using System;
using UnityEngine;

[CreateAssetMenu(menuName="Audio/Card skill sounds",fileName="CardSkillSounds")]
public class SkillSoundBank : ScriptableObject
{
    [Serializable] public class Cue
    {
        public string key;
        public AudioClip clip;
        [Range(0,1)] public float volume=.75f;
    }
    public Cue[] cues=Array.Empty<Cue>();
    public Cue Find(string key) => Array.Find(cues,c=>c!=null&&c.key==key);
}
