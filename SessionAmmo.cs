namespace SprocketSmokeLaunchers;
// A bounded session ledger survives object release/copies; only native combat boundaries replace it.
public sealed class SessionLedger
{
    public const int MaxTickets=1024;
    private readonly HashSet<int> spent=new();
    private int allocated;
    public string Id {get;private set;}="";
    public bool Active {get;private set;}
    public bool Begin(string id) { if(Active) return false; Id=id; Active=true; allocated=0; spent.Clear(); return true; }
    public void End() { Active=false; }
    public int Allocate() => Active && allocated<MaxTickets ? ++allocated : 0;
    public bool Known(int ticket) => ticket>0 && ticket<=allocated;
    public bool IsSpent(int ticket) => !Active || !Known(ticket) || spent.Contains(ticket);
    public bool Spend(int ticket) => Active && Known(ticket) && spent.Add(ticket);
}
public sealed class SessionAmmo
{
    private string session="";
    private int ticket;
    private void Ensure(SessionLedger ledger)
    { if(ledger.Active && session!=ledger.Id) { session=ledger.Id; ticket=ledger.Allocate(); } }
    public bool IsSpent(SessionLedger ledger) { Ensure(ledger); return ledger.IsSpent(ticket); }
    public bool TrySpend(SessionLedger ledger) { Ensure(ledger); return ledger.Spend(ticket); }
    public string Save(SessionLedger ledger)
    { Ensure(ledger); return ledger.Active ? $"2:{session}:{ticket}:{(ledger.IsSpent(ticket)?1:0)}" : "1:0"; }
    public void Load(SessionLedger ledger,string? value)
    {
        if(!ledger.Active) return; // editor designs always prepare a loaded bank for the next combat
        Ensure(ledger);
        if(value==null || value=="1:0" || value=="1:1") return; // legacy permanent-spent policy intentionally superseded
        var fields=value.Split(':');
        bool valid=fields.Length==4 && fields[0]=="2" && fields[1].Length==32 && fields[1].All(Uri.IsHexDigit) &&
            int.TryParse(fields[2],out int parsed) && parsed>0 && parsed<=SessionLedger.MaxTickets && (fields[3]=="0" || fields[3]=="1");
        if(!valid) { ledger.Spend(ticket); return; }
        if(fields[1]!=ledger.Id) return; // previous combat state is fresh preparation for this combat
        int incoming=int.Parse(fields[2]); bool wasSpent=ledger.IsSpent(ticket);
        if(!ledger.Known(incoming)) { ledger.Spend(ticket); return; } // never import forged/unknown current-session identity
        ticket=incoming;
        if(wasSpent || fields[3]=="1") ledger.Spend(ticket); // no load or duplicate can refill a live spent bank
    }
}
