using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeTextDraftTests
    {
        [Test] public void UnicodeLengthAndCaretEditingStayInsideDraft()
        {
            string source="Пилот";var draft=new NativeTextDraft(source,6);
            Assert.That(draft.Insert('я'),Is.True);Assert.That(draft.Insert('!'),Is.False);
            draft.Move(-2);draft.Backspace();Assert.That(draft.Text,Is.EqualTo("Пилтя"));
            draft.Delete();Assert.That(draft.Text,Is.EqualTo("Пиля"));
            draft.Move(-100);Assert.That(draft.Caret,Is.Zero);draft.Move(100);Assert.That(draft.Caret,Is.EqualTo(4));
            Assert.That(source,Is.EqualTo("Пилот"));
        }
        [TestCase("-1,25",true)] [TestCase("0.5",true)] [TestCase("-",false)] [TestCase(".",false)]
        public void NumericDraftAllowsIntermediateEditingButRequiresFiniteCompleteNumber(string text,bool complete)
        {var draft=new NativeTextDraft("",0,true);foreach(char c in text)Assert.That(draft.Insert(c),Is.True);Assert.That(draft.Complete,Is.EqualTo(complete));}
        [Test] public void NumericDraftRejectsLettersDuplicateSeparatorsAndMisplacedSigns()
        {
            var draft=new NativeTextDraft("1.2",0,true);
            Assert.That(draft.Insert(','),Is.False);Assert.That(draft.Insert('x'),Is.False);Assert.That(draft.Insert('-'),Is.False);
            draft.Move(-3);Assert.That(draft.Insert('-'),Is.True);draft.Move(-4);Assert.That(draft.Insert('9'),Is.False);Assert.That(draft.Text,Is.EqualTo("-1.2"));
        }
        [Test] public void FieldSpecificValidatorIsAppliedBeforeInsertion()
        {var draft=new NativeTextDraft("",4,false,(s,i,c)=>char.IsLetter(c)?char.ToUpperInvariant(c):'\0');Assert.That(draft.Insert('я'),Is.True);Assert.That(draft.Insert('3'),Is.False);Assert.That(draft.Text,Is.EqualTo("Я"));}
    }
}
