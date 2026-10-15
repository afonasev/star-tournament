using System;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class WellBotLiftIntent { public string Lift; public int Floor; public bool Boarding, Riding, Exiting; }
    /// <summary>Dynamic transit adapter uses only the planner's known goal and publicly visible lift state.</summary>
    public sealed class WellBotLiftRouter
    {
        readonly WellLiftSystem lifts;readonly INativeNavigation routes;readonly float range,lead,radius,skin;
        WellBotLiftIntent intent=new WellBotLiftIntent();
        public WellBotLiftRouter(WellLiftSystem lifts,INativeNavigation routes,ProvingProfile profile,ProvingProfile movement)
        {this.lifts=lifts;this.routes=routes;range=profile.Get("well.botDockRange");lead=profile.Get("well.botExitLead");radius=movement.Get("player.capsule.radius");skin=movement.Get("player.capsule.skinWidth");}
        public WellBotLiftIntent Capture()=>new WellBotLiftIntent{Lift=intent.Lift,Floor=intent.Floor,Boarding=intent.Boarding,Riding=intent.Riding,Exiting=intent.Exiting};
        public void Validate(WellBotLiftIntent s)
        {if(s==null||s.Floor<0||s.Floor>2||!string.IsNullOrEmpty(s.Lift)&&!lifts.Capture().Any(x=>x.Id==s.Lift)||string.IsNullOrEmpty(s.Lift)&&(s.Boarding||s.Riding||s.Exiting)||s.Exiting&&!s.Riding||s.Boarding&&s.Riding)throw new ArgumentException("Invalid bot lift intent");}
        public void Restore(WellBotLiftIntent s){Validate(s);intent=new WellBotLiftIntent{Lift=string.IsNullOrEmpty(s.Lift)?null:s.Lift,Floor=s.Floor,Boarding=s.Boarding,Riding=s.Riding,Exiting=s.Exiting};}
        public LocalAction Tick(ParticipantState pose,Vector3 goal,bool hasGoal,bool dead,LocalAction original)
        {
            if(dead){intent=new WellBotLiftIntent();return original;}
            var states=lifts.Capture();
            if(intent.Lift==null&&hasGoal&&Mathf.Abs(goal.y-pose.Position.y)>WellArenaCatalog.Storey*.5f)
            {
                int floor=Mathf.Clamp(Mathf.RoundToInt(goal.y/WellArenaCatalog.Storey),0,2);
                var available=states.Where(s=>(s.Phase==WellLiftPhase.Ready||s.Phase==WellLiftPhase.Boarding)&&Mathf.Abs(s.Height-pose.Position.y)<.25f&&Vector3.Distance(new Vector3(0,s.Height,Mathf.Sign(lifts.Definition(s.Id).Center.z)*24),pose.Position)<range).OrderBy(s=>Vector3.Distance(lifts.Definition(s.Id).Center,pose.Position)).FirstOrDefault();
                if(available!=null)intent=new WellBotLiftIntent{Lift=available.Id,Floor=floor};
            }
            if(intent.Lift==null)return original;
            var state=states.Single(s=>s.Id==intent.Lift);var d=lifts.Definition(intent.Lift);float side=Mathf.Sign(d.Center.z);
            bool aboard=lifts.OnPlatform(intent.Lift,pose)&&Mathf.Abs(pose.Position.x-d.Center.x)<=d.Size.x*.5f-radius&&Mathf.Abs(pose.Position.z-d.Center.z)<=d.Size.z*.5f-radius;
            if(aboard){intent.Riding=true;intent.Boarding=false;}
            if(intent.Riding)
            {
                float target=intent.Floor*WellArenaCatalog.Storey;
                if(!intent.Exiting&&(state.Phase==WellLiftPhase.Moving&&Mathf.Abs(state.Height-target)<=lead||state.Phase==WellLiftPhase.AwaitEmpty&&state.Height==target))
                {intent.Exiting=true;return Move(original,pose,new Vector3(0,0,-side),true);}
                if(intent.Exiting)
                {
                    if(side*pose.Position.z<WellArenaCatalog.Edge-radius&&pose.Grounded&&Mathf.Abs(pose.Position.y-target)<.3f){intent=new WellBotLiftIntent();return original;}
                    return Move(original,pose,new Vector3(0,0,-side),false);
                }
                // Waiting aboard follows the moving support, without resetting a trip when everyone exits.
                if(!aboard&&Mathf.Abs(pose.Position.y-state.Height)>.6f){intent=new WellBotLiftIntent();return original;}
                return Move(original,pose,Vector3.zero,false);
            }
            // A platform that left while approaching is no longer a route. Fall back immediately to the planner's stairs.
            if(state.Phase==WellLiftPhase.Moving||Mathf.Abs(state.Height-pose.Position.y)>.25f||state.Phase==WellLiftPhase.AwaitEmpty)
            {intent=new WellBotLiftIntent();return original;}
            var dock=new Vector3(0,state.Height,side*24);
            if(!intent.Boarding&&Vector3.Distance(pose.Position,dock)>1)
            {
                if(!routes.TryRoute(pose.Position,dock,out var points,out _)){intent=new WellBotLiftIntent();return original;}
                var next=points.FirstOrDefault(p=>Vector3.Distance(p.Position,pose.Position)>.6f);
                return Move(original,pose,next.Position-pose.Position,false);
            }
            intent.Boarding=true;
            // Board close to the lip so the jump to the middle storey has a real, short horizontal clearance.
            return Move(original,pose,new Vector3(d.Center.x,state.Height,d.Center.z-side*(d.Size.z*.5f-radius-skin))-pose.Position,false);
        }
        static LocalAction Move(LocalAction action,ParticipantState pose,Vector3 world,bool jump)
        {
            world.y=0;world=Vector3.ClampMagnitude(world,1);var local=Quaternion.Euler(0,-pose.Yaw,0)*world;
            action.Move=new Vector2(local.x,local.z);action.Jump=jump;return action;
        }
    }
}
