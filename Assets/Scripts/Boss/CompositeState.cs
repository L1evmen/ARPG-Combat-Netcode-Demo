public abstract class CompositeState : IState, IEventReceiver
{
    IState _current;

    public virtual void Enter() => _current?.Enter();
    public virtual void Update() => _current?.Update();
    public virtual void Exit()  => _current?.Exit();

    public void Change(IState to)
    {
        _current?.Exit();
        _current = to;
        _current?.Enter();
    }

    public IState CurrentLeaf => _current is CompositeState cs ? cs.CurrentLeaf : _current;

    public virtual void OnEvent(object evt)
    {
        if (_current is IEventReceiver r) r.OnEvent(evt);
    }
}
