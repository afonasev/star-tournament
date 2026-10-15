using System;
using System.Globalization;

namespace StarTournament.ProvingGround
{
    // Presentation draft: target fields and their persistence handlers see only Apply.
    public sealed class NativeTextDraft
    {
        public string Text { get; private set; }
        public int Caret { get; private set; }
        public readonly int Limit;
        public readonly bool Numeric;
        readonly Func<string,int,char,char> validate;
        public NativeTextDraft(string text,int limit=0,bool numeric=false,Func<string,int,char,char> validate=null)
        {Text=text??"";Caret=Text.Length;Limit=limit;Numeric=numeric;this.validate=validate;}
        public bool Insert(char value)
        {
            if(char.IsControl(value)||Limit>0&&Text.Length>=Limit)return false;
            if(validate!=null)value=validate(Text,Caret,value);
            if(value=='\0')return false;
            var next=Text.Insert(Caret,value.ToString());
            if(Numeric)
            {
                int separators=0;
                for(int i=0;i<next.Length;i++)
                {char c=next[i];if(c>='0'&&c<='9')continue;if(c=='-'&&i==0)continue;if((c=='.'||c==',')&&++separators==1)continue;return false;}
            }
            Text=next;Caret++;return true;
        }
        public void Move(int amount)=>Caret=Math.Max(0,Math.Min(Text.Length,Caret+amount));
        public void Backspace(){if(Caret>0){Text=Text.Remove(Caret-1,1);Caret--;}}
        public void Delete(){if(Caret<Text.Length)Text=Text.Remove(Caret,1);}
        public bool Complete=>!Numeric||float.TryParse(Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var number)&&!float.IsNaN(number)&&!float.IsInfinity(number);
    }
}
