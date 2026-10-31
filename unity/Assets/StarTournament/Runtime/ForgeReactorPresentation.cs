using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    /// <summary>Bounded, session-owned reactor surfaces. No Update, wall clock, gameplay writes or shot history.</summary>
    public sealed class ForgeReactorPresentation : IDisposable
    {
        sealed class Surface
        {
            public Renderer Renderer; public int Slot, Participant; public Material Original;
            public Matrix4x4 ToReactor, FromReactor; public MaterialPropertyBlock Block=new MaterialPropertyBlock();
            public ShadowCastingMode Shadows; public Bounds Bounds;
        }
        readonly NativeCombatSession session;
        readonly List<Surface> surfaces=new List<Surface>();
        readonly Material lens,flux;
        readonly float speed,pulse,intensity,shotGain,recovery,cooldown;
        bool disposed;
        public int SurfaceCount=>surfaces.Count;
        public Material LensMaterial=>lens;
        public Material FluxMaterial=>flux;
        static readonly int Clock=Shader.PropertyToID("_ReactorClock"),Motion=Shader.PropertyToID("_ReactorMotion"),
            ToReactor=Shader.PropertyToID("_ObjectToReactor"),FromReactor=Shader.PropertyToID("_ReactorToObject");
        public ForgeReactorPresentation(NativeCombatSession session,Transform owner,GameObject[] bodies,GameObject[] views,int[] local)
        {
            this.session=session;var p=session.PlasmaProfile;
            // Old saved profiles remain immutable; missing additive art controls use the current named defaults.
            var defaults=ProvingProfile.CreatePlasmaDefault();
            float Read(string key)=>p.Descriptors.Any(d=>d.Path==key)?p.Get(key):defaults.Get(key);
            speed=Read("plasma.reactorSpeed");pulse=Read("plasma.reactorPulse");intensity=Read("plasma.reactorIntensity");
            shotGain=Read("plasma.reactorShotGain");recovery=Read("plasma.reactorRecovery");cooldown=p.Get("plasma.cooldownSeconds");
            var shader=Resources.Load<Shader>("ForgeReactor");
            if(!shader)throw new InvalidOperationException("FORGE reactor shader missing");
            lens=new Material(shader){name="FORGE session reactor lens"};
            flux=new Material(shader){name="FORGE session reactor flux"};flux.SetFloat("_Flux",1);
            bool textureBound=false;
            foreach(var renderer in owner.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int slot=0;slot<materials.Length;slot++)
                {
                    var source=materials[slot];if(!source)continue;
                    bool isLens=source.name=="FORGE contained reactor",isFlux=source.name=="FORGE reactor flux";
                    if(!isLens&&!isFlux)continue;
                    int participant=-1;
                    for(int i=0;i<bodies.Length;i++)if(bodies[i]&&renderer.transform.IsChildOf(bodies[i].transform)){participant=i;break;}
                    for(int i=0;i<views.Length;i++)if(views[i]&&renderer.transform.IsChildOf(views[i].transform)){participant=local[i];break;}
                    // GLB batches share a model parent; authored chamber center in the exported grip frame.
                    var model=renderer.transform.parent;
                    var matrix=Matrix4x4.Translate(-new Vector3(0,.205f,-.212f))*model.worldToLocalMatrix*renderer.localToWorldMatrix;
                    surfaces.Add(new Surface{Renderer=renderer,Slot=slot,Participant=participant,Original=source,
                        ToReactor=matrix,FromReactor=matrix.inverse,Shadows=renderer.shadowCastingMode,Bounds=renderer.localBounds});
                    if(isLens&&!textureBound)
                        foreach(var property in source.GetTexturePropertyNames())
                            if(property.ToLowerInvariant().Contains("basecolor")&&source.GetTexture(property))
                            {lens.SetTexture("_FlowMap",source.GetTexture(property));textureBound=true;break;}
                    materials[slot]=isLens?lens:flux;changed=true;
                }
                if(changed)
                {
                    renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
                    // Conservative asset culling margin for the bounded GPU tube deformation, not gameplay volume.
                    var bounds=renderer.localBounds;bounds.Expand(.03f);renderer.localBounds=bounds;
                }
            }
            Render();
        }
        public float ShotPulse(int participant)
        {
            if(disposed||participant<0)return 0;
            var life=session.Life(participant);
            if(life.Dead||!life.PlasmaOwned||life.SelectedWeapon!=WeaponId.Plasma||life.PlasmaCooldownRemaining<=0)return 0;
            // Cooldown is set only by a real shot and is serialized. This also reconstructs mid-shot restores exactly.
            float age=Mathf.Max(0,cooldown-(float)life.PlasmaCooldownRemaining);
            float x=Mathf.Clamp01(age/Mathf.Min(recovery,cooldown));
            return (1-x)*(1-x)*(1+2*x);
        }
        public void Render()
        {
            if(disposed)return;
            foreach(var surface in surfaces)
            {
                if(!surface.Renderer)continue;
                surface.Renderer.GetPropertyBlock(surface.Block,surface.Slot);
                surface.Block.SetVector(Clock,new Vector4((float)session.Time,ShotPulse(surface.Participant),speed,pulse));
                surface.Block.SetVector(Motion,new Vector4(intensity,shotGain,0,0));
                surface.Block.SetMatrix(ToReactor,surface.ToReactor);surface.Block.SetMatrix(FromReactor,surface.FromReactor);
                surface.Renderer.SetPropertyBlock(surface.Block,surface.Slot);
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var surface in surfaces)
            {
                if(!surface.Renderer)continue;
                var materials=surface.Renderer.sharedMaterials;
                materials[surface.Slot]=surface.Original;surface.Renderer.sharedMaterials=materials;
                surface.Renderer.SetPropertyBlock(null,surface.Slot);surface.Renderer.shadowCastingMode=surface.Shadows;surface.Renderer.localBounds=surface.Bounds;
            }
            surfaces.Clear();UnityEngine.Object.Destroy(lens);UnityEngine.Object.Destroy(flux);
        }
    }
}
