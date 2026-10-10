using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        NativeLoadingScreen loadingScreen;
        bool worldReady;
        public bool IsReady {get;private set;}
        public bool IsLoading=>phase==Phase.Loading;
        public NativeLoadingScreen LoadingScreen=>loadingScreen;
        void QueueMatch(bool diagnostics,bool repeating=false)
        {
            if(!IsReady||IsLoading)return;
            if(repeating ? !diagnostic&&!input.Ready : ((!diagnostics&&(!input.Ready||!IdentitiesReady()))||!ValidSetup()))return;
            var configuration=repeating?frozenConfiguration:Configuration;
            var mode=repeating?frozenRoster.Mode:SetupMode;
            var count=repeating?Composition.ParticipantCount:RosterTotal;
            loadingScreen.ShowMatch(AuthoredArenaCatalog.Name(repeating?frozenMatchArena.Definition.MapId:SelectedMapId),mode,count,configuration);
            worldReady=false;gameAudio?.StopEffects();
            phase=Phase.Loading;
            if(EventSystem.current){EventSystem.current.SetSelectedGameObject(null);EventSystem.current.sendNavigationEvents=false;}
            var ui=transform.Find("native-ui");if(ui)ui.gameObject.SetActive(false);
            SetCursor(false);
            StartCoroutine(RunLoadingSteps(PrepareMatch(diagnostics,repeating),true));
        }
        IEnumerator PrepareMatch(bool diagnostics,bool repeating)
        {
            // Let the loading Canvas reach rendering before constructing world objects.
            yield return null;
            diagnostic=diagnostics;
            if(repeating)frozenMatchArena.RequireDefinition(arena.Definition);
            else
            {
                if(!diagnostics&&!combatReview&&!botReviewEnabled&&reviewComposition==null)ApplySavedLabRevision();
                yield return BuildSelectedArenaSteps(true);
            }
            yield return CreateMatchSteps(repeating);
            input.Clear();Session.ClearInput();setupError=null;
            var ui=transform.Find("native-ui");if(ui)ui.gameObject.SetActive(true);
            worldReady=true;phase=Phase.Running;SetCursor(!diagnostics&&input.HasKeyboard);
            ApplyMatchFpsPreference();RefreshInterface();loadingScreen.Hide();
            if(!diagnostics&&!input.Ready)Pause("Устройство отключено");
            else if(!musicFocused&&!diagnostics&&!combatReview&&!nativeInputReview)Pause("Окно потеряло фокус");
            if(!repeating&&!diagnostics&&!combatReview&&!botReviewEnabled&&reviewComposition==null&&!nativeInputReview)RememberPlayedMap();
        }
        static void DrainLoadingSteps(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            try{while(stack.Count>0){var current=stack.Peek();if(!current.MoveNext()){(stack.Pop() as IDisposable)?.Dispose();continue;}if(current.Current is IEnumerator nested)stack.Push(nested);}}
            finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
        }
        IEnumerator RunLoadingSteps(IEnumerator routine,bool match)
        {
            // Flatten nested iterators so failures in every preparation stage reach recovery.
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            Exception failure=null;
            try
            {
                while(stack.Count>0)
                {
                    object next=null;bool moved=false;
                    try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                    catch(Exception error){failure=error;}
                    if(failure!=null)break;
                    if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                    if(next is IEnumerator nested){stack.Push(nested);continue;}
                    yield return next;
                }
            }
            finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
            if(failure==null)yield break;
            if(match)
            {
                // Recover UI without reconstructing a possibly damaged projection/actor.
                try
                {
                    worldReady=false;Session?.Stop();presentation?.Dispose();presentation=null;
                    BotDriver=null;NavigationReviewDriver=null;ClearSeatPauseMenus();
                    foreach(var motor in motors)if(motor)motor.gameObject.SetActive(false);
                    foreach(var camera in cameras)if(camera)camera.enabled=false;
                    var ui=transform.Find("native-ui");if(ui)ui.gameObject.SetActive(true);
                    diagnostic=combatReview=nativeInputReview=false;phase=Phase.Setup;
                    input.Clear();Session?.ClearInput();setupError=failure.Message;SetCursor(false);
                    RefreshInterface();FocusSetupStep();
                }
                finally{loadingScreen.Hide();}
            }
            else {loadingScreen.ShowError("Не удалось загрузить игру");Debug.LogException(failure);}
        }
    }
}
