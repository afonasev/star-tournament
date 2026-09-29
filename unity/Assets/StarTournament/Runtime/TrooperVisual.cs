using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace StarTournament.ProvingGround
{
    /// <summary>Visual child only. Explicit session clock; never writes a motor, camera, or combat state.</summary>
    public sealed class TrooperVisual : MonoBehaviour
    {
        public static readonly string[] ClipNames={"idle","walk","run","aim","fire","hit","death"};
        public AnimationClip[] Clips;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] playable;
        ProvingProfile profile;
        OrbitalGripPose equipmentGrip;
        readonly double[] starts=new double[7]; // Fixed clip contract, not tunable gameplay data.
        readonly float[] weights=new float[7], transitionFrom=new float[7];
        double transitionAt;
        int active=-1, life=-1;
        float health;
        double firedAt=double.NegativeInfinity, hitAt=double.NegativeInfinity;
        bool firstPerson, corpse;
        public ProvingProfile Tuning => profile;
        public DeathRagdoll Ragdoll {get;internal set;}
        public string State => Ragdoll!=null?"ragdoll":active<0?"uninitialized":ClipNames[active];
        public double SampleTime { get; private set; }
        public bool GraphValid => graph.IsValid();
        public int PoseRevision { get; private set; }
        public void Initialize(AnimationClip[] clips,ProvingProfile tuning,bool view=false)
        {
            Release(); Clips=clips; profile=tuning; firstPerson=view;
            if(tuning==null || tuning.Validate().Count!=0) throw new InvalidOperationException("Invalid trooper profile");
            if(clips==null || clips.Length!=ClipNames.Length) throw new InvalidOperationException("Trooper requires seven persisted clips");
            foreach(var clip in clips) if(!clip || clip.legacy || !(clip.length>0) || float.IsInfinity(clip.length)) throw new InvalidOperationException("Invalid persisted trooper clip");
            var animator=GetComponentInChildren<Animator>(true);
            if(!animator) throw new InvalidOperationException("Trooper Animator root missing");
            animator.runtimeAnimatorController=null; animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            equipmentGrip=new OrbitalGripPose(animator.transform);
            graph=PlayableGraph.Create("trooper-presentation"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,clips.Length);
            playable=new AnimationClipPlayable[clips.Length];
            for(int i=0;i<clips.Length;i++)
            {
                if(!clips[i] || clips[i].legacy) throw new InvalidOperationException("Invalid trooper clip "+ClipNames[i]);
                playable[i]=AnimationClipPlayable.Create(graph,clips[i]); playable[i].SetSpeed(0);
                playable[i].SetApplyFootIK(false); playable[i].SetApplyPlayableIK(false);
                graph.Connect(playable[i],0,mixer,i);
            }
            AnimationPlayableOutput.Create(graph,"trooper",animator).SetSourcePlayable(mixer);
            graph.Play(); ResetPose(0);
        }
        public void ResetPose(double clock)
        {
            corpse=false; life=-1; active=-1; health=0;
            firedAt=hitAt=double.NegativeInfinity;
            Array.Clear(weights,0,weights.Length); Array.Clear(starts,0,starts.Length);
            Sample(firstPerson?3:0,clock,true);
        }
        public void Fire(double clock) { if(!corpse) { firedAt=clock; starts[4]=clock; } }
        public void BeginDeath(double clock)
        {
            corpse=true; active=-1; Array.Clear(weights,0,weights.Length); Sample(6,clock,true);
        }
        public void Render(double clock,Vector3 velocity,float currentHealth,int currentLife)
        {
            if(!graph.IsValid()) return;
            if(corpse) { Sample(6,clock,false); return; }
            if(life!=currentLife) { if(life>=0) ResetPose(clock); life=currentLife; health=currentHealth; }
            if(currentHealth<health) { hitAt=clock; starts[5]=clock; }
            health=currentHealth;
            float speed=new Vector2(velocity.x,velocity.z).magnitude;
            int next=speed<profile.Get("animation.walkThreshold")?(firstPerson?3:0):speed<profile.Get("animation.runThreshold")?1:2;
            if(clock-firedAt<profile.Get("animation.aimHoldSeconds")) next=3;
            if(clock-firedAt<Clips[4].length) next=4;
            if(clock-hitAt<Clips[5].length) next=5;
            Sample(next,clock,false);
        }
        void Sample(int next,double clock,bool immediate)
        {
            if(next!=active)
            {
                active=next; transitionAt=clock; Array.Copy(weights,transitionFrom,weights.Length);
                starts[next]=next==4?firedAt:next==5?hitAt:clock;
            }
            float fade=profile.Get("animation.blendSeconds");
            float fraction=immediate||fade<=0?1:Mathf.Clamp01((float)(clock-transitionAt)/fade);
            for(int i=0;i<weights.Length;i++)
            {
                weights[i]=Mathf.Lerp(transitionFrom[i],i==active?1:0,fraction);
                double time=Math.Max(0,clock-starts[i]);
                // Clip duration is immutable imported content; one-shots hold their terminal pose.
                time=i<4?time%Clips[i].length:Math.Min(time,Clips[i].length);
                playable[i].SetTime(time); playable[i].SetDone(false); mixer.SetInputWeight(i,weights[i]);
                if(i==active) SampleTime=time;
            }
            if(immediate) Array.Copy(weights,transitionFrom,weights.Length);
            graph.Evaluate(0);
            if(!corpse)equipmentGrip?.Apply(profile);
            PoseRevision++;
        }
        public void Release()
        {
            if(graph.IsValid()) graph.Destroy();
            active=-1;
        }
        void OnDestroy() => Release();
    }
}
