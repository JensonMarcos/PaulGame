using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSync : NetworkBehaviour
{
    [SerializeField] int sendInterval = 1; //ticks
    [SerializeField] float interpolationDelay = 3f; 
    [SerializeField] float maxExtrapolation = 5f; 
    [SerializeField] float clockCorrection = 1f;
    [SerializeField] float clockSnapThreshold = 10f; 

    const int bufferSize = 32;
    readonly List<PlayerState> buffer = new List<PlayerState>(bufferSize);
    readonly ulong[] forwardExcluded = new ulong[2];
    float renderTick;

    public void Send(PlayerState _state)
    {
        if (_state.Tick % sendInterval != 0) return;

        if (IsServer) Forward(_state);
        else SendStateServerRpc(_state);
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
    void SendStateServerRpc(PlayerState _state)
    {
        Receive(_state);
        Forward(_state);
    }

    void Forward(PlayerState _state)
    {
        //the owner made it and the host already has it
        forwardExcluded[0] = OwnerClientId;
        forwardExcluded[1] = NetworkManager.ServerClientId;
        StateClientRpc(_state, RpcTarget.Not(forwardExcluded, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
    void StateClientRpc(PlayerState _state, RpcParams _rpcParams = default)
    {
        Receive(_state);
    }

    void Receive(PlayerState _state)
    {
        //unreliable can arrive out of order, keep the buffer sorted by tick
        int _index = buffer.Count;
        while (_index > 0 && buffer[_index - 1].Tick > _state.Tick) _index--;
        if (_index > 0 && buffer[_index - 1].Tick == _state.Tick) return;

        buffer.Insert(_index, _state);
        if (buffer.Count > bufferSize) buffer.RemoveAt(0);
    }

    public bool TryGetState(out PlayerState _state)
    {
        _state = default;
        if (buffer.Count == 0) return false;

        float _tickRate = NetworkManager.NetworkConfig.TickRate;
        int _newestTick = buffer[buffer.Count - 1].Tick;

        //render clock runs a fixed delay behind the newest state, nudged back whenever it drifts
        renderTick += Time.deltaTime * _tickRate;
        float _drift = _newestTick - interpolationDelay - renderTick;
        if (Mathf.Abs(_drift) > clockSnapThreshold) renderTick = _newestTick - interpolationDelay;
        else renderTick += _drift * clockCorrection * Time.deltaTime;

        //drop states we've passed, keeping the last one at or before the render tick
        while (buffer.Count > 1 && buffer[1].Tick <= renderTick) buffer.RemoveAt(0);

        PlayerState _from = buffer[0];
        if (buffer.Count == 1 || renderTick <= _from.Tick)
        {
            //nothing newer yet, keep moving along the last velocity for a bit
            float _ahead = Mathf.Clamp(renderTick - _from.Tick, 0f, maxExtrapolation);
            _state = _from;
            _state.Position += _from.Velocity * (_ahead / _tickRate);
            return true;
        }

        PlayerState _to = buffer[1];
        float _t = (renderTick - _from.Tick) / (_to.Tick - _from.Tick);
        if (_from.Teleports != _to.Teleports) _t = 0f; //snap on teleport instead of sliding across the map

        _state = _from;
        _state.Position = Vector3.Lerp(_from.Position, _to.Position, _t);
        _state.CameraPitch = Mathf.LerpAngle(_from.CameraPitch, _to.CameraPitch, _t);
        _state.CameraYaw = Mathf.LerpAngle(_from.CameraYaw, _to.CameraYaw, _t);
        _state.Velocity = Vector3.Lerp(_from.Velocity, _to.Velocity, _t);
        _state.Aiming = Mathf.Lerp(_from.Aiming, _to.Aiming, _t);
        _state.Reloading = Mathf.Lerp(_from.Reloading, _to.Reloading, _t);
        return true;
    }
}
