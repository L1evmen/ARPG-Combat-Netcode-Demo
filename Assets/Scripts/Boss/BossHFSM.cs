public class BossHFSM
{
    readonly CompositeState _root;

    public BossHFSM(CompositeState root) => _root = root;

    public IState CurrentLeaf => _root.CurrentLeaf;

    public void Update() => _root.Update();
    public void Change(IState state) => _root.Change(state);
    public void OnEvent(object evt) => _root.OnEvent(evt);
}
