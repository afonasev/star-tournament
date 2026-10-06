using System;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    // Audio destinations are independent of combat subscriptions/preview sessions.
    // At most two menu + two round voices; reversals reuse transports and current weights.
    public sealed class NativeMusicCoordinator : IDisposable
    {
        public NativeRoundMusic Round { get; }
        public NativeMenuMusic Menu { get; }
        NativeMatchState bound;
        float elapsed,seconds,fromMenu=1,fromRound;
        bool towardRound;
        bool suspended;
        public float RoundWeight { get; private set; }
        public float MenuWeight { get; private set; }=1;
        public float TransitionProgress=>Mathf.Clamp01(elapsed/Mathf.Max(.01f,seconds));
        public NativeMatchState BoundMatch=>bound;
        public bool Suspended=>suspended;
        public NativeMusicCoordinator(Transform parent,ProvingProfile profile)
        {
            Round=new NativeRoundMusic(parent);Menu=new NativeMenuMusic(parent,profile);
            seconds=profile.Get("audio.music.menuTransitionSeconds");
            elapsed=seconds;
        }
        public void SetSuspended(bool value)
        {
            suspended=value;Menu.SetSuspended(value||MenuWeight<=0);Round.SetSuspended(value||RoundWeight<=0);
        }
        void Begin(bool round,float menuOrigin,float roundOrigin,ProvingProfile profile)
        {
            towardRound=round;fromMenu=menuOrigin;fromRound=roundOrigin;elapsed=0;
            seconds=profile.Get("audio.music.menuTransitionSeconds");
        }
        public void Tick(bool inMenu,NativeMatchState desired,ProvingProfile profile,bool suspend,float dt)
        {
            bool wasSuspended=suspended;
            if(suspend){SetSuspended(true);return;}
            suspended=false;Menu.Configure(profile);
            bool wantRound=!inMenu&&desired!=null,began=false;
            if(wantRound&&desired!=bound)
            {
                // Coalesce rapid start/back/repeat into the latest desired identity. Gameplay
                // proceeds immediately, while an audible old round relinquishes its transport.
                if(bound==null||RoundWeight<=0||!Round.HasOutput||wasSuspended)
                {
                    // New audio never inherits the envelope of a silent/finished old track.
                    Begin(true,wasSuspended?0:MenuWeight,0,profile);began=true;
                    Round.Bind(desired,profile);bound=desired;
                }
                else wantRound=false;
            }
            if(!began&&wantRound!=towardRound)
                Begin(wantRound,wasSuspended?0:MenuWeight,wasSuspended||!Round.HasOutput?0:RoundWeight,profile);
            elapsed=Mathf.Min(seconds,elapsed+Mathf.Max(0,dt));float t=TransitionProgress;
            float a=t>=1?0:Mathf.Cos(t*Mathf.PI*.5f),b=t>=1?1:Mathf.Sin(t*Mathf.PI*.5f);
            float menu=fromMenu*a+(towardRound?0:b),round=fromRound*a+(towardRound?b:0);
            float sum=Mathf.Max(1,menu+round);MenuWeight=menu/sum;RoundWeight=round/sum;
            Menu.Tick(MenuWeight>0,dt);
            if(RoundWeight>0){Round.SetSuspended(false);Round.Tick(true,dt);}
            else
            {
                Round.SetSuspended(true);
                if(bound!=null&&!towardRound){Round.Stop();bound=null;}
            }
        }
        public void Restore(NativeMatchState match)
        {
            if(match==bound)Round.Restore();
        }
        public float RequestedVolume(float user)=>suspended?0:Menu.RequestedVolume(user*MenuWeight)+Round.RequestedVolume(user*RoundWeight);
        public void UpdateVolume(float user,float available)
        {
            float menu=suspended?0:Menu.RequestedVolume(user*MenuWeight);
            float round=suspended?0:Round.RequestedVolume(user*RoundWeight);
            float scale=Mathf.Min(Mathf.Max(0,available),menu+round)/Mathf.Max(.00001f,menu+round);
            Menu.UpdateVolume(user*MenuWeight,menu*scale);Round.UpdateVolume(user*RoundWeight,round*scale);
        }
        public void Dispose(){Menu.Dispose();Round.Dispose();}
    }
}
