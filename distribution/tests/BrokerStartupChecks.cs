using StarTournament.Distribution;

static class BrokerStartupChecks
{
    static BrokerUpgradeState State(string phase)=>new(1,"root","app","1.0.0","production",new string('a',32),new string('b',64),phase,[]);
    static void Require(bool value){if(!value)throw new Exception("Broker startup assertion failed");}
    static async Task Reject(Func<Task> action)
    {try{await action();}catch(IOException){return;}throw new Exception("Expected startup rejection");}
    public static async Task Run()
    {
        int count=0;async Task Test(string name,Func<Task> body){await body();count++;Console.WriteLine("PASS "+name);}
        await Test("capable broker requires no elevation",async()=>{int invoked=0;await WindowsBrokerUpgradeStartup.Ensure(()=>null,()=>2,()=>{invoked++;return Task.FromResult(0);});Require(invoked==0);});
        await Test("new reader readback precedes continuation",async()=>{int reader=1;int calls=0;await WindowsBrokerUpgradeStartup.Ensure(()=>null,()=>reader,()=>{calls++;reader=2;return Task.FromResult(0);});Require(calls==1);});
        await Test("pending partial contract is not read before recovery",async()=>{bool recovered=false;int calls=0;await WindowsBrokerUpgradeStartup.Ensure(()=>recovered?null:State("replacing"),()=>{Require(recovered);return 2;},()=>{calls++;recovered=true;return Task.FromResult(0);});Require(calls==1);});
        await Test("cancel never loops or starts game",async()=>{int calls=0;await Reject(()=>WindowsBrokerUpgradeStartup.Ensure(()=>null,()=>1,()=>{calls++;return Task.FromResult(1223);}));Require(calls==1);});
        await Test("failed actor never loops",async()=>{int calls=0;await Reject(()=>WindowsBrokerUpgradeStartup.Ensure(()=>null,()=>1,()=>{calls++;return Task.FromResult(1);}));Require(calls==1);});
        await Test("exit zero without sufficient reader is not success",()=>Reject(()=>WindowsBrokerUpgradeStartup.Ensure(()=>null,()=>1,()=>Task.FromResult(0))));
        await Test("exit zero with pending state is not success",()=>Reject(()=>WindowsBrokerUpgradeStartup.Ensure(()=>State("prepared"),()=>2,()=>Task.FromResult(0))));
        await Test("concurrent successful actor wins over local cancellation",async()=>{int reader=1;await WindowsBrokerUpgradeStartup.Ensure(()=>null,()=>reader,()=>{reader=2;return Task.FromResult(1223);});});
        await Test("committed actor cleanup failure cannot block ready broker",async()=>{int reader=1;BrokerUpgradeState? state=null;await WindowsBrokerUpgradeStartup.Ensure(()=>state,()=>reader,()=>{state=State("committed");reader=2;return Task.FromResult(1);});});
        await Test("bad status never requests elevation",async()=>{int calls=0;await Reject(()=>WindowsBrokerUpgradeStartup.Ensure(()=>throw new IOException("foreign"),()=>2,()=>{calls++;return Task.FromResult(0);}));Require(calls==0);});
        await Test("restored old component still needs upgrade",async()=>{int reader=1,calls=0;BrokerUpgradeState? state=State("restored");await WindowsBrokerUpgradeStartup.Ensure(()=>state,()=>reader,()=>{calls++;reader=2;state=null;return Task.FromResult(0);});Require(calls==1);});
        await Test("corrupt reader without transaction fails visibly",()=>Reject(()=>WindowsBrokerUpgradeStartup.Ensure(()=>null,()=>throw new IOException("corrupt"),()=>throw new Exception("unexpected UAC"))));
        Console.WriteLine($"Broker startup: {count} passed; orchestrator only, native UAC/ACL/process admission NotRun");
    }
}
