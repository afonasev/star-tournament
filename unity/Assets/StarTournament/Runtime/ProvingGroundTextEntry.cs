using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        GameObject textEntryPanel;
        InputField textEntryTarget;
        NativeTextDraft textDraft;
        InputField.EndEditEvent textEndEdit;
        Gamepad textPad;
        Keyboard textKeyboard;
        Text textEntryValue,textEntryCount,textEntryStatus;
        Button textSpace,textDelete,textCancel,textApply;
        readonly List<List<Button>> textRows=new List<List<Button>>();
        bool textRussian=true,textUpper=true,textClosing,textNavigationBefore,textUsingPad;
        int textOpenedFrame,textOriginalCaret,textCharacterFrame=-1,textFieldEndedFrame=-1;
        Vector2 textLastDirection;
        float textRepeatAt;

        void BindTextInput(InputField field,bool numeric=false)
        {
            field.gameObject.AddComponent<MenuActionHint>().Initialize(field,()=>menuDevice,()=>menuGamepadCursor&&menuWindowFocused&&!textEntryPanel);
            var binding=field.gameObject.AddComponent<NativeGamepadTextField>();
            binding.Open=()=>{if(menuDevice is Gamepad pad)OpenTextEntry(field,pad,numeric);};
            field.onEndEdit.AddListener(_=>textFieldEndedFrame=Time.frameCount);
        }
        void OpenTextEntry(InputField field,Gamepad pad,bool numeric)
        {
            if(textEntryPanel||textClosing||!field.IsInteractable())return;
            textPad=pad;textEntryTarget=field;textOriginalCaret=field.caretPosition;
            textDraft=new NativeTextDraft(field.text,field.characterLimit,numeric,
                field.onValidateInput==null?null:new Func<string,int,char,char>(field.onValidateInput));
            // Deselecting a legacy InputField ends editing. Keep that event inside Apply.
            textEndEdit=field.onEndEdit;field.onEndEdit=new InputField.EndEditEvent();
            field.DeactivateInputField();field.interactable=false;
            textNavigationBefore=EventSystem.current.sendNavigationEvents;EventSystem.current.sendNavigationEvents=false;
            textOpenedFrame=Time.frameCount;textLastDirection=Vector2.zero;textUsingPad=true;
            var canvas=field.GetComponentInParent<Canvas>().rootCanvas;
            textEntryPanel=Panel(canvas.transform,"text-entry",Vector2.zero,Vector2.one,new Color32(8,19,30,255));
            var overlay=textEntryPanel.AddComponent<Canvas>();overlay.overrideSorting=true;overlay.sortingOrder=100;
            textEntryPanel.AddComponent<GraphicRaycaster>();
            Label(textEntryPanel.transform,"title",numeric?"ЧИСЛОВОЕ ЗНАЧЕНИЕ":field.name.Contains("name")?"ИМЯ ПРОФИЛЯ":field.name=="lab-search"?"ПОИСК В ЛАБОРАТОРИИ":"ВВОД ТЕКСТА",36,new Vector2(.11f,.86f),new Vector2(.89f,.95f),TextAnchor.MiddleLeft,MenuGold);
            var value=Panel(textEntryPanel.transform,"text-entry-field",new Vector2(.11f,.68f),new Vector2(.89f,.84f),MenuCard);
            textEntryValue=Label(value.transform,"text-entry-value","",30,new Vector2(.03f,.2f),new Vector2(.97f,.95f),TextAnchor.MiddleLeft,Color.white);textEntryValue.supportRichText=false;
            textEntryCount=Label(value.transform,"text-entry-count","",18,new Vector2(.8f,0),new Vector2(.97f,.2f),TextAnchor.MiddleRight,Color.white);
            textEntryStatus=Label(textEntryPanel.transform,"text-entry-status","",19,new Vector2(.11f,.615f),new Vector2(.89f,.675f),TextAnchor.MiddleLeft,new Color32(255,218,184,255));
            BuildTextKeys();AttachTextKeyboard();RefreshTextEntry();
        }
        Button TextKey(string name,string caption,Vector2 min,Vector2 max,Action action)
        {
            var button=MenuButton(textEntryPanel.transform,name,caption,min,max,()=>
            {if(Time.frameCount<=textOpenedFrame||!textEntryPanel)return;action();if(textEntryPanel)RefreshTextEntry();});
            bool extra=name=="text-space"||name=="text-delete"||name=="text-cancel"||name=="text-apply";
            button.GetComponent<MenuActionHint>().Initialize(button,()=>textPad,()=>menuGamepadCursor&&menuWindowFocused&&textUsingPad&&!extra);
            button.GetComponent<MenuPresentation>().Height=56;
            button.GetComponentInChildren<Text>().fontSize=24;
            return button;
        }
        void BuildTextKeys()
        {
            int oldRow=0,oldColumn=0;var selected=EventSystem.current.currentSelectedGameObject;
            for(int r=0;r<textRows.Count;r++)for(int c=0;c<textRows[r].Count;c++)if(textRows[r][c]&&textRows[r][c].gameObject==selected){oldRow=r;oldColumn=c;}
            foreach(var row in textRows)foreach(var key in row){key.gameObject.SetActive(false);Destroy(key.gameObject);}textRows.Clear();
            string[] rows=textDraft.Numeric?new[]{"1234567890","-.,"}:textRussian?new[]{"1234567890-_","ЙЦУКЕНГШЩЗХЪ","ФЫВАПРОЛДЖЭ","ЯЧСМИТЬБЮЁ"}:new[]{"1234567890-_","QWERTYUIOP","ASDFGHJKL","ZXCVBNM.,"};
            for(int r=0;r<rows.Length;r++)
            {
                var row=new List<Button>();textRows.Add(row);string letters=textUpper?rows[r]:rows[r].ToLowerInvariant();
                float width=.78f/12,left=.11f+(.78f-letters.Length*width)/2;
                for(int c=0;c<letters.Length;c++)
                {char letter=letters[c];row.Add(TextKey("text-key-"+r+"-"+c,letter.ToString(),new Vector2(left+c*width,.555f-r*.074f),new Vector2(left+(c+1)*width-.006f,.617f-r*.074f),()=>textDraft.Insert(letter)));}
            }
            var actions=new List<Button>();textRows.Add(actions);
            if(!textDraft.Numeric)
            {
                actions.Add(TextKey("text-language","RU / EN",new Vector2(.11f,.25f),new Vector2(.215f,.31f),()=>{textRussian=!textRussian;BuildTextKeys();}));
                actions.Add(TextKey("text-case","А / а",new Vector2(.223f,.25f),new Vector2(.328f,.31f),()=>{textUpper=!textUpper;BuildTextKeys();}));
            }
            actions.Add(TextKey("text-left","←",new Vector2(.336f,.25f),new Vector2(.426f,.31f),()=>textDraft.Move(-1)));
            textSpace=TextKey("text-space","Пробел",new Vector2(.434f,.25f),new Vector2(.614f,.31f),()=>textDraft.Insert(' '));
            textSpace.interactable=!textDraft.Numeric;actions.Add(textSpace);
            actions.Add(TextKey("text-right","→",new Vector2(.622f,.25f),new Vector2(.712f,.31f),()=>textDraft.Move(1)));
            textDelete=TextKey("text-delete","Удалить",new Vector2(.72f,.25f),new Vector2(.89f,.31f),()=>textDraft.Backspace());actions.Add(textDelete);
            textCancel=TextKey("text-cancel","Отмена",new Vector2(.11f,.12f),new Vector2(.35f,.185f),()=>CloseTextEntry(false));
            textApply=TextKey("text-apply","Применить",new Vector2(.65f,.12f),new Vector2(.89f,.185f),()=>CloseTextEntry(true));
            textApply.GetComponent<Image>().color=MenuGold;textApply.GetComponentInChildren<Text>().color=MenuInk;
            textRows.Add(new List<Button>{textCancel,textApply});
            var focusRow=textRows[Mathf.Min(oldRow,textRows.Count-1)];Select(focusRow[Mathf.Min(oldColumn,focusRow.Count-1)]);
        }
        void AttachTextKeyboard()
        {if(textKeyboard==Keyboard.current)return;if(textKeyboard!=null)textKeyboard.onTextInput-=TextEntryCharacter;textKeyboard=Keyboard.current;if(textKeyboard!=null)textKeyboard.onTextInput+=TextEntryCharacter;}
        void TextEntryCharacter(char c)
        {if(!textEntryPanel||Time.frameCount<=textOpenedFrame||char.IsControl(c))return;textDraft.Insert(c);menuDevice=textKeyboard;textUsingPad=false;textCharacterFrame=Time.frameCount;menuGamepadCursor=false;RefreshTextEntry();}
        void UpdateTextEntryCursor()
        {if(textEntryPanel){if(!menuWindowFocused){menuGamepadCursor=false;SetCursor(false);}else{menuGamepadCursor=textUsingPad;Cursor.visible=!textUsingPad;}RefreshTextEntry();}}
        void RefreshTextEntry()
        {
            int start=Mathf.Max(0,textDraft.Caret-32),end=Mathf.Min(textDraft.Text.Length,textDraft.Caret+32);
            textEntryValue.text=(start>0?"…":"")+textDraft.Text.Substring(start,textDraft.Caret-start)+"│"+textDraft.Text.Substring(textDraft.Caret,end-textDraft.Caret)+(end<textDraft.Text.Length?"…":"");
            textEntryCount.text=textDraft.Text.Length+(textDraft.Limit>0?" / "+textDraft.Limit:"");
            textEntryStatus.text=textPad!=null&&(!textPad.added||!textPad.enabled)?"Геймпад отключён. Черновик сохранён; нажмите нижнюю кнопку подключённого геймпада или используйте клавиатуру и мышь.":textDraft.Complete?"":"Введите корректное число.";
            textApply.interactable=textDraft.Complete;
            bool hints=menuGamepadCursor;
            textSpace.GetComponentInChildren<Text>().text=MenuGamepadHints.Label("Пробел",textPad,hints&&textSpace.IsInteractable(),MenuPadButton.North);
            textDelete.GetComponentInChildren<Text>().text=MenuGamepadHints.Label("Удалить",textPad,hints&&textDelete.IsInteractable(),MenuPadButton.West);
            textCancel.GetComponentInChildren<Text>().text=MenuGamepadHints.Label("Отмена",textPad,hints&&textCancel.IsInteractable(),MenuPadButton.East);
            textApply.GetComponentInChildren<Text>().text=MenuGamepadHints.Label("Применить",textPad,hints&&textApply.IsInteractable(),MenuPadButton.Start);
        }
        void CloseTextEntry(bool apply)
        {
            if(!textEntryPanel||apply&&!textDraft.Complete)return;
            var target=textEntryTarget;string value=textDraft.Text;int caret=apply?textDraft.Caret:textOriginalCaret;
            textEntryPanel.SetActive(false);Destroy(textEntryPanel);textEntryPanel=null;textRows.Clear();
            if(textKeyboard!=null)textKeyboard.onTextInput-=TextEntryCharacter;textKeyboard=null;
            if(target)
            {
                target.interactable=true;
                // Value callbacks can rebuild a Lab search view. Only touch a surviving target.
                if(apply)target.text=value;
                if(target)
                {
                    target.interactable=false;
                    if(target.gameObject.activeInHierarchy)Select(target);
                    target.DeactivateInputField();target.caretPosition=caret;
                    target.onEndEdit=textEndEdit;target.interactable=true;
                    if(apply)textEndEdit?.Invoke(target.text);
                }
            }
            textClosing=true;
        }
        bool UpdateTextEntry()
        {
            if(textClosing)
            {
                bool held=textPad!=null&&textPad.added&&(textPad.buttonSouth.isPressed||textPad.buttonEast.isPressed||textPad.startButton.isPressed);
                held|=Keyboard.current!=null&&(Keyboard.current.enterKey.isPressed||Keyboard.current.escapeKey.isPressed);
                if(!held){textClosing=false;EventSystem.current.sendNavigationEvents=textNavigationBefore;}
                return true;
            }
            if(!textEntryPanel)
            {
                var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                var field=selected?selected.GetComponent<InputField>():null;
                if(!field||!field.IsInteractable())return false;
                var binding=field.GetComponent<NativeGamepadTextField>();
                var pressed=Gamepad.all.FirstOrDefault(p=>p.enabled&&p.buttonSouth.wasPressedThisFrame);
                if(binding&&pressed!=null){menuDevice=pressed;binding.Open();return textEntryPanel;}
                if((field.isFocused||textFieldEndedFrame==Time.frameCount)&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){field.DeactivateInputField();return true;}
                return false;
            }
            AttachTextKeyboard();RefreshTextEntry();if(Time.frameCount<=textOpenedFrame)return true;
            if(textPad==null||!textPad.added||!textPad.enabled)
            {
                var replacement=Gamepad.all.FirstOrDefault(p=>p.enabled&&p.buttonSouth.wasPressedThisFrame);
                if(replacement!=null){textPad=replacement;menuDevice=replacement;textOpenedFrame=Time.frameCount;return true;}
            }
            var keyboard=Keyboard.current;
            var mouse=Mouse.current;
            bool otherIntent=textCharacterFrame==Time.frameCount||keyboard!=null&&keyboard.anyKey.wasPressedThisFrame||mouse!=null&&(mouse.delta.ReadValue()!=Vector2.zero||mouse.leftButton.wasPressedThisFrame);
            if(otherIntent)textUsingPad=false;
            if(keyboard!=null)
            {
                if(keyboard.escapeKey.wasPressedThisFrame){CloseTextEntry(false);return true;}
                if(keyboard.enterKey.wasPressedThisFrame){CloseTextEntry(true);return true;}
                if(keyboard.backspaceKey.wasPressedThisFrame)textDraft.Backspace();if(keyboard.deleteKey.wasPressedThisFrame)textDraft.Delete();
                if(keyboard.leftArrowKey.wasPressedThisFrame)textDraft.Move(-1);if(keyboard.rightArrowKey.wasPressedThisFrame)textDraft.Move(1);
            }
            if(textPad!=null&&textPad.added&&textPad.enabled)
            {
                var nav=textPad.dpad.ReadValue();if(nav==Vector2.zero)nav=textPad.leftStick.ReadValue();
                nav=new Vector2(Mathf.Abs(nav.x)>.5f?Mathf.Sign(nav.x):0,Mathf.Abs(nav.y)>.5f?Mathf.Sign(nav.y):0);
                bool fresh=nav!=textLastDirection&&nav!=Vector2.zero||textPad.buttonSouth.wasPressedThisFrame||textPad.buttonEast.wasPressedThisFrame||textPad.startButton.wasPressedThisFrame||textPad.buttonWest.wasPressedThisFrame||textPad.buttonNorth.wasPressedThisFrame;
                if(fresh&&!otherIntent)textUsingPad=true;
                if(!textUsingPad||otherIntent){textLastDirection=nav;RefreshTextEntry();return true;}
                if(textPad.buttonEast.wasPressedThisFrame){CloseTextEntry(false);return true;}
                if(textPad.startButton.wasPressedThisFrame){CloseTextEntry(true);return true;}
                if(textPad.buttonWest.wasPressedThisFrame)textDraft.Backspace();if(textPad.buttonNorth.wasPressedThisFrame)textDraft.Insert(' ');
                if(nav!=textLastDirection){textLastDirection=nav;textRepeatAt=Time.unscaledTime+menuInputModule.moveRepeatDelay;MoveTextFocus(nav);}
                else if(nav!=Vector2.zero&&Time.unscaledTime>=textRepeatAt){textRepeatAt=Time.unscaledTime+menuInputModule.moveRepeatRate;MoveTextFocus(nav);}
                if(textPad.buttonSouth.wasPressedThisFrame)EventSystem.current.currentSelectedGameObject?.GetComponent<Button>()?.onClick.Invoke();
            }
            if(textEntryPanel)RefreshTextEntry();return true;
        }
        void MoveTextFocus(Vector2 nav)
        {
            if(nav==Vector2.zero)return;
            var current=EventSystem.current.currentSelectedGameObject;
            for(int r=0;r<textRows.Count;r++)for(int c=0;c<textRows[r].Count;c++)if(textRows[r][c].gameObject==current)
            {
                int nr=r,nc=c;
                if(nav.y!=0){nr=(r+(nav.y>0?-1:1)+textRows.Count)%textRows.Count;nc=Mathf.Min(c,textRows[nr].Count-1);}
                else nc=(c+(nav.x>0?1:-1)+textRows[r].Count)%textRows[r].Count;
                var next=textRows[nr][nc];if(!next.IsInteractable())next=textRows[nr].First(b=>b.IsInteractable());Select(next);return;
            }
            Select(textRows[0][0]);
        }
        void DisposeTextEntry()
        {if(textKeyboard!=null)textKeyboard.onTextInput-=TextEntryCharacter;if(textEntryPanel||textClosing)if(EventSystem.current)EventSystem.current.sendNavigationEvents=textNavigationBefore;}
    }
    public sealed class NativeGamepadTextField : MonoBehaviour,ISubmitHandler
    {
        public Action Open;
        public void OnSubmit(BaseEventData data)=>Open?.Invoke();
    }
}
