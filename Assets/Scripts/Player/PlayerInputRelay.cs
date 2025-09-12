using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteractor))]
public class PlayerInputRelay : MonoBehaviour
{
    BallInteractor interactor;
    void Awake(){ interactor = GetComponent<BallInteractor>(); }
    public void OnKick(InputValue v){ if(v.isPressed) interactor.DoKick(); }
    public void OnPass(InputValue v){ if(v.isPressed) interactor.DoPass(); }
    public void OnSwitchPlayer(InputValue v){ /* TODO */ }
}