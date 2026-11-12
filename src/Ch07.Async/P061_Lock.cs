namespace MasteringModernCSharp.Ch07;

public class Account(decimal opening)
{
    // A dedicated lock object. `lock` on a Lock uses its fast
    // EnterScope path instead of Monitor.
    private readonly Lock _gate = new();
    private decimal _balance = opening;

    public decimal Balance
    {
        get { lock (_gate) return _balance; }
    }

    public bool TryWithdraw(decimal amount)
    {
        lock (_gate)
        {
            if (_balance < amount) return false;
            _balance -= amount;
            return true;
        }
    }

    public void Deposit(decimal amount)
    {
        // The same thing, written out by hand.
        using (_gate.EnterScope())
        {
            _balance += amount;
        }
    }
}
