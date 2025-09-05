using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteractor))]
public class PlayerInputRelay : MonoBehaviour
{
    BallInteractor interactor;
    void Awake(){ interactor = GetComponent<BallInteractor>(); }
    public void OnKick(InputValue v){ if(v.isPressed) interactor.KickButton(); }
    public void OnPass(InputValue v){ if(v.isPressed) interactor.PassButton(); }
    public void OnSwitchPlayer(InputValue v){ /* TODO */ }
}