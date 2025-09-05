public class StateMachine {
    IState _state;
    public void Set(IState s){ _state?.Exit(); _state = s; _state?.Enter(); }
    public void Tick(){ _state?.Tick(); }
}